// Licensed under the Apache-2.0 License
// https://github.com/sator-imaging/FGenerator

#:sdk FGenerator.Sdk@2.4.1

using FGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

#pragma warning disable IDE0045  // Convert to conditional expression
#pragma warning disable CA1050   // Declare types in namespaces
#pragma warning disable CS1591   // Missing XML comment for publicly visible type or member
#pragma warning disable SMA0026  // Enum Obfuscation

/// <summary>
/// Generates obfuscated properties from a preceding multiline comment.
/// </summary>
[Generator]
public sealed class EnvObfuscator : FGeneratorBase
{
    protected override string DiagnosticCategory => nameof(EnvObfuscator);
    protected override string DiagnosticIdPrefix => "ENVOBF";
    protected override string? TargetAttributeName => "Obfuscate";

    protected override string? PostInitializationOutput =>
@"using System;

namespace EnvObfuscator
{
    /// <summary>
    /// Apply to a partial class or struct to generate obfuscated properties from the preceding <c>/* ... */</c>
    /// multiline <c>.env</c> format comment.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
    internal sealed class ObfuscateAttribute : Attribute
    {
        /// <inheritdoc cref=""ObfuscateAttribute""/>
        /// <param name=""seed"">Seed for deterministic obfuscation output; omit the parameter to use a random seed. 0 is allowed and deterministic.</param>
        public ObfuscateAttribute(int seed = 0)
        {
            // The seed value is read by the source generator from the attribute's syntax tree.
            // This parameter exists only to capture the value at compile time.
        }
    }
}
";

    protected override CodeGeneration? Generate(Target target, out AnalyzeResult? diagnostic)
    {
        diagnostic = null;

        try
        {
            if (target.RawSymbol is not INamedTypeSymbol typeSymbol)
            {
                diagnostic = new AnalyzeResult("001", "Invalid target", DiagnosticSeverity.Error, "Obfuscate can only be applied to named types.");
                return null;
            }

            if (typeSymbol.TypeKind is not TypeKind.Class and not TypeKind.Struct)
            {
                diagnostic = new AnalyzeResult("002", "Invalid target", DiagnosticSeverity.Error, "Obfuscate can only be applied to classes, structs, or records.");
                return null;
            }

            if (!target.IsPartial)
            {
                diagnostic = new AnalyzeResult("003", "Type must be partial", DiagnosticSeverity.Error, $"Type '{typeSymbol.Name}' must be declared as partial to use Obfuscate.");
                return null;
            }

            var attributeData = target.RawAttributes.FirstOrDefault();
            if (attributeData == null)
            {
                diagnostic = new AnalyzeResult("004", "Attribute missing", DiagnosticSeverity.Error, "Obfuscate attribute could not be resolved.");
                return null;
            }

            var attributeSyntax = attributeData.ApplicationSyntaxReference?.GetSyntax() as AttributeSyntax;
            if (attributeSyntax?.Parent is not AttributeListSyntax attributeList)
            {
                diagnostic = new AnalyzeResult("005", "Attribute syntax not found", DiagnosticSeverity.Error, "Unable to locate Obfuscate attribute syntax.");
                return null;
            }

            if (!TryGetEnvComment(attributeList, out var envText, out var commentStatus))
            {
                if (commentStatus == EnvCommentStatus.NotMultilineComment)
                {
                    diagnostic = new AnalyzeResult("010", "Invalid env comment", DiagnosticSeverity.Warning, "The trivia immediately preceding [Obfuscate] must be a /* ... */ multiline comment.");
                }
                else
                {
                    diagnostic = new AnalyzeResult("006", "Missing env comment", DiagnosticSeverity.Warning, "A /* ... */ multiline comment preceding [Obfuscate] is required.");
                }
                return null;
            }

            var entries = new List<EnvEntry>();
            var invalidLines = new List<string>();

            ParseEnv(envText, entries, invalidLines);

            AnalyzeResult? warning = null;
            if (entries.Count == 0)
            {
                warning = new AnalyzeResult("007", "No entries", DiagnosticSeverity.Warning, "No valid env entries were found in the preceding comment.");
            }
            else if (invalidLines.Count > 0)
            {
                var sample = invalidLines[0];
                warning = new AnalyzeResult("008", "Invalid env lines", DiagnosticSeverity.Warning, $"Ignored {invalidLines.Count} invalid env line(s). First: '{sample}'.");
            }

            bool hasExplicitSeed = attributeSyntax.ArgumentList?.Arguments.Count > 0;
            int seedValueFromAttribute = 0;

            // Determine if a seed was explicitly provided in the attribute's source code.
            // If the seed argument is omitted (e.g., `[Obfuscate()]`), a random seed will be generated.
            // If `[Obfuscate(0)]` is used, the seed will be explicitly set to 0.
            if (hasExplicitSeed && attributeData.ConstructorArguments.Length > 0)
            {
                var arg = attributeData.ConstructorArguments[0];
                if (!arg.IsNull && arg.Value is int seedValue)
                {
                    seedValueFromAttribute = seedValue;
                }
            }

            if (hasExplicitSeed)
            {
                foreach (var named in attributeData.NamedArguments)
                {
                    if (string.Equals(named.Key, "seed", StringComparison.OrdinalIgnoreCase) && named.Value.Value is int seedValue)
                    {
                        seedValueFromAttribute = seedValue;
                        break;
                    }
                }
            }

            if (hasExplicitSeed && seedValueFromAttribute == 0 && warning == null)
            {
                warning = new AnalyzeResult("009", "Seed is zero", DiagnosticSeverity.Warning, "Seed value 0 is valid but deterministic. To use a random seed, omit the seed argument.");
            }

            if (warning != null)
            {
                diagnostic = warning;
            }

            int seed = hasExplicitSeed
                ? seedValueFromAttribute
                : GenerateRandomSeed();
            int effectiveSeed = MixSeed(seed ^ StableHash(target.ToAssemblyUniqueIdentifier()));

            var source = GenerateSource(target, entries, effectiveSeed);
            return new CodeGeneration(target.ToHintName(), source);
        }
        catch (EnvKeyValidationException ex)
        {
            diagnostic = new AnalyzeResult("011", "Invalid env key", DiagnosticSeverity.Error, ex.Message);
            return null;
        }
        catch (ObfuscationKeyException ex)
        {
            diagnostic = new AnalyzeResult("012", "Invalid obfuscation key", DiagnosticSeverity.Error, ex.Message);
            return null;
        }
        catch (Exception ex)
        {
            diagnostic = new AnalyzeResult("013", "Unhandled generator error", DiagnosticSeverity.Error, ex.Message);
            return null;
        }
    }

    private static bool TryGetEnvComment(AttributeListSyntax attributeList, out string envText, out EnvCommentStatus status)
    {
        envText = string.Empty;
        status = EnvCommentStatus.Missing;

        var triviaList = attributeList.GetLeadingTrivia();
        for (int i = triviaList.Count - 1; i >= 0; i--)
        {
            var trivia = triviaList[i];
            if (trivia.IsKind(SyntaxKind.WhitespaceTrivia) || trivia.IsKind(SyntaxKind.EndOfLineTrivia))
            {
                continue;
            }

            if (trivia.IsKind(SyntaxKind.MultiLineCommentTrivia))
            {
                var raw = trivia.ToFullString();
                if (raw.StartsWith("/**", StringComparison.Ordinal))
                {
                    status = EnvCommentStatus.NotMultilineComment;
                    return false;
                }

                if (raw.StartsWith("/*", StringComparison.Ordinal) && raw.EndsWith("*/", StringComparison.Ordinal))
                {
                    envText = raw.Substring(2, raw.Length - 4);
                }
                else
                {
                    envText = raw;
                }
                status = EnvCommentStatus.Found;
                return true;
            }

            status = EnvCommentStatus.NotMultilineComment;
            return false;
        }

        return false;
    }

    private enum EnvCommentStatus
    {
        Found,
        Missing,
        NotMultilineComment,
    }

    private static void ParseEnv(string envText, List<EnvEntry> entries, List<string> invalidLines)
    {
        using var reader = new StringReader(envText);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }

            if (trimmed.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            int eqIndex = trimmed.IndexOf('=');
            if (eqIndex < 0)
            {
                invalidLines.Add(trimmed);
                continue;
            }

            var key = trimmed.Substring(0, eqIndex).Trim();
            if (key.Length == 0)
            {
                invalidLines.Add(trimmed);
                continue;
            }
            ValidateEnvKeyOrThrow(key);

            var value = trimmed.Substring(eqIndex + 1).Trim();
            entries.Add(new EnvEntry(key, value));
        }
    }

    private static string GenerateSource(Target target, List<EnvEntry> entries, int seed)
    {
        var baseChars = BuildBaseChars(entries);

        IRandomSource random = new SeededRandomSource(seed);

        // Ensure obfuscated names are produced immediately after random instantiation to avoid
        // accidental reuse of identical internal seeds across types (compile error as a result).
        var nameRandom = new SeededRandomSource(seed ^ unchecked(0x6D2B79F5));
        string oddKeyNamespace = CreateHexName(nameRandom);
        string evenKeyNamespace = CreateHexName(nameRandom);
        string ocNamespace = CreateHexName(nameRandom);
        string ecNamespace = CreateHexName(nameRandom);

        string oddKeyClass = CreateHexName(nameRandom);
        string evenKeyClass = CreateHexName(nameRandom);
        string ocClass = CreateHexName(nameRandom);
        string ecClass = CreateHexName(nameRandom);

        string oddKeyField = CreateHexName(nameRandom);
        string evenKeyField = CreateHexName(nameRandom);
        string ocField = CreateHexName(nameRandom);
        string ecField = CreateHexName(nameRandom);

        ushort oddKey = CreateRandomUShortNonZero(random);
        ushort evenKey;
        do
        {
            evenKey = CreateRandomUShortNonZero(random);
        }
        while ((evenKey & 0xFF00) == (oddKey & 0xFF00) || (evenKey & 0xFF) == (oddKey & 0xFF));

        var ocBytes = BuildByteTableFromBaseChars(baseChars, oddKey, random, out var ocByteSources);
        var ecBytes = BuildByteTableFromBaseChars(baseChars, evenKey, random, out var ecByteSources);
        var obChars = BuildObfuscatedChars(baseChars, oddKey, evenKey, ocBytes, ecBytes);

        var usedPropertyNames = new HashSet<string>(StringComparer.Ordinal);
        var sb = new StringBuilder(1024 + (entries.Count * 256));

        string oddKeyRef = BuildNamespacePrefix(oddKeyNamespace) + oddKeyClass + "." + oddKeyField;
        string evenKeyRef = BuildNamespacePrefix(evenKeyNamespace) + evenKeyClass + "." + evenKeyField;
        string ocRef = BuildNamespacePrefix(ocNamespace) + ocClass + "." + ocField;
        string ecRef = BuildNamespacePrefix(ecNamespace) + ecClass + "." + ecField;

        int actualSeed = random.Seed;
        sb.Append("// seed: ");
        AppendSeedBinary(sb, actualSeed);
        sb.Append(" (");
        sb.Append(actualSeed.ToString(CultureInfo.InvariantCulture));
        sb.AppendLine(")");
        sb.AppendLine();
        AppendKeyClass(sb, oddKeyNamespace, oddKeyClass, oddKeyField, oddKey);
        sb.AppendLine();
        AppendKeyClass(sb, evenKeyNamespace, evenKeyClass, evenKeyField, evenKey);
        sb.AppendLine();
        AppendByteArrayClass(sb, ocNamespace, ocClass, ocField, ocBytes, ocByteSources);
        sb.AppendLine();
        AppendByteArrayClass(sb, ecNamespace, ecClass, ecField, ecBytes, ecByteSources);

        const string PropertyDocComment =
@"        /// <summary>
        /// Returns a freshly decoded clone each time; call <c>Span.Clear()</c> on the returned buffer to zero the decoded code when done.
        /// </summary>";

        const string ValidateDocComment =
@"        /// <summary>
        /// This method compares the contents from two buffers for equality in a way that doesn't leak timing information, making it ideal for use within cryptographic routines.
        /// </summary>
        /// <remarks>
        /// This method will short-circuit and return false only if left and right have different lengths.
        /// Fixed-time behavior is guaranteed in all other cases, including when left and right reference the same address.
        /// </remarks>";

        var helperSb = new StringBuilder(1024 + (entries.Count * 256));
        var targetSb = new StringBuilder(1024 + (entries.Count * 256));
        var targetDecoySb = new StringBuilder(1024 + (entries.Count * 256));
        var loaderTypeName = target.RawSymbol.Name + "Loader";
        // The generated class is a top-level type, so its accessibility must be public or internal.
        var loaderVisibility = target.RawSymbol.DeclaredAccessibility == Accessibility.Public
            ? "public "
            : "internal ";

        var decoyNames = new HashSet<string>(StringComparer.Ordinal);

        var containingNamespace = target.RawSymbol.ContainingNamespace;
        bool hasNamespace = !containingNamespace.IsGlobalNamespace;

        targetSb.AppendLine();
        targetSb.AppendLine("// Loader type is intentionally declared outside of container types.");
        if (hasNamespace)
        {
            targetSb.Append("namespace ");
            targetSb.AppendLine(containingNamespace.ToDisplayString());
            targetSb.AppendLine("{");
        }
        targetSb.Append("    ");
        targetSb.Append(loaderVisibility);
        targetSb.Append("sealed class ");
        targetSb.AppendLine(loaderTypeName);
        targetSb.AppendLine("    {");

        targetDecoySb.AppendLine();
        targetDecoySb.AppendLine(target.ToNamespaceAndContainingTypeDeclarations());
        targetDecoySb.Append("    partial ");
        targetDecoySb.AppendLine(target.ToDeclarationString(modifiers: false));
        targetDecoySb.AppendLine("    {");

        foreach (var entry in entries)
        {
            if (!TryGetIdentifier(entry.Key, usedPropertyNames, out var propertyName))
            {
                continue;
            }

            string decoyPropertyName;
            do
            {
                decoyPropertyName = CreateHexName(nameRandom);
            }
            while (usedPropertyNames.Contains(decoyPropertyName) || !decoyNames.Add(decoyPropertyName));

            string decoyValidateName;
            do
            {
                decoyValidateName = CreateHexName(nameRandom);
            }
            while (usedPropertyNames.Contains(decoyValidateName) || !decoyNames.Add(decoyValidateName));

            // targetDecoySb.AppendLine(PropertyDocComment);
            targetDecoySb.AppendLine($"        public static global::System.Memory<char> {decoyPropertyName}");
            targetDecoySb.AppendLine("        {");
            targetDecoySb.AppendLine("            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
            targetDecoySb.AppendLine("            get => throw new global::System.Exception();");
            targetDecoySb.AppendLine("        }");
            targetDecoySb.AppendLine();
            // targetDecoySb.AppendLine(ValidateDocComment);
            targetDecoySb.AppendLine("        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
            targetDecoySb.AppendLine($"        public static bool {decoyValidateName}(global::System.ReadOnlySpan<char> value) => throw new global::System.Exception();");
            targetDecoySb.AppendLine();

            if (entry.Value.Length == 0)
            {
                targetSb.AppendLine(PropertyDocComment);
                targetSb.AppendLine($"        public static global::System.Memory<char> {propertyName}");
                targetSb.AppendLine("        {");
                targetSb.AppendLine("            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
                targetSb.AppendLine("            get => global::System.Memory<char>.Empty;");
                targetSb.AppendLine("        }");
                targetSb.AppendLine();
                targetSb.AppendLine(ValidateDocComment);
                targetSb.AppendLine("        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
                targetSb.AppendLine($"        public static bool Validate_{propertyName}(global::System.ReadOnlySpan<char> value) => value.Length == 0;");
                targetSb.AppendLine();
                continue;
            }

            int entryLength = entry.Value.Length;
            var entryIndices = new int[entryLength];
            for (int i = 0; i < entryLength; i++)
            {
                char c = entry.Value[i];
                int index = baseChars.IndexOf(c);
                entryIndices[i] = index;
            }

            const int flagsBitWindowRange = 6; // 8 bits in a byte - 3 flag bits + 1 = 6
            int decodeArg1FlagsBitWindow = nameRandom.NextInt(flagsBitWindowRange);
            bool decodeArg2LowerInLowBits = nameRandom.NextBool();
            bool decodeArg3OddInUpperHalf = nameRandom.NextBool();
            string decodeRef = AppendDecodeHelper(helperSb, nameRandom, decodeArg1FlagsBitWindow, decodeArg2LowerInLowBits, decodeArg3OddInUpperHalf);
            string helperGetNamespace = CreateHexName(nameRandom);
            string helperGetClass = CreateHexName(nameRandom);
            string helperGetMethod = CreateHexName(nameRandom);
            string helperValidateNamespace = CreateHexName(nameRandom);
            string helperValidateClass = CreateHexName(nameRandom);
            string helperValidateMethod = CreateHexName(nameRandom);
            string wrapperGetNamespace = CreateHexName(nameRandom);
            string wrapperGetClass = CreateHexName(nameRandom);
            string wrapperGetField = CreateHexName(nameRandom);
            string wrapperValidateNamespace = CreateHexName(nameRandom);
            string wrapperValidateClass = CreateHexName(nameRandom);
            string wrapperValidateField = CreateHexName(nameRandom);
            string getDelegateNamespace = CreateHexName(nameRandom);
            string getDelegateClass = CreateHexName(nameRandom);
            string getDelegateType = CreateHexName(nameRandom);
            string validateDelegateNamespace = CreateHexName(nameRandom);
            string validateDelegateClass = CreateHexName(nameRandom);
            string validateDelegateType = CreateHexName(nameRandom);

            helperSb.AppendLine();
            helperSb.Append("// ");
            helperSb.AppendLine(propertyName);
            helperSb.Append("namespace ");
            helperSb.Append(helperGetNamespace);
            helperSb.Append(" { sealed class ");
            helperSb.Append(helperGetClass);
            helperSb.AppendLine(" {");
            helperSb.AppendLine("    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]");
            helperSb.Append("    internal static global::System.Memory<char> ");
            helperSb.Append(helperGetMethod);
            helperSb.AppendLine("()");
            helperSb.AppendLine("    {");
            helperSb.AppendLine("        var ocb = (byte[])" + ocRef + ";");
            helperSb.AppendLine("        var ecb = (byte[])" + ecRef + ";");
            helperSb.AppendLine("        // Bounds-check elimination hint: pre-touch last index.");
            helperSb.Append("        _ = ocb[");
            helperSb.Append(ocBytes.Length - 1);
            helperSb.AppendLine("];");
            helperSb.Append("        _ = ecb[");
            helperSb.Append(ecBytes.Length - 1);
            helperSb.AppendLine("];");
            helperSb.AppendLine("        var ok = (ushort)" + oddKeyRef + ";");
            helperSb.AppendLine("        var ek = (ushort)" + evenKeyRef + ";");
            helperSb.AppendLine(decodeArg3OddInUpperHalf
                ? "        uint kk = unchecked((uint)((ok << 16) | ek));"
                : "        uint kk = unchecked((uint)((ek << 16) | ok));");
            helperSb.Append("        var d = ");
            helperSb.Append(decodeRef);
            helperSb.AppendLine(";");
            helperSb.AppendLine("        return new char[]");
            helperSb.AppendLine("        {");

            for (int i = 0; i < entryLength; i++)
            {
                char c = entry.Value[i];
                int index = entryIndices[i];

                var obChar = obChars[index];
                bool lowerIsEven = random.NextBool();
                bool upperIsEven = random.NextBool();
                bool lowerUseLast = random.NextBool();
                bool upperUseLast = random.NextBool();
                helperSb.Append("            /* ");
                helperSb.Append(FormatCharForComment(c));
                helperSb.Append(" */ ");
                int lowerIndex = lowerIsEven
                    ? (lowerUseLast ? obChar.EvenLowerLastIndex : obChar.EvenLowerFirstIndex)
                    : (lowerUseLast ? obChar.OddLowerLastIndex : obChar.OddLowerFirstIndex);
                int upperIndex = upperIsEven
                    ? (upperUseLast ? obChar.EvenUpperLastIndex : obChar.EvenUpperFirstIndex)
                    : (upperUseLast ? obChar.OddUpperLastIndex : obChar.OddUpperFirstIndex);
                helperSb.Append(BuildDecodeCallExpression(
                    "d",
                    random,
                    flagsBitWindow: decodeArg1FlagsBitWindow,
                    lowerInLowBits: decodeArg2LowerInLowBits,
                    ocLength: ocBytes.Length,
                    ecLength: ecBytes.Length,
                    isAscii: obChar.IsAscii,
                    lowerIsEven: lowerIsEven,
                    upperIsEven: upperIsEven,
                    lowerIndex: lowerIndex,
                    upperIndex: upperIndex));
                helperSb.Append(',');
                helperSb.AppendLine();
            }

            helperSb.AppendLine("        };");
            helperSb.AppendLine("    }");
            helperSb.AppendLine("}}");

            string helperGetRef = BuildNamespacePrefix(helperGetNamespace) + helperGetClass;
            string helperValidateRef = BuildNamespacePrefix(helperValidateNamespace) + helperValidateClass;

            helperSb.Append("namespace ");
            helperSb.Append(getDelegateNamespace);
            helperSb.Append(" { sealed class ");
            helperSb.Append(getDelegateClass);
            helperSb.AppendLine(" {");
            helperSb.Append("    internal delegate global::System.Memory<char> ");
            helperSb.Append(getDelegateType);
            helperSb.AppendLine("();");
            helperSb.AppendLine("}}");

            helperSb.Append("namespace ");
            helperSb.Append(wrapperGetNamespace);
            helperSb.Append(" { sealed class ");
            helperSb.Append(wrapperGetClass);
            helperSb.AppendLine(" {");
            helperSb.Append("    // Intentionally non-readonly to avoid aggressive inlining/const-prop assumptions.");
            helperSb.AppendLine();
            helperSb.Append("    internal static object ");
            helperSb.Append(wrapperGetField);
            helperSb.AppendLine(";");
            helperSb.Append("    static ");
            helperSb.Append(wrapperGetClass);
            helperSb.AppendLine("()");
            helperSb.AppendLine("    {");
            helperSb.Append("        ");
            helperSb.Append(wrapperGetField);
            helperSb.Append(" = (");
            helperSb.Append(BuildNamespacePrefix(getDelegateNamespace));
            helperSb.Append(getDelegateClass);
            helperSb.Append('.');
            helperSb.Append(getDelegateType);
            helperSb.Append(")(() => ");
            helperSb.Append(helperGetRef);
            helperSb.Append('.');
            helperSb.Append(helperGetMethod);
            helperSb.Append("())");
            helperSb.AppendLine(";");
            helperSb.AppendLine("    }");
            helperSb.AppendLine("}}");

            helperSb.Append("namespace ");
            helperSb.Append(helperValidateNamespace);
            helperSb.Append(" { sealed class ");
            helperSb.Append(helperValidateClass);
            helperSb.AppendLine(" {");
            helperSb.AppendLine("    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]");
            helperSb.Append("    internal static bool ");
            helperSb.Append(helperValidateMethod);
            helperSb.AppendLine("(global::System.ReadOnlySpan<char> value)");
            helperSb.AppendLine("    {");
            helperSb.AppendLine("        if (value.Length != " + entryLength + ")");
            helperSb.AppendLine("        {");
            helperSb.AppendLine("            return false;");
            helperSb.AppendLine("        }");
            helperSb.AppendLine("        var ocb = (byte[])" + ocRef + ";");
            helperSb.AppendLine("        var ecb = (byte[])" + ecRef + ";");
            helperSb.AppendLine("        // Bounds-check elimination hint: pre-touch last index.");
            helperSb.Append("        _ = ocb[");
            helperSb.Append(ocBytes.Length - 1);
            helperSb.AppendLine("];");
            helperSb.Append("        _ = ecb[");
            helperSb.Append(ecBytes.Length - 1);
            helperSb.AppendLine("];");
            helperSb.AppendLine("        var ok = (ushort)" + oddKeyRef + ";");
            helperSb.AppendLine("        var ek = (ushort)" + evenKeyRef + ";");
            helperSb.AppendLine(decodeArg3OddInUpperHalf
                ? "        uint kk = unchecked((uint)((ok << 16) | ek));"
                : "        uint kk = unchecked((uint)((ek << 16) | ok));");
            helperSb.Append("        var d = ");
            helperSb.Append(decodeRef);
            helperSb.AppendLine(";");
            helperSb.AppendLine("        int diff = 0;");
            for (int i = 0; i < entryLength; i++)
            {
                helperSb.Append("        /* ");
                helperSb.Append(FormatCharForComment(entry.Value[i]));
                helperSb.Append(" */ ");
                int index = entryIndices[i];
                var obChar = obChars[index];
                bool lowerIsEven = random.NextBool();
                bool upperIsEven = random.NextBool();
                bool lowerUseLast = random.NextBool();
                bool upperUseLast = random.NextBool();
                helperSb.Append("diff |= ");
                int lowerIndex = lowerIsEven
                    ? (lowerUseLast ? obChar.EvenLowerLastIndex : obChar.EvenLowerFirstIndex)
                    : (lowerUseLast ? obChar.OddLowerLastIndex : obChar.OddLowerFirstIndex);
                int upperIndex = upperIsEven
                    ? (upperUseLast ? obChar.EvenUpperLastIndex : obChar.EvenUpperFirstIndex)
                    : (upperUseLast ? obChar.OddUpperLastIndex : obChar.OddUpperFirstIndex);
                helperSb.Append(BuildDecodeCallExpression(
                    "d",
                    random,
                    flagsBitWindow: decodeArg1FlagsBitWindow,
                    lowerInLowBits: decodeArg2LowerInLowBits,
                    ocLength: ocBytes.Length,
                    ecLength: ecBytes.Length,
                    isAscii: obChar.IsAscii,
                    lowerIsEven: lowerIsEven,
                    upperIsEven: upperIsEven,
                    lowerIndex: lowerIndex,
                    upperIndex: upperIndex));
                helperSb.Append(" ^ value[");
                helperSb.Append(i);
                helperSb.AppendLine("];");
            }
            helperSb.AppendLine("        return diff == 0;");
            helperSb.AppendLine("    }");
            helperSb.AppendLine("}}");

            helperSb.Append("namespace ");
            helperSb.Append(validateDelegateNamespace);
            helperSb.Append(" { sealed class ");
            helperSb.Append(validateDelegateClass);
            helperSb.AppendLine(" {");
            // Parameter: the ReadOnlySpan<char> value to validate.
            helperSb.Append("    internal delegate bool ");
            helperSb.Append(validateDelegateType);
            helperSb.AppendLine("(global::System.ReadOnlySpan<char> value);");
            helperSb.AppendLine("}}");

            helperSb.Append("namespace ");
            helperSb.Append(wrapperValidateNamespace);
            helperSb.Append(" { sealed class ");
            helperSb.Append(wrapperValidateClass);
            helperSb.AppendLine(" {");
            helperSb.Append("    // Intentionally non-readonly to avoid aggressive inlining/const-prop assumptions.");
            helperSb.AppendLine();
            helperSb.Append("    internal static object ");
            helperSb.Append(wrapperValidateField);
            helperSb.AppendLine(";");
            helperSb.Append("    static ");
            helperSb.Append(wrapperValidateClass);
            helperSb.AppendLine("()");
            helperSb.AppendLine("    {");
            helperSb.Append("        ");
            helperSb.Append(wrapperValidateField);
            helperSb.Append(" = (");
            helperSb.Append(BuildNamespacePrefix(validateDelegateNamespace));
            helperSb.Append(validateDelegateClass);
            helperSb.Append('.');
            helperSb.Append(validateDelegateType);
            helperSb.Append(")((_) => ");
            helperSb.Append(helperValidateRef);
            helperSb.Append('.');
            helperSb.Append(helperValidateMethod);
            helperSb.Append("(_))");
            helperSb.AppendLine(";");
            helperSb.AppendLine("    }");
            helperSb.AppendLine("}}");

            string helperGetWrapperRef = BuildNamespacePrefix(wrapperGetNamespace) + wrapperGetClass;
            string helperValidateWrapperRef = BuildNamespacePrefix(wrapperValidateNamespace) + wrapperValidateClass;
            string helperGetDelegateRef = BuildNamespacePrefix(getDelegateNamespace) + getDelegateClass + "." + getDelegateType;
            string helperValidateDelegateRef = BuildNamespacePrefix(validateDelegateNamespace) + validateDelegateClass + "." + validateDelegateType;

            targetSb.AppendLine(PropertyDocComment);
            targetSb.AppendLine($"        public static global::System.Memory<char> {propertyName}");
            targetSb.AppendLine("        {");
            targetSb.AppendLine("            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
            targetSb.AppendLine($"            get => ({helperGetWrapperRef}.{wrapperGetField} as {helperGetDelegateRef})!.Invoke();");
            targetSb.AppendLine("        }");
            targetSb.AppendLine();
            targetSb.AppendLine(ValidateDocComment);
            targetSb.AppendLine("        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
            targetSb.AppendLine($"        public static bool Validate_{propertyName}(global::System.ReadOnlySpan<char> value) => ({helperValidateWrapperRef}.{wrapperValidateField} as {helperValidateDelegateRef})!.Invoke(value);");
            targetSb.AppendLine();
        }

        targetSb.AppendLine("    }");
        if (hasNamespace)
        {
            targetSb.AppendLine("}");
        }

        targetDecoySb.AppendLine("    }");
        targetDecoySb.AppendLine(target.ToNamespaceAndContainingTypeClosingBraces());

        sb.Append(helperSb);
        sb.Append(targetDecoySb);
        sb.Append(targetSb);

        return sb.ToString();
    }

    private static List<char> BuildBaseChars(List<EnvEntry> entries)
    {
        var list = new List<char>();
        var seen = new HashSet<char>();

        foreach (var entry in entries)
        {
            foreach (var c in entry.Value)
            {
                if (seen.Add(c))
                {
                    list.Add(c);
                }
            }
        }

        const string extra = @"ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/-_=:.?&#";
        foreach (var c in extra)
        {
            if (seen.Add(c))
            {
                list.Add(c);
            }
        }

        return list;
    }

    private static string BuildNamespacePrefix(string namespaceName)
    {
        return "global::" + namespaceName + ".";
    }

    private static string AppendDecodeHelper(StringBuilder sb, IRandomSource nameRandom, int flagsBitWindow, bool lowerInLowBits, bool oddInUpperHalf)
    {
        string decodeNamespace = CreateHexName(nameRandom);
        string decodeClass = CreateHexName(nameRandom);
        string decodeMethod = CreateHexName(nameRandom);
        string decodeArgFlags = CreateHexName(nameRandom);
        string decodeArgBytes = CreateHexName(nameRandom);
        string decodeArgKeys = CreateHexName(nameRandom);
        string decodeWrapperNamespace = CreateHexName(nameRandom);
        string decodeWrapperClass = CreateHexName(nameRandom);
        string decodeWrapperField = CreateHexName(nameRandom);
        string decodeDelegateNamespace = CreateHexName(nameRandom);
        string decodeDelegateClass = CreateHexName(nameRandom);
        string decodeDelegateType = CreateHexName(nameRandom);
        byte asciiMask = (byte)(1 << flagsBitWindow);
        byte lowerEvenMask = (byte)(1 << (flagsBitWindow + 1));
        byte upperEvenMask = (byte)(1 << (flagsBitWindow + 2));

        sb.AppendLine();
        sb.AppendLine("// Decode a single character using preselected flags and byte indices.");
        sb.Append("namespace ");
        sb.Append(decodeNamespace);
        sb.Append(" { sealed class ");
        sb.Append(decodeClass);
        sb.AppendLine(" {");
        sb.AppendLine("    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]");
        sb.Append("    internal static char ");
        sb.Append(decodeMethod);
        sb.Append("(byte ");
        sb.Append(decodeArgFlags);
        sb.Append(", ushort ");
        sb.Append(decodeArgBytes);
        sb.Append(", uint ");
        sb.Append(decodeArgKeys);
        sb.AppendLine(")");
        sb.AppendLine("    {");
        sb.Append("        var ok = (ushort)(");
        sb.Append(oddInUpperHalf
            ? decodeArgKeys + " >> 16"
            : decodeArgKeys + " & 0xFFFF");
        sb.Append(");");
        sb.AppendLine();
        sb.Append("        var ek = (ushort)(");
        sb.Append(oddInUpperHalf
            ? decodeArgKeys + " & 0xFFFF"
            : decodeArgKeys + " >> 16");
        sb.Append(");");
        sb.AppendLine();
        sb.Append("        var lb = (byte)(");
        sb.Append(lowerInLowBits
            ? decodeArgBytes + " & 0xFF"
            : decodeArgBytes + " >> 8");
        sb.Append(");");
        sb.AppendLine();
        sb.Append("        var ub = (byte)(");
        sb.Append(lowerInLowBits
            ? decodeArgBytes + " >> 8"
            : decodeArgBytes + " & 0xFF");
        sb.Append(");");
        sb.AppendLine();
        sb.Append("        var ");
        sb.Append(decodeArgFlags);
        sb.AppendLine($"LowerKey = ({decodeArgFlags} & 0x{lowerEvenMask:X2}) != 0 ? ek : ok;");
        sb.Append("        var ");
        sb.Append(decodeArgFlags);
        sb.AppendLine($"UpperKey = ({decodeArgFlags} & 0x{upperEvenMask:X2}) != 0 ? ek : ok;");
        sb.Append("        if ((");
        sb.Append(decodeArgFlags);
        sb.Append($" & 0x{asciiMask:X2}) != 0)");
        sb.AppendLine();
        sb.AppendLine("        {");
        sb.Append("            return (char)((ushort)(");
        sb.Append("lb");
        sb.Append(" ^ (");
        sb.Append(decodeArgFlags);
        sb.Append("LowerKey");
        sb.AppendLine(" & 0xFF)));");
        sb.AppendLine("        }");
        sb.Append("        return (char)((ushort)(((ushort)(");
        sb.Append("ub");
        sb.Append(" ^ (");
        sb.Append(decodeArgFlags);
        sb.Append("UpperKey");
        sb.Append(" >> 8)) << 8) | (ushort)(");
        sb.Append("lb");
        sb.Append(" ^ (");
        sb.Append(decodeArgFlags);
        sb.Append("LowerKey");
        sb.AppendLine(" & 0xFF))));");
        sb.AppendLine("    }");
        sb.AppendLine("}}");

        string decodeRef = BuildNamespacePrefix(decodeNamespace) + decodeClass + "." + decodeMethod;

        sb.Append("namespace ");
        sb.Append(decodeDelegateNamespace);
        sb.Append(" { sealed class ");
        sb.Append(decodeDelegateClass);
        sb.AppendLine(" {");
        // Parameters: flags, packed bytes, packed keys.
        sb.Append("    internal delegate char ");
        sb.Append(decodeDelegateType);
        sb.AppendLine("(byte value, ushort n, uint m);");
        sb.AppendLine("}}");

        sb.Append("namespace ");
        sb.Append(decodeWrapperNamespace);
        sb.Append(" { sealed class ");
        sb.Append(decodeWrapperClass);
        sb.AppendLine(" {");
        sb.Append("    // Intentionally non-readonly to avoid aggressive inlining/const-prop assumptions.");
        sb.AppendLine();
        sb.Append("    internal static object ");
        sb.Append(decodeWrapperField);
        sb.AppendLine(";");
        sb.Append("    static ");
        sb.Append(decodeWrapperClass);
        sb.AppendLine("()");
        sb.AppendLine("    {");
        sb.Append("        ");
        sb.Append(decodeWrapperField);
        sb.Append(" = (");
        sb.Append(BuildNamespacePrefix(decodeDelegateNamespace));
        sb.Append(decodeDelegateClass);
        sb.Append('.');
        sb.Append(decodeDelegateType);
        sb.Append(")((i, _, j) => ");
        sb.Append(decodeRef);
        sb.Append("(i, _, j))");
        sb.AppendLine(";");
        sb.AppendLine("    }");
        sb.AppendLine("}}");

        string decodeWrapperRef = BuildNamespacePrefix(decodeWrapperNamespace) + decodeWrapperClass;
        string decodeDelegateRef = BuildNamespacePrefix(decodeDelegateNamespace) + decodeDelegateClass + "." + decodeDelegateType;
        return "((" + decodeDelegateRef + ")" + decodeWrapperRef + "." + decodeWrapperField + ")";
    }

    private static string CreateHexName(IRandomSource random)
    {
        var buffer = new byte[16];
        for (int i = 0; i < buffer.Length; i++)
        {
            buffer[i] = (byte)random.NextInt(256);
        }

        var sb = new StringBuilder(32);
        for (int i = 0; i < buffer.Length; i++)
        {
            sb.Append(buffer[i].ToString("x2", CultureInfo.InvariantCulture));
        }
        // "x2" produces lowercase hex; keep names lowercase for identifiers.
        sb[0] = (char)('a' + random.NextInt(6));
        return sb.ToString();
    }

    private static void AppendKeyClass(StringBuilder sb, string namespaceName, string className, string fieldName, ushort key)
    {
        AppendNamespaceOpen(sb, namespaceName);
        sb.Append("sealed class ");
        sb.Append(className);
        sb.AppendLine(" {");
        sb.Append("    // Intentionally non-readonly to avoid aggressive inlining/const-prop assumptions.");
        sb.AppendLine();
        sb.Append("    internal static object ");
        sb.Append(fieldName);
        sb.Append(" = (ushort)0x");
        sb.Append(key.ToString("X4", CultureInfo.InvariantCulture));
        sb.Append(";  // ");
        AppendKeyByteBinary(sb, key);
        sb.AppendLine();
        sb.Append('}');
        AppendNamespaceClose(sb);
    }

    private static void AppendByteArrayClass(StringBuilder sb, string namespaceName, string className, string fieldName, byte[] values, ByteSource[] sources)
    {
        AppendNamespaceOpen(sb, namespaceName);
        sb.Append("sealed class ");
        sb.Append(className);
        sb.AppendLine(" {");
        sb.Append("    // Intentionally non-readonly to avoid aggressive inlining/const-prop assumptions.");
        sb.AppendLine();
        sb.Append("    internal static object ");
        sb.Append(fieldName);
        sb.Append(" = new byte[]");
        sb.Append("  // ");
        sb.Append(values.Length);
        sb.AppendLine(" items");
        sb.AppendLine("    {");
        AppendByteArray(sb, values, sources, 8);
        sb.AppendLine("    };");
        sb.Append('}');
        AppendNamespaceClose(sb);
    }

    private static void AppendNamespaceOpen(StringBuilder sb, string namespaceName)
    {
        sb.Append("namespace ");
        sb.Append(namespaceName);
        sb.Append(" { ");
    }

    private static void AppendNamespaceClose(StringBuilder sb)
    {
        sb.AppendLine("}");
    }

    private static ushort CreateRandomUShortNonZero(IRandomSource random)
    {
        while (true)
        {
            byte hi = (byte)random.NextInt(1, 0x100);
            byte lo = (byte)random.NextInt(1, 0x100);
            ushort key = (ushort)((hi << 8) | lo);
            key ^= unchecked((ushort)((key << 7) | (key >> 9)));
            if (HasValidHammingWeight((byte)(key & 0xFF))
             && HasValidHammingWeight((byte)(key >> 8)))
            {
                return key;
            }
        }
    }

    private static bool HasValidHammingWeight(byte value)
    {
        if (value is <= 0b_1111 and not 0xFF)
        {
            return false;
        }

        int count = 0;
        while (value != 0)
        {
            count++;
            value &= (byte)(value - 1);
        }

        return count is >= 3 and <= 5;
    }

    private static void Shuffle(List<ByteSource> list, IRandomSource random)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = random.NextInt(i + 1);
            var tmp = list[i];
            list[i] = list[j];
            list[j] = tmp;
        }
    }

    private static ObfuscatedChar[] BuildObfuscatedChars(List<char> baseChars, ushort oddKey, ushort evenKey, byte[] ocBytes, byte[] ecBytes)
    {
        var result = new ObfuscatedChar[baseChars.Count];
        for (int i = 0; i < baseChars.Count; i++)
        {
            result[i] = new ObfuscatedChar(baseChars[i], oddKey, evenKey, ocBytes, ecBytes);
        }
        return result;
    }

    private static byte[] BuildByteTableFromBaseChars(List<char> baseChars, ushort key, IRandomSource random, out ByteSource[] sources)
    {
        var distinct = new List<ByteSource>(baseChars.Count * 2);
        var seen = new HashSet<byte>();
        for (int i = 0; i < baseChars.Count; i++)
        {
            char original = baseChars[i];
            char encoded = (char)(original ^ key);
            byte lower = (byte)(encoded & 0xFF);
            byte upper = (byte)(encoded >> 8);

            if (seen.Add(lower))
            {
                distinct.Add(new ByteSource(lower, original, isUpper: false));
            }
            if (original >= 0x80 && seen.Add(upper))
            {
                distinct.Add(new ByteSource(upper, original, isUpper: true));
            }
        }

        int originalCount = distinct.Count;
        for (int i = 0; i < originalCount; i++)
        {
            distinct.Add(distinct[i]);
        }

        Shuffle(distinct, random);
        sources = distinct.ToArray();

        var bytes = new byte[sources.Length];
        for (int i = 0; i < sources.Length; i++)
        {
            bytes[i] = sources[i].Value;
        }
        return bytes;
    }

    private static int IndexOfByte(byte[] bytes, byte value)
    {
        for (int i = 0; i < bytes.Length; i++)
        {
            if (bytes[i] == value)
            {
                return i;
            }
        }
        return -1;
    }

    private static int LastIndexOfByte(byte[] bytes, byte value)
    {
        for (int i = bytes.Length - 1; i >= 0; i--)
        {
            if (bytes[i] == value)
            {
                return i;
            }
        }
        return -1;
    }


    private static void AppendByteArray(StringBuilder sb, byte[] values, ByteSource[] sources, int indent)
    {
        var indentText = new string(' ', indent);
        for (int i = 0; i < values.Length; i++)
        {
            sb.Append(indentText);
            sb.Append("0x");
            sb.Append(values[i].ToString("X2", CultureInfo.InvariantCulture));
            sb.Append(',');
            var source = sources[i];
            sb.Append("  // ");
            AppendByteBinary(sb, values[i]);
            sb.Append(" [");
            sb.Append(i);
            sb.Append("] ");
            sb.Append(source.IsUpper ? "upper" : "lower");
            sb.Append(" of '");
            sb.Append(FormatCharForComment(source.Original));
            sb.Append("' -> ");
            AppendEncodedByteComment(sb, values[i]);
            sb.AppendLine();
        }
    }

    private static string FormatCharForComment(char c)
    {
        if (c == '\r')
        {
            return "\\\\r";
        }
        if (c == '\n')
        {
            return "\\\\n";
        }
        if (c == '\t')
        {
            return "\\\\t";
        }
        if (char.IsControl(c) || char.IsSurrogate(c))
        {
            return "\\\\u" + ((int)c).ToString("X4", CultureInfo.InvariantCulture);
        }
        return c.ToString();
    }

    private static string BuildDecodeCallExpression(string decodeMethodRef, IRandomSource random, int flagsBitWindow, bool lowerInLowBits, int ocLength, int ecLength, bool isAscii, bool lowerIsEven, bool upperIsEven, int lowerIndex, int upperIndex)
    {
        string lowerBytesRef = lowerIsEven ? "ecb" : "ocb";
        string upperBytesRef = upperIsEven ? "ecb" : "ocb";
        string lowerByteExpr = lowerBytesRef + "[" + lowerIndex + "]";
        bool asciiNoiseUseEven = random.NextBool();
        string asciiNoiseBytesRef = asciiNoiseUseEven ? "ecb" : "ocb";
        int asciiNoiseIndex = asciiNoiseUseEven ? random.NextInt(ecLength) : random.NextInt(ocLength);
        string upperByteExpr = isAscii
            ? (asciiNoiseBytesRef + "[" + asciiNoiseIndex + "]")
            : (upperBytesRef + "[" + upperIndex + "]");
        byte asciiMask = (byte)(1 << flagsBitWindow);
        byte lowerEvenMask = (byte)(1 << (flagsBitWindow + 1));
        byte upperEvenMask = (byte)(1 << (flagsBitWindow + 2));
        byte baseFlags = (byte)((isAscii ? asciiMask : 0x0)
            | (lowerIsEven ? lowerEvenMask : 0x0)
            | (upperIsEven ? upperEvenMask : 0x0));
        byte usedMask = (byte)(0x07 << flagsBitWindow);
        // 1) fill all bits randomly, 2) overwrite only the semantic flag bits.
        byte flags = (byte)random.NextInt(256);
        flags = (byte)((flags & ~usedMask) | baseFlags);
        string bytesArg = lowerInLowBits
            ? "((ushort)(" + lowerByteExpr + " | (" + upperByteExpr + " << 8)))"
            : "((ushort)(" + upperByteExpr + " | (" + lowerByteExpr + " << 8)))";

        return decodeMethodRef
            + "("
            + "0x"
            + flags.ToString("X2", CultureInfo.InvariantCulture)
            + ", "
            + bytesArg
            + ", "
            + "kk"
            + ")";
    }

    private static void AppendEncodedByteComment(StringBuilder sb, byte value)
    {
        if (value is < 0x20 or >= 0x7F)  // 0x7F = DEL
        {
            sb.Append("unspeakable");
            return;
        }

        sb.Append('\'');
        sb.Append((char)value);
        sb.Append('\'');
    }

    private static void AppendByteBinary(StringBuilder sb, byte value)
    {
        sb.Append("0b_");
        for (int i = 7; i >= 0; i--)
        {
            sb.Append(((value >> i) & 1) == 0 ? '0' : '1');
        }
    }

    private static void AppendSeedBinary(StringBuilder sb, int seed)
    {
        uint value = unchecked((uint)seed);
        sb.Append("0b_");
        for (int i = 31; i >= 0; i--)
        {
            sb.Append(((value >> i) & 1u) == 0u ? '0' : '1');
            if (i != 0 && (i % 8) == 0)
            {
                sb.Append('_');
            }
        }
    }

    private static void AppendKeyByteBinary(StringBuilder sb, ushort key)
    {
        sb.Append("0b_");
        for (int i = 15; i >= 0; i--)
        {
            sb.Append(((key >> i) & 1) == 0 ? '0' : '1');
            if (i == 8)
            {
                sb.Append('_');
            }
        }
    }

    private static bool TryGetIdentifier(string key, HashSet<string> usedNames, out string identifier)
    {
        identifier = string.Empty;

        ValidateEnvKeyOrThrow(key);

        var builder = new StringBuilder(key.Length + 2);
        for (int i = 0; i < key.Length; i++)
        {
            var ch = key[i];
            if (i == 0)
            {
                builder.Append(ch);
            }
            else
            {
                builder.Append(ch);
            }
        }

        var candidate = builder.ToString();
        if (candidate.Length == 0)
        {
            return false;
        }

        if (!SyntaxFacts.IsValidIdentifier(candidate))
        {
            throw new EnvKeyValidationException($"Env key '{key}' is not a valid C# identifier.");
        }

        if (!usedNames.Add(candidate))
        {
            throw new EnvKeyValidationException($"Env key '{key}' duplicates another generated identifier '{candidate}'.");
        }

        identifier = candidate;
        return true;
    }

    private static void ValidateEnvKeyOrThrow(string key)
    {
        if (key.Length == 0)
        {
            throw new EnvKeyValidationException("Env key cannot be empty.");
        }

        for (int i = 0; i < key.Length; i++)
        {
            var ch = key[i];
            if (i == 0)
            {
                if (ch == '@' || !SyntaxFacts.IsIdentifierStartCharacter(ch))
                {
                    throw new EnvKeyValidationException($"Env key '{key}' contains an invalid identifier start character '{ch}'.");
                }
            }
            else
            {
                if (!SyntaxFacts.IsIdentifierPartCharacter(ch))
                {
                    throw new EnvKeyValidationException($"Env key '{key}' contains an invalid identifier character '{ch}'.");
                }
            }
        }

        if (!SyntaxFacts.IsValidIdentifier(key))
        {
            throw new EnvKeyValidationException($"Env key '{key}' is not a valid C# identifier.");
        }
    }

    private sealed class EnvKeyValidationException : InvalidOperationException
    {
        public EnvKeyValidationException(string message) : base(message)
        {
        }
    }

    private sealed class ObfuscationKeyException : InvalidOperationException
    {
        public ObfuscationKeyException(string message) : base(message)
        {
        }
    }

    private sealed class ObfuscatedChar
    {
        public bool IsAscii;
        public char Original;
        public int OddLowerFirstIndex;
        public int OddLowerLastIndex;
        public int OddUpperFirstIndex;
        public int OddUpperLastIndex;
        public int EvenLowerFirstIndex;
        public int EvenLowerLastIndex;
        public int EvenUpperFirstIndex;
        public int EvenUpperLastIndex;
        public byte[] OcBytes;
        public byte[] EcBytes;

        public ObfuscatedChar(char original, ushort oddKey, ushort evenKey, byte[] ocBytes, byte[] ecBytes)
        {
            Original = original;
            IsAscii = original < 0x80;
            OcBytes = ocBytes;
            EcBytes = ecBytes;

            byte oddLower = (byte)((original ^ oddKey) & 0xFF);
            byte oddUpper = (byte)(((ushort)(original ^ oddKey)) >> 8);
            byte evenLower = (byte)((original ^ evenKey) & 0xFF);
            byte evenUpper = (byte)(((ushort)(original ^ evenKey)) >> 8);

            OddLowerFirstIndex = IndexOfByte(ocBytes, oddLower);
            OddLowerLastIndex = LastIndexOfByte(ocBytes, oddLower);
            EvenLowerFirstIndex = IndexOfByte(ecBytes, evenLower);
            EvenLowerLastIndex = LastIndexOfByte(ecBytes, evenLower);

            if (IsAscii)
            {
                OddUpperFirstIndex = -1;
                OddUpperLastIndex = -1;
                EvenUpperFirstIndex = -1;
                EvenUpperLastIndex = -1;
            }
            else
            {
                OddUpperFirstIndex = IndexOfByte(ocBytes, oddUpper);
                OddUpperLastIndex = LastIndexOfByte(ocBytes, oddUpper);
                EvenUpperFirstIndex = IndexOfByte(ecBytes, evenUpper);
                EvenUpperLastIndex = LastIndexOfByte(ecBytes, evenUpper);
            }
        }
    }

    private readonly struct ByteSource
    {
        public ByteSource(byte value, char original, bool isUpper)
        {
            Value = value;
            Original = original;
            IsUpper = isUpper;
        }

        public byte Value { get; }
        public char Original { get; }
        public bool IsUpper { get; }
    }

    private readonly struct EnvEntry
    {
        public EnvEntry(string key, string value)
        {
            Key = key;
            Value = value;
        }

        public string Key { get; }
        public string Value { get; }
    }

    private interface IRandomSource
    {
        int NextInt(int maxExclusive);
        int NextInt(int minInclusive, int maxExclusive);
        bool NextBool();
        int Seed { get; }
    }

    private sealed class SeededRandomSource : IRandomSource
    {
        private readonly Random _random;

        public SeededRandomSource(int seed)
        {
            Seed = MixSeed(seed);
            _random = new Random(Seed);
            _random.Next(); // one spin-up
        }

        public int NextInt(int maxExclusive) => _random.Next(maxExclusive);
        public int NextInt(int minInclusive, int maxExclusive) => _random.Next(minInclusive, maxExclusive);
        public bool NextBool() => _random.Next(2) == 0;
        public int Seed { get; }
    }

    private static int GenerateRandomSeed()
    {
        return NextCryptoRangeInt32(1, int.MaxValue);
    }

    private static int NextCryptoRangeInt32(int minInclusive, int maxExclusive)
    {
        if (minInclusive >= maxExclusive)
        {
            throw new ArgumentOutOfRangeException(nameof(maxExclusive), "maxExclusive must be greater than minInclusive.");
        }

        uint range = (uint)(maxExclusive - minInclusive);
        uint limit = uint.MaxValue - (uint.MaxValue % range);
        uint value;

        using var rng = RandomNumberGenerator.Create();
        var buffer = new byte[4];
        do
        {
            rng.GetBytes(buffer);
            value = BitConverter.ToUInt32(buffer, 0);
        } while (value >= limit);

        return unchecked((int)(minInclusive + (value % range)));
    }

    private static int MixSeed(int seed)
    {
        unchecked
        {
            uint x = (uint)seed;
            x += 0x9E3779B9u;
            x ^= x >> 16;
            x *= 0x85EBCA6Bu;
            x ^= x >> 13;
            x *= 0xC2B2AE35u;
            x ^= x >> 16;
            return (int)x;
        }
    }

    private static int StableHash(string value)
    {
        unchecked
        {
            const uint fnvOffset = 2166136261;
            const uint fnvPrime = 16777619;
            uint hash = fnvOffset;
            for (int i = 0; i < value.Length; i++)
            {
                hash ^= value[i];
                hash *= fnvPrime;
            }
            return (int)hash;
        }
    }
}
