// Licensed under the Apache-2.0 License
// https://github.com/sator-imaging/FGenerator

#:sdk FGenerator.Sdk@2.5.1

using FGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Buffers;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

#pragma warning disable IDE0051  // Remove unused private members

[Generator]
public sealed class MacroDotNetGenerator : FGeneratorBase
{
    private const byte MaxArgCount = 10;
    private const string DebugSymbol = "DEBUG";
    private static readonly Regex DisplayPrefixRegex = new Regex(@"^[^_]*_", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    protected override string DiagnosticCategory => nameof(MacroDotNetGenerator);
    protected override string DiagnosticIdPrefix => "MACRO";

    // Scan all types, then collect [Macro]-annotated fields from each type.
    protected override string? TargetAttributeName => null;
    protected override bool CombineCompilationProvider => true;

    protected override string? PostInitializationOutput =>
@"using System;

#if NET7_0_OR_GREATER == false
namespace System.Diagnostics.CodeAnalysis
{
    [System.AttributeUsage(System.AttributeTargets.Field | System.AttributeTargets.Parameter | System.AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
    public sealed class StringSyntaxAttribute : Attribute
    {
        public StringSyntaxAttribute(string syntax)
        {
        }
    }
}
#endif

namespace MacroDotNet
{
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = true, Inherited = false)]
    internal sealed class MacroAttribute : Attribute
    {
        public MacroAttribute(
            [System.Diagnostics.CodeAnalysis.StringSyntax(""C#-test"")] string template,
            [System.Diagnostics.CodeAnalysis.StringSyntax(""C#-test"")] params string[] args)
        {
        }
    }
}
";

    protected override CodeGeneration? Generate(Target target, out AnalyzeResult? diagnostic)
    {
        diagnostic = null;

        if (target.RawSymbol is not INamedTypeSymbol type)
        {
            diagnostic = new AnalyzeResult("001", "Invalid target", DiagnosticSeverity.Error, "MacroDotNetGenerator only supports named types.");
            return null;
        }

        // 100 lines of C# code is larger than 2k. ex: DefaultUsings.Length > 512
        const int OutputBufferCapacity = 16384;
        const int ScratchBufferCapacity = 1024;

        ValuePoolBuffer? outputBufferHolder = null;
        ValuePoolBuffer? scratchBufferHolder = null;
        try
        {
            foreach (var field in type.GetMembers().OfType<IFieldSymbol>().Where(x => HasMacroAttribute(x)))
            {
                var output = outputBufferHolder ?? new ValuePoolBuffer(OutputBufferCapacity);
                if (outputBufferHolder == null)
                {
                    init(ref output, target, type);

                    static void init(ref ValuePoolBuffer output, Target target, INamedTypeSymbol type)
                    {
                        output.Append(DefaultUsings);
                        output.Append('\n');
                        AppendDeclarationSiteUsings(type, ref output);
                        output.Append(target.ToNamespaceAndContainingTypeDeclarations());
                        output.Append('\n');
                        output.Append("    partial ");
                        output.Append(target.ToDeclarationString(modifiers: false));
                        output.Append('\n');
                        output.Append("    {");
                        output.Append('\n');
                    }
                }

                output.Append('\n');
                output.Append("#region  ==== ");
                output.Append(field.Name);
                output.Append(" ====\n\n");

                foreach (var (template, args) in GetMacroAttributes(field))
                {
                    var templateSpan = template.AsSpan();

                    int firstTokenIndex = templateSpan.IndexOf('$');
                    if (firstTokenIndex < 0)
                    {
                        output.Append(templateSpan);
                    }
                    else
                    {
                        ValuePoolBuffer work;
                        if (scratchBufferHolder == null)
                        {
                            int capacity = 256 + template.Length;
                            if (capacity < ScratchBufferCapacity)
                            {
                                capacity = ScratchBufferCapacity;
                            }
                            work = new ValuePoolBuffer(capacity);
                        }
                        else
                        {
                            work = scratchBufferHolder.Value;
                        }

                        work.Clear();

                        if (!args.IsEmpty())
                        {
                            if (!args.IsValid())
                            {
                                diagnostic = new AnalyzeResult(
                                    "003",
                                    "Too many macro args",
                                    DiagnosticSeverity.Error,
                                    $"Field '{field.Name}' has {args.Count} macro args. At most {MaxArgCount} args are supported ($0 to $9).");

                                return null;
                            }

                            AppendWithReplaceMacroArgs(ref work, templateSpan, firstTokenIndex, args);
                        }
                        else
                        {
                            work.Append(templateSpan);
                        }

                        AppendWithTokenReplacements(in work, ref output, field);

                        scratchBufferHolder = work;  // Write back updated struct to Nullable<T> here!
                    }

                    output.Append("\n\n");
                }

                output.Append("#endregion\n\n");

                outputBufferHolder = output;  // Write back updated struct to Nullable<T> here!
            }

            if (outputBufferHolder == null)
            {
                return null;
            }

            if (!target.IsPartial)
            {
                diagnostic = new AnalyzeResult("002", "Containing type must be partial", DiagnosticSeverity.Error,
                    $"Type '{type.ToNameString(localName: true)}' must be declared partial to use [Macro].");

                return null;
            }

            var result = outputBufferHolder.Value;
            result.Append('\n');
            result.Append("    }");
            result.Append('\n');
            result.Append(target.ToNamespaceAndContainingTypeClosingBraces());
            result.Append('\n');

            var generatedCode = result.ToString();

            if (IsDebugSymbolDefined(target.Compilation))
            {
                BuildGeneratedSyntaxDiagnostic(generatedCode, out diagnostic);
            }

            return new CodeGeneration(target.ToHintName(), generatedCode);
        }
        finally
        {
            outputBufferHolder?.Dispose();
            scratchBufferHolder?.Dispose();
        }
    }

    private static bool IsDebugSymbolDefined(Compilation? compilation)
    {
        if (compilation is CSharpCompilation csharpCompilation)
        {
            return csharpCompilation.Options.OptimizationLevel == OptimizationLevel.Debug;

            /*
            foreach (var tree in csharpCompilation.SyntaxTrees)
            {
                if (tree.Options is not CSharpParseOptions parseOptions)
                {
                    continue;
                }

                foreach (var symbol in parseOptions.PreprocessorSymbolNames)
                {
                    if (symbol == DebugSymbol)
                    {
                        return true;
                    }
                }
            }
            */
        }

        return false;
    }

    private static void BuildGeneratedSyntaxDiagnostic(string generatedCode, out AnalyzeResult? diagnostic)
    {
        var tree = CSharpSyntaxTree.ParseText(generatedCode);

        var generatedCodeSpan = generatedCode.AsSpan(DefaultUsings.Length);
        ValuePoolBuffer? bufferHolder = null;
        try
        {

            foreach (var parseDiagnostic in tree.GetDiagnostics())
            {
                if (parseDiagnostic.Severity != DiagnosticSeverity.Error)
                {
                    continue;
                }

                var work = bufferHolder ?? new ValuePoolBuffer(capacity: 512 + generatedCodeSpan.Length);
                if (bufferHolder == null)
                {
                    work.Append("Macro causes syntactic error.\n");
                }

                var span = parseDiagnostic.Location.GetLineSpan();
                var line = span.StartLinePosition.Line + 1;
                var column = span.StartLinePosition.Character + 1;

                work.Append('(');
                work.Append(line);
                work.Append(',');
                work.Append(column);
                work.Append(") ");
                work.Append(parseDiagnostic.Id);
                work.Append(' ');
                work.Append(parseDiagnostic.GetMessage(CultureInfo.CurrentCulture));
                work.Append('\n');

                bufferHolder = work;  // Write back updated struct to Nullable<T> here!
            }

            var result = bufferHolder ?? new ValuePoolBuffer(capacity: 8 + generatedCodeSpan.Length);

            if (bufferHolder == null)
            {
                result.Append("\n---");
                result.Append(generatedCodeSpan);

                diagnostic = new AnalyzeResult(
                    "_DEBUG",
                    "Generated code preview",
                    DiagnosticSeverity.Info,
                    result.ToString());
            }
            else
            {
                result.Append("---");
                result.Append(generatedCodeSpan);

                diagnostic = new AnalyzeResult(
                    "004",
                    "Generated syntax errors",
                    DiagnosticSeverity.Error,
                    result.ToString());
            }
        }
        finally
        {
            bufferHolder?.Dispose();
        }
    }

    private static void AppendDeclarationSiteUsings(INamedTypeSymbol type, ref ValuePoolBuffer output)
    {
        bool hasAny = false;

        foreach (var syntaxRef in type.DeclaringSyntaxReferences)
        {
            if (syntaxRef.GetSyntax() is not TypeDeclarationSyntax declaration)
            {
                continue;
            }

            if (declaration.SyntaxTree.GetRoot() is CompilationUnitSyntax compilationUnit)
            {
                foreach (var usingDirective in compilationUnit.Usings)
                {
                    var text = usingDirective.WithoutTrivia().ToFullString();
                    if (text.Length == 0)
                    {
                        continue;
                    }

                    output.Append(text);
                    output.Append('\n');

                    hasAny = true;
                }
            }
        }

        if (hasAny)
        {
            output.Append('\n');
        }
    }

    private static string StripTrailingNullableMarker(string value)
    {
        if (value.Length > 0 && value[value.Length - 1] == '?')
        {
            return value.Substring(0, value.Length - 1);
        }

        return value;
    }

    private static string ToDisplayName(string rawFieldName)
    {
        if (string.IsNullOrWhiteSpace(rawFieldName))
        {
            return rawFieldName;
        }

        var value = DisplayPrefixRegex.Replace(rawFieldName, string.Empty);
        if (value.Length == 0)
        {
            return rawFieldName;
        }

        var first = value[0];

        return char.IsUpper(first)
            ? value
            : char.ToUpperInvariant(first) + value.Substring(1);
    }

    private static string GetInitialValueText(IFieldSymbol field)
    {
        foreach (var syntaxRef in field.DeclaringSyntaxReferences)
        {
            if (syntaxRef.GetSyntax() is VariableDeclaratorSyntax variable)
            {
                var initializer = variable.Initializer?.Value;
                if (initializer is null)
                {
                    continue;
                }

                if (initializer.IsKind(SyntaxKind.NullLiteralExpression))
                {
                    return "null";
                }

                var text = initializer.ToString();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }
        }

        return "(((default!)))";
    }

    private static string GetTypeArgsText(ITypeSymbol type)
    {
        if (type is not INamedTypeSymbol named || named.TypeArguments.Length == 0)
        {
            return string.Empty;
        }

        using var sb = new ValuePoolBuffer(capacity: 128);

        sb.Append('<');
        for (int i = 0; i < named.TypeArguments.Length; i++)
        {
            if (i > 0)
            {
                sb.Append(", ");
            }

            sb.Append(named.TypeArguments[i].ToNameString());
        }
        sb.Append('>');

        return sb.ToString();
    }

    private static string GetTypeConstraintsText(IFieldSymbol field)
    {
        if (field.Type is not INamedTypeSymbol namedType)
        {
            return string.Empty;
        }

        var definition = namedType.OriginalDefinition;
        if (definition.TypeParameters.Length == 0)
        {
            return string.Empty;
        }

        ValuePoolBuffer? scratch = null;
        try
        {
            foreach (var parameter in definition.TypeParameters)
            {
                bool hasConstraint = false;

                if (parameter.HasUnmanagedTypeConstraint)
                {
                    AppendConstraint(ref scratch, ref hasConstraint, parameter, "unmanaged");
                }
                else if (parameter.HasValueTypeConstraint)
                {
                    AppendConstraint(ref scratch, ref hasConstraint, parameter, "struct");
                }
                else if (parameter.HasReferenceTypeConstraint)
                {
                    var referenceConstraint = parameter.ReferenceTypeConstraintNullableAnnotation == NullableAnnotation.Annotated
                        ? "class?"
                        : "class";
                    AppendConstraint(ref scratch, ref hasConstraint, parameter, referenceConstraint);
                }
                else if (parameter.HasNotNullConstraint)
                {
                    AppendConstraint(ref scratch, ref hasConstraint, parameter, "notnull");
                }

                foreach (var constraintType in parameter.ConstraintTypes)
                {
                    AppendConstraint(ref scratch, ref hasConstraint, parameter, constraintType.ToNameString());
                }

                if (parameter.HasConstructorConstraint)
                {
                    AppendConstraint(ref scratch, ref hasConstraint, parameter, "new()");
                }
            }

            return scratch?.ToString() ?? string.Empty;
        }
        finally
        {
            scratch?.Dispose();
        }

        static void AppendConstraint(
            ref ValuePoolBuffer? bufferHolder,
            ref bool hasConstraint,
            ITypeParameterSymbol parameter,
            string text
        )
        {
            var work = bufferHolder ?? new ValuePoolBuffer(capacity: 128);

            if (!hasConstraint)
            {
                if (bufferHolder != null)
                {
                    work.Append(' ');
                }

                work.Append("where ");
                work.Append(parameter.Name);
                work.Append(" : ");
            }
            else
            {
                work.Append(", ");
            }

            work.Append(text);
            hasConstraint = true;

            bufferHolder = work;  // Write back updated struct to Nullable<T> here!
        }
    }

    private static bool HasMacroAttribute(IFieldSymbol field)
    {
        if (field.IsImplicitlyDeclared)
        {
            return false;
        }

        foreach (var attr in field.GetAttributes())
        {
            var attrName = attr.AttributeClass?.Name;
            if (attrName is "Macro" or "MacroAttribute")
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<(string template, MacroArgs args)> GetMacroAttributes(IFieldSymbol field)
    {
        foreach (var attr in field.GetAttributes())
        {
            var attrName = attr.AttributeClass?.Name;
            if (attrName is not "Macro" and not "MacroAttribute")
            {
                continue;
            }

            var template = attr.ConstructorArguments.Length > 0 && attr.ConstructorArguments[0].Value is string s
                ? s
                : string.Empty;

            var args = GetMacroArgs(attr);

            yield return (template, args);
        }
    }

    private static MacroArgs GetMacroArgs(AttributeData attr)
    {
        if (attr.ConstructorArguments.Length <= 1)
        {
            return new MacroArgs(isEmpty: true);
        }

        var result = new MacroArgs(isEmpty: false);

        for (int i = 1; i < attr.ConstructorArguments.Length; i++)
        {
            var arg = attr.ConstructorArguments[i];

            if (arg.Kind == TypedConstantKind.Array)
            {
                foreach (var item in arg.Values)
                {
                    if (item.Value is string s)
                    {
                        result.Add(s);
                    }
                }
            }
            else if (arg.Value is string s)
            {
                result.Add(s);
            }
        }

        return result;
    }

    /*
    > dotnet new console
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    */
    private const string DefaultUsings =
// Unity-safe Usings
@"#pragma warning disable CS0105  // Using directive appeared previously in this namespace
using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Net.Sockets;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
";


    private static void AppendWithReplaceMacroArgs(
        ref ValuePoolBuffer buffer,
        ReadOnlySpan<char> template,
        int tokenIndex,
        MacroArgs args
    )
    {
        int argLength = args.Count;
        do
        {
            if (template.Length < 2)
            {
                break;
            }

            buffer.Append(template.Slice(0, tokenIndex));

            int argIndex = template[tokenIndex + 1] - '0';

            var match = template.Slice(tokenIndex, 2);
            template = template.Slice(tokenIndex + 2);

            if (argIndex is < 0 or > MaxArgCount)  // Not '$[0-9]'
            {
                buffer.Append(match);
                continue;
            }
            else if (argIndex >= argLength)
            {
                continue;
            }

            buffer.Append(argIndex switch
            {
                0 => args[0],
                1 => args[1],
                2 => args[2],
                3 => args[3],
                4 => args[4],
                5 => args[5],
                6 => args[6],
                7 => args[7],
                8 => args[8],
                9 or _ => args[9],
            });
        }
        while ((tokenIndex = template.IndexOf('$')) >= 0);

        buffer.Append(template);
    }

    private static void AppendWithTokenReplacements(
        in ValuePoolBuffer template,
        ref ValuePoolBuffer output,
        IFieldSymbol field
    )
    {
        string? fieldName = null;
        string? typeName = null;
        string? typeShortName = null;
        string? typeBareName = null;
        string? containerType = null;
        string? displayName = null;
        string? typeArgs = null;
        string? typeConstraints = null;
        string? visibility = null;
        string? initialValue = null;
        const string inline = "[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]";
        const string noinline = "[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]";

        const int MinMacroWordTokenLength = 7;   // "$static"
        const int MaxMacroWordTokenLength = 16;  // "$typeConstraints"
        const string DisplayNameToken = "$displayName";
        const string FieldNameToken = "$fieldName";
        const string TypeNameToken = "$typeName";
        const string StaticToken = "$static";
        const string InlineToken = "$inline";
        const string TypeArgsToken = "$typeArgs";
        const string TypeConstraintsToken = "$typeConstraints";
        const string InitialValueToken = "$initialValue";
        const string VisibilityToken = "$visibility";
        const string TypeShortNameToken = "$typeShortName";
        const string TypeBareNameToken = "$typeBareName";
        const string ContainerTypeToken = "$containerType";
        const string NoInlineToken = "$noinline";

        var span = template.GetWrittenSpan();
        int spanLength = span.Length;

        int offset = 0;
        int found;
        while ((found = span.Slice(offset).IndexOf('$')) >= 0)
        {
            output.Append(span.Slice(offset, found));
            offset += found;

            int consumed = 0;

            if ((spanLength - offset) is >= MinMacroWordTokenLength &&
                span[offset + 1] is >= 'a' and <= 'z')
            {
                var tokenHead = span.Slice(offset);

                // Order is important: Commonly used tokens processed first
                if (tokenHead.StartsWith(DisplayNameToken.AsSpan(), StringComparison.Ordinal))
                {
                    displayName ??= ToDisplayName(field.Name);
                    output.Append(displayName);
                    consumed = DisplayNameToken.Length;
                }
                else if (tokenHead.StartsWith(FieldNameToken.AsSpan(), StringComparison.Ordinal))
                {
                    fieldName ??= field.Name;
                    output.Append(fieldName);
                    consumed = FieldNameToken.Length;
                }
                else if (tokenHead.StartsWith(TypeNameToken.AsSpan(), StringComparison.Ordinal))
                {
                    typeName ??= StripTrailingNullableMarker(field.Type.ToNameString());
                    output.Append(typeName);
                    consumed = TypeNameToken.Length;
                }
                else if (tokenHead.StartsWith(StaticToken.AsSpan(), StringComparison.Ordinal))
                {
                    if (field.IsStatic)
                    {
                        output.Append("static");
                    }
                    consumed = StaticToken.Length;
                }
                else if (tokenHead.StartsWith(TypeArgsToken.AsSpan(), StringComparison.Ordinal))
                {
                    typeArgs ??= GetTypeArgsText(field.Type);
                    output.Append(typeArgs);
                    consumed = TypeArgsToken.Length;
                }
                else if (tokenHead.StartsWith(InlineToken.AsSpan(), StringComparison.Ordinal))
                {
                    output.Append(inline);
                    consumed = InlineToken.Length;
                }
                else if (tokenHead.StartsWith(InitialValueToken.AsSpan(), StringComparison.Ordinal))
                {
                    initialValue ??= GetInitialValueText(field);
                    output.Append(initialValue);
                    consumed = InitialValueToken.Length;
                }
                else if (tokenHead.StartsWith(TypeConstraintsToken.AsSpan(), StringComparison.Ordinal))
                {
                    typeConstraints ??= GetTypeConstraintsText(field);
                    output.Append(typeConstraints);
                    consumed = TypeConstraintsToken.Length;
                }
                else if (tokenHead.StartsWith(VisibilityToken.AsSpan(), StringComparison.Ordinal))
                {
                    visibility ??= SyntaxFacts.GetText(field.DeclaredAccessibility);
                    output.Append(visibility);
                    consumed = VisibilityToken.Length;
                }
                else if (tokenHead.StartsWith(TypeShortNameToken.AsSpan(), StringComparison.Ordinal))
                {
                    typeShortName ??= StripTrailingNullableMarker(field.Type.ToNameString(localName: true));
                    output.Append(typeShortName);
                    consumed = TypeShortNameToken.Length;
                }
                else if (tokenHead.StartsWith(TypeBareNameToken.AsSpan(), StringComparison.Ordinal))
                {
                    typeBareName ??= field.Type.ToNameString(localName: true, noGeneric: true, noNullable: true);
                    output.Append(typeBareName);
                    consumed = TypeBareNameToken.Length;
                }
                else if (tokenHead.StartsWith(ContainerTypeToken.AsSpan(), StringComparison.Ordinal))
                {
                    containerType ??= field.ContainingType?.ToNameString() ?? string.Empty;
                    output.Append(containerType);
                    consumed = ContainerTypeToken.Length;
                }
                else if (tokenHead.StartsWith(NoInlineToken.AsSpan(), StringComparison.Ordinal))
                {
                    output.Append(noinline);
                    consumed = NoInlineToken.Length;
                }
            }

            if (consumed > 0)
            {
                offset += consumed;
            }
            else
            {
                // '$' may exist within string. Do not try removing this to quick fix compile errors.
                output.Append('$');
                offset++;
            }
        }

        output.Append(span.Slice(offset));
    }

    [StructLayout(LayoutKind.Auto)]
    struct ValuePoolBuffer : IDisposable
    {
        const int MinimumCapacity = 128;

        char[] buffer;
        int consumed;

        public ValuePoolBuffer(int capacity)
        {
            if (capacity < MinimumCapacity)
            {
                capacity = MinimumCapacity;
            }

            buffer = ArrayPool<char>.Shared.Rent(capacity);
            consumed = 0;
        }

        public void Dispose()
        {
            var r = buffer;
            buffer = (((null!)));

            ArrayPool<char>.Shared.Return(r, clearArray: false);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        readonly public override string ToString() => new string(buffer, 0, consumed);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        readonly public ReadOnlySpan<char> GetWrittenSpan() => buffer.AsSpan(0, consumed);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear() => consumed = 0;

        public void Append(string value) => Append(value.AsSpan());

        public void Append<T>(T value) where T : struct, IFormattable
        {
            var text = value.ToString(format: null, CultureInfo.InvariantCulture);
            Append(text.AsSpan());
        }

        public void Append(ReadOnlySpan<char> value)
        {
            var consumed = this.consumed;

            if (value.TryCopyTo(buffer.AsSpan(consumed)))
            {
                checked
                {
                    this.consumed = consumed + value.Length;
                }
            }
            else
            {
                ExpandAppend(value);
            }
        }

        public void Append(char value)
        {
            var buffer = this.buffer;
            var consumed = this.consumed;

            if (buffer.Length - consumed > 0)
            {
                buffer[consumed] = value;
                checked
                {
                    this.consumed = consumed + 1;
                }
            }
            else
            {
                ExpandAppend(stackalloc char[] { value });
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        void ExpandAppend(ReadOnlySpan<char> value)
        {
            var buffer = this.buffer;
            var consumed = this.consumed;

            int resultLength;
            int newCapacity;
            checked
            {
                resultLength = consumed + value.Length;
                newCapacity = buffer.Length;
                do
                {
                    newCapacity *= 2;
                }
                while (newCapacity < resultLength);
            }

            char[]? expanded = null;
            try
            {
                expanded = ArrayPool<char>.Shared.Rent(newCapacity);

                buffer.AsSpan(0, consumed).CopyTo(expanded);
                value.CopyTo(expanded.AsSpan(consumed));

                this.buffer = expanded;
                this.consumed = resultLength;
            }
            catch
            {
                if (expanded != null)
                {
                    ArrayPool<char>.Shared.Return(expanded, clearArray: false);
                }

                throw;
            }
            finally
            {
                ArrayPool<char>.Shared.Return(buffer, clearArray: false);
            }
        }
    }

    [StructLayout(LayoutKind.Auto)]
    struct MacroArgs
    {
        // NOTE: Overlapping reference type is NOT work as expected.
        object argsOrFirstItem;
        sbyte consumed;

        public MacroArgs(bool isEmpty)
        {
            argsOrFirstItem = string.Empty;
            consumed = (sbyte)(isEmpty ? -1 : 0);
        }

        public void Add(string value)
        {
            var consumed = this.consumed;
            if (consumed < MaxArgCount)
            {
                if (consumed == 0)
                {
                    this.argsOrFirstItem = value;
                }
                else
                {
                    var argsOrFirstItem = this.argsOrFirstItem;
                    if (argsOrFirstItem is not string[] args)
                    {
                        args = new string[MaxArgCount];
                        args[0] = (string)argsOrFirstItem;

                        this.argsOrFirstItem = args;
                    }

                    args[consumed] = value;
                }
            }

            this.consumed = (sbyte)(consumed + 1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        readonly public bool IsValid() => unchecked((byte)consumed <= MaxArgCount);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        readonly public bool IsEmpty() => consumed < 0;

        readonly public int Count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => consumed;
        }

        readonly public string this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => consumed > 1
                ? ((string[])argsOrFirstItem)[index]
                : (string)argsOrFirstItem;
        }
    }
}
