#:project ../src
#:property TargetFramework=netstandard2.0
#:property IsRoslynComponent=true
#:property PublishAot=false
#:property LangVersion=latest
#:property OutputType=Library

using FGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>
/// Analyzer that audits async methods for calls to UnityEngine.Object members.
/// This is important because Unity's Object methods must be called from the main thread,
/// but async methods may resume on different threads after await points.
/// </summary>
[Generator]
public sealed class UnityAsyncMethodAuditor : FGeneratorBase
{
    /// <summary>
    /// Diagnostic category for any errors/warnings this analyzer produces.
    /// </summary>
    protected override string DiagnosticCategory => nameof(UnityAsyncMethodAuditor);

    /// <summary>
    /// Prefix for diagnostic IDs (e.g., "UAMA001").
    /// </summary>
    protected override string DiagnosticIdPrefix => "UAMA";

    protected override bool CombineCompilationProvider => true;

    /// <summary>
    /// Target all types by setting this to null.
    /// </summary>
    protected override string? TargetAttributeName => null;

    /// <summary>
    /// No attribute to inject since we're analyzing all types.
    /// </summary>
    protected override string? PostInitializationOutput =>
$@"using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using UnityEngine;

#nullable enable

internal static class {nameof(UnityAsyncMethodAuditor)}
{{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryGet<TObject, TValue>([MaybeNullWhen(false)] this TObject self, Func<TObject, TValue> getter, out TValue result)
        where TObject : global::UnityEngine.Object
    {{
        if (self == null)
        {{
            result = (((default)))!;
            return false;
        }}

        result = getter.Invoke(self);
        return true;
    }}

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TrySet<TObject, TValue>([MaybeNullWhen(false)] this TObject self, TValue value, Action<TObject, TValue> setter)
        where TObject : global::UnityEngine.Object
    {{
        if (self == null)
        {{
            return false;
        }}

        setter.Invoke(self, value);
        return true;
    }}

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryRun<TObject>([MaybeNullWhen(false)] this TObject self, Action<TObject> action)
        where TObject : global::UnityEngine.Object
    {{
        if (self == null)
        {{
            return false;
        }}

        action.Invoke(self);
        return true;
    }}
}}
";

    /// <summary>
    /// Main analysis method called for each type found.
    /// </summary>
    /// <param name="target">The target type to analyze.</param>
    /// <param name="diagnostic">Output parameter for any diagnostic to report.</param>
    /// <returns>Null since this is an analyzer, not a generator.</returns>
    protected override CodeGeneration? Generate(Target target, out AnalyzeResult? diagnostic)
    {
        diagnostic = null;

        // Ensure the target is a named type (class, struct, etc.)
        if (target.Compilation == null ||
            target.RawSymbol is not INamedTypeSymbol typeSymbol)
        {
            return null;
        }

        // Get all async methods in this type
        var asyncMethods = typeSymbol.GetMembers()
            .OfType<IMethodSymbol>()
            .Where(m => m.IsAsync);

        // Analyze each async method
        foreach (var method in asyncMethods)
        {
            var methodDiagnostic = AnalyzeAsyncMethod(method, target.Compilation, typeSymbol);
            if (methodDiagnostic != null)
            {
                diagnostic = methodDiagnostic;
                return null;
            }
        }

        return null;
    }

    /// <summary>
    /// Analyzes an async method for calls to UnityEngine.Object members.
    /// </summary>
    private AnalyzeResult? AnalyzeAsyncMethod(
        IMethodSymbol method,
        Compilation compilation,
        INamedTypeSymbol analyzingMethodContainer
    )
    {
        foreach (var syntaxReference in method.DeclaringSyntaxReferences)
        {
            if (syntaxReference == null)
            {
                return null;
            }

            var methodSyntax = syntaxReference.GetSyntax() as MethodDeclarationSyntax;
            if (methodSyntax?.Body == null && methodSyntax?.ExpressionBody == null)
            {
                return null;
            }

            // Receiver-less call
            var nameOnlyInvocation = !IsUnityObject(analyzingMethodContainer)
                ? null
                : methodSyntax
                    .DescendantNodes()
                    .OfType<InvocationExpressionSyntax>()
                    .Select(x => x.Expression)
                    .OfType<IdentifierNameSyntax>()
                    .FirstOrDefault();

            if (nameOnlyInvocation != null)
            {
                return CreateResult(method, analyzingMethodContainer, nameOnlyInvocation);
            }
            else
            {
                // NOTE: must be same length!!
                const string TryGet = nameof(TryGet);
                const string TrySet = nameof(TrySet);
                const string TryRun = nameof(TryRun);

                // Check all member access expressions
                var memberAccesses =
                    methodSyntax.DescendantNodes(node =>
                    {
                        if (node is not InvocationExpressionSyntax ie ||
                            ie.Expression is not MemberAccessExpressionSyntax mae)
                        {
                            return true;
                        }

                        var name = mae.Name.Identifier.Text;

                        return !(name.Length == TryRun.Length && name is TrySet or TryGet or TryRun);  // Try* methods
                    })
                    .Select(x =>
                    {
                        var (node, name) = x switch
                        {
                            MemberAccessExpressionSyntax s => (s.Expression, s.Name.Identifier.Text),
                            ConditionalAccessExpressionSyntax s => (s.Expression, string.Empty),
                            _ => (null, string.Empty),
                        };

                        if (name.Length == TryRun.Length && name is TrySet or TryGet or TryRun)  // Try* methods
                        {
                            return null;
                        }

                        return node;
                    })
                    .OfType<ExpressionSyntax>();

                foreach (var expression in memberAccesses)
                {
                    INamedTypeSymbol? receiverType = null;

                    // Handle 'this'
                    if (expression is ThisExpressionSyntax)
                    {
                        receiverType = analyzingMethodContainer;
                    }

                    receiverType ??= GetReceiverType(expression, compilation);

                    if (receiverType != null && IsUnityObject(receiverType))
                    {
                        return CreateResult(method, receiverType, expression);
                    }
                }
            }
        }

        return null;

        static AnalyzeResult CreateResult(
            IMethodSymbol method,
            INamedTypeSymbol receiverType,
            SyntaxNode violationNode
        )
        {
            return new AnalyzeResult(
                "001",
                "Unity object access in async method",
                DiagnosticSeverity.Error,
                $"Async method '{method.Name}' accesses the member of Unity type '{receiverType.Name}'.",
                (violationNode.Parent ?? violationNode).GetLocation());
        }
    }

    /// <summary>
    /// Gets the type of the receiver expression.
    /// </summary>
    private INamedTypeSymbol? GetReceiverType(
        ExpressionSyntax expression,
        Compilation compilation
    )
    {
        // Use SemanticModel to resolve the type of the expression
        var semanticModel = compilation.GetSemanticModel(expression.SyntaxTree);
        var symbol = semanticModel.GetTypeInfo(expression).Type as INamedTypeSymbol;

        return symbol;
    }

    /// <summary>
    /// Checks if a type is a Unity object.
    /// </summary>
    private bool IsUnityObject(INamedTypeSymbol typeSymbol)
    {
        var currentType = typeSymbol;
        while (currentType != null)
        {
            if (currentType.ContainingNamespace?.Name is "UnityEngine" &&
                currentType.ContainingNamespace.ContainingNamespace?.IsGlobalNamespace == true)
            {
                if (currentType.Name is "MonoBehaviour" or "Object")// or "GameObject" or "Component")
                {
                    return true;
                }
            }

            currentType = currentType.BaseType;
        }

        return false;
    }
}
