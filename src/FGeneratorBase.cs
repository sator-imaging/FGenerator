using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace FGenerator
{
    /// <summary>
    /// Base implementation for incremental generators that locate targets and emit sources or diagnostics.
    /// </summary>
    public abstract class FGeneratorBase : IIncrementalGenerator
    {
        /// <summary>
        /// Prefix appended to all diagnostic IDs emitted by this generator (e.g., "GEN").
        /// </summary>
        protected abstract string DiagnosticIdPrefix { get; }

        /// <summary>
        /// Diagnostic category reported to Roslyn analyzers and IDE tooling.
        /// </summary>
        protected abstract string DiagnosticCategory { get; }

        /// <summary>
        /// Indicates whether to merge the target provider with the compilation provider so targets carry their Compilation.
        /// If <see cref="TargetAttributeName"/> is null (enumerate all types), the providers are always combined.
        /// Optional (default false); setting this to false when attributes are used can improve performance by avoiding the combine step.
        /// </summary>
        protected virtual bool CombineCompilationProvider { get; }

        /// <summary>
        /// Gets the name of the attribute to search for on target symbols.
        /// If null, all types in the compilation will be enumerated as targets.
        /// The "Attribute" suffix is optional - specifying "My" will match both [My] and [MyAttribute].
        /// Should be a simple name (e.g., "MyAttribute"). Namespaces are ignored.
        /// </summary>
        protected abstract string? TargetAttributeName { get; }

        /// <summary>
        /// Optional support code added during post-initialization (e.g., attribute definitions).
        /// </summary>
        protected abstract string? PostInitializationOutput { get; }

        // TODO: for v2, support multiple diagnostics by `out ImmutableArray<AnalyzeResult>`
        //       (may hurt performance, but useful for reporting warnings and performance tips at once)

        /// <summary>
        /// Generates code for a target or returns diagnostic to report.
        /// </summary>
        /// <param name="target">The discovered target symbol and metadata.</param>
        /// <param name="diagnostic">Output diagnostic to emit when generation fails.</param>
        /// <returns>Generated source payload or null when only diagnostic are produced.</returns>
        protected abstract CodeGeneration? Generate(Target target, out AnalyzeResult? diagnostic);

        private string? _targetAttributeBaseName;
        private string? _targetAttributeNameWithSuffix;
        private string? _targetAttributeBaseNameWithDot;
        private string? _targetAttributeNameWithSuffixWithDot;
        private string? _generatedCodeHeader;

        /// <summary>
        /// Entry point invoked by Roslyn to configure incremental generation.
        /// </summary>
        /// <param name="context">Initialization context used to register pipelines.</param>
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            // Compute base attribute name once (without "Attribute" suffix if present)
            if (TargetAttributeName != null)
            {
                _targetAttributeBaseName = TargetAttributeName.EndsWith("Attribute")
                    ? TargetAttributeName.Substring(0, TargetAttributeName.Length - "Attribute".Length)
                    : TargetAttributeName;

                _targetAttributeNameWithSuffix = $"{_targetAttributeBaseName}Attribute";
                _targetAttributeBaseNameWithDot = $".{_targetAttributeBaseName}";
                _targetAttributeNameWithSuffixWithDot = $".{_targetAttributeNameWithSuffix}";
            }

            // Register post-initialization output for support contents
            var postInit = PostInitializationOutput;
            if (!string.IsNullOrWhiteSpace(postInit))
            {
                context.RegisterPostInitializationOutput(ctx =>
                {
                    ctx.AddSource(
                        $"- {nameof(FGenerator)} - {this.GetType().Name}.g.cs",
                        GenerateSourceText((((postInit)))!));
                });
            }

            // Filter for targets with the attribute (or all types if no attribute specified)
            IncrementalValuesProvider<Target> provider;

            if (string.IsNullOrWhiteSpace(_targetAttributeBaseName))
            {
                // Enumerate all types in the compilation
                provider = context.CompilationProvider
                    .SelectMany(static (compilation, _) =>
                    {
                        return EnumerateTypes(compilation, compilation.Assembly.GlobalNamespace);
                    });
            }
            else
            {
                // Filter for targets with the attribute
                provider = context.SyntaxProvider
                    .CreateSyntaxProvider(
                        predicate: (s, _) => IsSyntaxTargetForGeneration(s),
                        transform: (ctx, _) => GetSemanticTargetForGeneration(ctx))
                    .Where(t => t != null)
                    !;  // netstandard2.0 doesn't have .OfType<T>...!

                if (CombineCompilationProvider)
                {
                    provider = provider
                        .Combine(context.CompilationProvider)
                        .Select(static (pair, _) =>
                        {
                            var (target, compilation) = pair;

                            // TODO: target = target with { Compilation = c };
                            return new Target(target.RawSymbol, target.RawAttributes, compilation);
                        });
                }
            }

            // Generate source
            context.RegisterSourceOutput(provider, (spc, target) =>
            {
#if DEBUG
                foreach (var ra in target.RawAttributes)
                {
                    var location = ra.ApplicationSyntaxReference?.GetSyntax().GetLocation();
                    if (location != null)
                    {
                        var d = new AnalyzeResult("", "DEBUG", DiagnosticSeverity.Info, $"Attribute found: {target}");
                        spc.ReportDiagnostic(d.ToDiagnostic(DiagnosticIdPrefix, DiagnosticCategory, location));
                    }
                }
#endif

                var codeGen = Generate(target, out AnalyzeResult? diagnosticResult);

                if (diagnosticResult.HasValue)
                {
                    var result = diagnosticResult.Value;

                    if (target.RawSymbol is IAssemblySymbol)
                    {
                        foreach (var attr in target.RawAttributes)
                        {
                            var location = attr.ApplicationSyntaxReference?.GetSyntax().GetLocation();
                            if (location != null)
                            {
                                spc.ReportDiagnostic(result.ToDiagnostic(DiagnosticIdPrefix, DiagnosticCategory, location));
                            }
                        }
                    }
                    else
                    {
                        foreach (var location in target.RawSymbol.Locations)
                        {
                            spc.ReportDiagnostic(result.ToDiagnostic(DiagnosticIdPrefix, DiagnosticCategory, location));
                        }
                    }
                }

                if (codeGen.HasValue)
                {
                    spc.AddSource(codeGen.Value.HintName, GenerateSourceText(codeGen.Value.Source));
                }
            });
        }

        private SourceText GenerateSourceText(string source)
        {
            if (!source.Contains("<auto-generated", StringComparison.OrdinalIgnoreCase))
            {
                _generatedCodeHeader ??=
$@"// <auto-generated>{this.GetType().FullName}</auto-generated>

";

                source = $"{_generatedCodeHeader}{source}";
            }

            return SourceText.From(source, Encoding.UTF8);
        }

        private bool IsSyntaxTargetForGeneration(SyntaxNode node)
        {
            // Get attribute lists based on node type
            SyntaxList<AttributeListSyntax> attributeLists = node switch
            {
                MemberDeclarationSyntax m => m.AttributeLists,        // types, methods, properties, fields, events, etc.
                ParameterSyntax p => p.AttributeLists,                // method/constructor/indexer parameters
                TypeParameterSyntax tp => tp.AttributeLists,          // generic type parameters
                CompilationUnitSyntax cu => cu.AttributeLists,        // assembly/module-level attributes
                _ => default
            };

            if (attributeLists.Count == 0)
            {
                return false;
            }

            var _targetAttributeBaseName = this._targetAttributeBaseName;
            var _targetAttributeNameWithSuffix = this._targetAttributeNameWithSuffix;

            // Check if any attribute name matches the target attribute name (syntax-level check)
            foreach (var attributeList in attributeLists)
            {
                foreach (var attribute in attributeList.Attributes)
                {
                    var lastIdentifier = attribute.Name switch
                    {
                        // NOTE: in C# 14, attribute cannot be generic anyway include GenericNameSyntax.
                        //       * IdentifierNameSyntax and GenericNameSyntax inherit from SimpleNameSyntax
                        SimpleNameSyntax s => s.Identifier.Text,
                        QualifiedNameSyntax q => q.Right.Identifier.Text,
                        AliasQualifiedNameSyntax a => a.Name.Identifier.Text,
                        _ => attribute.Name.ToString(),
                    };

                    if (lastIdentifier == _targetAttributeBaseName ||
                        lastIdentifier == _targetAttributeNameWithSuffix)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private Target? GetSemanticTargetForGeneration(GeneratorSyntaxContext context)
        {
            // Get the symbol from the syntax node (mirrors behavior in IsSyntaxTargetForGeneration)
            ISymbol? symbol = context.Node switch
            {
                MemberDeclarationSyntax m => context.SemanticModel.GetDeclaredSymbol(m),
                ParameterSyntax p => context.SemanticModel.GetDeclaredSymbol(p),
                TypeParameterSyntax tp => context.SemanticModel.GetDeclaredSymbol(tp),
                CompilationUnitSyntax => context.SemanticModel.Compilation.Assembly,
                _ => null
            };

            if (symbol == null)
            {
                return null;
            }

            var _targetAttributeBaseName = this._targetAttributeBaseName;
            var _targetAttributeNameWithSuffix = this._targetAttributeNameWithSuffix;

            // Collect all matching attributes from the symbol
            var matchingAttributes = symbol.GetAttributes();

            if (symbol is IMethodSymbol methodSymbol)
            {
                matchingAttributes = matchingAttributes.AddRange(methodSymbol.GetReturnTypeAttributes());
            }

            matchingAttributes = matchingAttributes
                .Where(attr =>
                {
                    var attrType = attr.AttributeClass;
                    if (attrType == null)
                    {
                        return false;
                    }

                    string simpleName = attrType.Name;

                    return simpleName == _targetAttributeBaseName ||
                           simpleName == _targetAttributeNameWithSuffix;
                })
                .ToImmutableArray();

            if (matchingAttributes.IsEmpty)
            {
                return null;
            }

            return new Target(symbol, matchingAttributes);
        }

        private static IEnumerable<Target> EnumerateTypes(Compilation compilation, INamespaceSymbol namespaceSymbol)
        {
            foreach (var member in namespaceSymbol.GetMembers())
            {
                if (member is INamespaceSymbol childNamespace)
                {
                    foreach (var t in EnumerateTypes(compilation, childNamespace))
                    {
                        yield return t;
                    }
                }
                else if (member is INamedTypeSymbol typeSymbol)
                {
                    yield return CreateTargetFromType(compilation, typeSymbol);

                    // Recursively enumerate nested types
                    foreach (var nested in EnumerateNestedTypes(compilation, typeSymbol))
                    {
                        yield return nested;
                    }
                }
            }
        }

        private static IEnumerable<Target> EnumerateNestedTypes(Compilation compilation, INamedTypeSymbol typeSymbol)
        {
            foreach (var nestedType in typeSymbol.GetTypeMembers())
            {
                yield return CreateTargetFromType(compilation, nestedType);

                foreach (var deep in EnumerateNestedTypes(compilation, nestedType))
                {
                    yield return deep;
                }
            }
        }

        private static Target CreateTargetFromType(Compilation compilation, INamedTypeSymbol typeSymbol)
        {
            return new Target(
                typeSymbol,
                ImmutableArray<AttributeData>.Empty // No attributes when enumerating all types
            )
            {
                Compilation = compilation,
            };
        }

    }
}
