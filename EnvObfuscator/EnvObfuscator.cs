// Licensed under the Apache-2.0 License
// https://github.com/sator-imaging/FGenerator

#:project ../src
#:property TargetFramework=netstandard2.0
#:property IsRoslynComponent=true
#:property PublishAot=false
#:property LangVersion=latest
#:property OutputType=Library

using FGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

#pragma warning disable IDE0045  // Convert to conditional expression
#pragma warning disable CA1050   // Declare types in namespaces
#pragma warning disable CS1591   // Missing XML comment for publicly visible type or member
#pragma warning disable SMA0026  // Enum Obfuscation

/// <summary>
/// Generates obfuscated ReadOnlyMemory&lt;char&gt; properties from a preceding multiline comment.
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
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
    internal sealed class ObfuscateAttribute : Attribute
    {
        public ObfuscateAttribute(int seed = 0)
        {
            Seed = seed;
        }

        public int Seed { get; }
    }
}
";

    protected override CodeGeneration? Generate(Target target, out AnalyzeResult? diagnostic)
    {
        diagnostic = null;

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
        using var reader = new System.IO.StringReader(envText);
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

            var value = trimmed.Substring(eqIndex + 1).Trim();
            entries.Add(new EnvEntry(key, value));
        }
    }

    private static string GenerateSource(Target target, List<EnvEntry> entries, int seed)
    {
        var baseChars = BuildBaseChars(entries);

        IRandomSource random = new SeededRandomSource(seed);
        ushort oddKey = CreateRandomUShort(random);
        ushort evenKey = CreateRandomUShort(random);

        var doubled = new char[baseChars.Count * 2];
        for (int i = 0; i < baseChars.Count; i++)
        {
            doubled[i] = baseChars[i];
            doubled[i + baseChars.Count] = baseChars[i];
        }

        var oc = new ushort[doubled.Length];
        var ec = new ushort[doubled.Length];
        for (int i = 0; i < doubled.Length; i++)
        {
            oc[i] = (ushort)(doubled[i] ^ oddKey);
            ec[i] = (ushort)(doubled[i] ^ evenKey);
        }

        Shuffle(oc, random);
        Shuffle(ec, random);

        var usedPropertyNames = new HashSet<string>(StringComparer.Ordinal);
        var sb = new StringBuilder(1024 + (entries.Count * 256));

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

        string oddKeyRef = BuildNamespacePrefix(oddKeyNamespace) + oddKeyClass + "." + oddKeyField;
        string evenKeyRef = BuildNamespacePrefix(evenKeyNamespace) + evenKeyClass + "." + evenKeyField;
        string ocRef = BuildNamespacePrefix(ocNamespace) + ocClass + "." + ocField;
        string ecRef = BuildNamespacePrefix(ecNamespace) + ecClass + "." + ecField;

        sb.AppendLine("using System;");
        sb.AppendLine("using System.Runtime.CompilerServices;");
        sb.AppendLine();

        const string ValidateDocComment =
@"        /// <summary>
        /// This method compares the contents from two buffers for equality in a way that doesn't leak timing information, making it ideal for use within cryptographic routines.
        /// </summary>
        /// <remarks>
        /// This method will short-circuit and return false only if left and right have different lengths.
        /// Fixed-time behavior is guaranteed in all other cases, including when left and right reference the same address.
        /// </remarks>";

        AppendKeyClass(sb, oddKeyNamespace, oddKeyClass, oddKeyField, oddKey);
        sb.AppendLine();
        AppendKeyClass(sb, evenKeyNamespace, evenKeyClass, evenKeyField, evenKey);
        sb.AppendLine();
        AppendArrayClass(sb, ocNamespace, ocClass, ocField, oc, oddKey);
        sb.AppendLine();
        AppendArrayClass(sb, ecNamespace, ecClass, ecField, ec, evenKey);
        sb.AppendLine();

        sb.AppendLine(target.ToNamespaceAndContainingTypeDeclarations());
        sb.Append("    partial ");
        sb.AppendLine(target.ToDeclarationString(modifiers: false));
        sb.AppendLine("    {");

        foreach (var entry in entries)
        {
            if (!TryGetIdentifier(entry.Key, usedPropertyNames, out var propertyName))
            {
                continue;
            }

            if (entry.Value.Length == 0)
            {
                sb.AppendLine($"        public static ReadOnlyMemory<char> {propertyName}");
                sb.AppendLine("        {");
                sb.AppendLine("            [MethodImpl(MethodImplOptions.NoInlining)]");
                sb.AppendLine("            get => ReadOnlyMemory<char>.Empty;");
                sb.AppendLine("        }");
                sb.AppendLine();
                sb.AppendLine(ValidateDocComment);
                sb.AppendLine("        [MethodImpl(MethodImplOptions.NoInlining)]");
                sb.AppendLine($"        public static bool Validate_{propertyName}(ReadOnlySpan<char> value) => value.Length == 0;");
                sb.AppendLine();
                continue;
            }

            int entryLength = entry.Value.Length;
            var entryIndices = new int[entryLength];
            var entryIsEven = new bool[entryLength];
            for (int i = 0; i < entryLength; i++)
            {
                char c = entry.Value[i];
                bool isEven = random.NextBool(); // was: (i % 2) == 0
                ushort key = isEven ? evenKey : oddKey;
                ushort encoded = (ushort)(c ^ key);
                ushort[] table = isEven ? ec : oc;
                int index = random.NextBool() ? IndexOf(table, encoded) : LastIndexOf(table, encoded);

                entryIndices[i] = index;
                entryIsEven[i] = isEven;
            }

            sb.AppendLine($"        public static ReadOnlyMemory<char> {propertyName}");
            sb.AppendLine("        {");
            sb.AppendLine("            [MethodImpl(MethodImplOptions.NoInlining)]");
            sb.AppendLine("            get");
            sb.AppendLine("            {");
            sb.AppendLine("                var oc = " + ocRef + ";");
            sb.AppendLine("                var ec = " + ecRef + ";");
            sb.AppendLine("                var ok = " + oddKeyRef + ";");
            sb.AppendLine("                var ek = " + evenKeyRef + ";");
            sb.AppendLine("                return new char[]");
            sb.AppendLine("                {");

            for (int i = 0; i < entryLength; i++)
            {
                char c = entry.Value[i];
                bool isEven = entryIsEven[i];
                int index = entryIndices[i];

                sb.Append("                    /* ");
                sb.Append(FormatCharForComment(c));
                sb.Append(" */ ");
                if (isEven)
                {
                    sb.Append("(char)(");
                    sb.Append("ec");
                    sb.Append('[');
                }
                else
                {
                    sb.Append("(char)(");
                    sb.Append("oc");
                    sb.Append('[');
                }
                sb.Append(index);
                sb.Append("] ^ ");
                sb.Append(isEven ? "ek" : "ok");
                sb.Append("),");
                sb.AppendLine();
            }

            sb.AppendLine("                };");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine(ValidateDocComment);
            sb.AppendLine("        [MethodImpl(MethodImplOptions.NoInlining)]");
            sb.AppendLine($"        public static bool Validate_{propertyName}(ReadOnlySpan<char> value)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (value.Length != " + entryLength + ")");
            sb.AppendLine("            {");
            sb.AppendLine("                return false;");
            sb.AppendLine("            }");
            sb.AppendLine("            var oc = " + ocRef + ";");
            sb.AppendLine("            var ec = " + ecRef + ";");
            sb.AppendLine("            var ok = " + oddKeyRef + ";");
            sb.AppendLine("            var ek = " + evenKeyRef + ";");
            sb.AppendLine("            int diff = 0;");
            for (int i = 0; i < entryLength; i++)
            {
                sb.Append("            /* ");
                sb.Append(FormatCharForComment(entry.Value[i]));
                sb.Append(" */ ");
                if (entryIsEven[i])
                {
                    sb.Append("diff |= (char)(");
                    sb.Append("ec");
                    sb.Append('[');
                    sb.Append(entryIndices[i]);
                    sb.Append("] ^ ");
                    sb.Append("ek");
                    sb.Append(") ^ value[");
                    sb.Append(i);
                    sb.AppendLine("];");
                }
                else
                {
                    sb.Append("diff |= (char)(");
                    sb.Append("oc");
                    sb.Append('[');
                    sb.Append(entryIndices[i]);
                    sb.Append("] ^ ");
                    sb.Append("ok");
                    sb.Append(") ^ value[");
                    sb.Append(i);
                    sb.AppendLine("];");
                }
            }
            sb.AppendLine("            return diff == 0;");
            sb.AppendLine("        }");
            sb.AppendLine();
        }

        sb.AppendLine("    }");
        sb.AppendLine(target.ToNamespaceAndContainingTypeClosingBraces());

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

    private static string BuildNamespacePrefix(string? namespaceName)
    {
        if (string.IsNullOrWhiteSpace(namespaceName))
        {
            return string.Empty;
        }
        return "global::" + namespaceName + ".";
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

    private static void AppendKeyClass(StringBuilder sb, string? namespaceName, string className, string fieldName, ushort key)
    {
        AppendNamespaceOpen(sb, namespaceName);
        sb.Append("sealed class ");
        sb.Append(className);
        sb.AppendLine(" {");
        sb.Append("    internal static readonly ushort ");
        sb.Append(fieldName);
        sb.Append(" = 0x");
        sb.Append(key.ToString("X4", CultureInfo.InvariantCulture));
        sb.AppendLine(";");
        sb.Append('}');
        AppendNamespaceClose(sb, namespaceName);
    }

    private static void AppendArrayClass(StringBuilder sb, string? namespaceName, string className, string fieldName, ushort[] values, ushort key)
    {
        AppendNamespaceOpen(sb, namespaceName);
        sb.Append("sealed class ");
        sb.Append(className);
        sb.AppendLine(" {");
        sb.Append("    internal static readonly ushort[] ");
        sb.Append(fieldName);
        sb.Append(" = new ushort[]");
        sb.Append(" // ");
        sb.Append(values.Length);
        sb.AppendLine(" items");
        sb.AppendLine("    {");
        AppendCharArray(sb, values, key, 8);
        sb.AppendLine("    };");
        sb.Append('}');
        AppendNamespaceClose(sb, namespaceName);
    }

    private static void AppendNamespaceOpen(StringBuilder sb, string? namespaceName)
    {
        if (string.IsNullOrWhiteSpace(namespaceName))
        {
            return;
        }
        sb.Append("namespace ");
        sb.Append(namespaceName);
        sb.Append(" { ");
    }

    private static void AppendNamespaceClose(StringBuilder sb, string? namespaceName)
    {
        if (string.IsNullOrWhiteSpace(namespaceName))
        {
            return;
        }
        sb.AppendLine("}");
    }

    private static ushort CreateRandomUShort(IRandomSource random)
    {
        return (ushort)random.NextInt(1, 0x10000);
    }

    private static void Shuffle(ushort[] array, IRandomSource random)
    {
        for (int i = array.Length - 1; i > 0; i--)
        {
            int j = random.NextInt(i + 1);
            var tmp = array[i];
            array[i] = array[j];
            array[j] = tmp;
        }
    }

    private static int IndexOf(ushort[] array, ushort target)
    {
        for (int i = 0; i < array.Length; i++)
        {
            if (array[i] == target)
            {
                return i;
            }
        }
        return -1;
    }

    private static int LastIndexOf(ushort[] array, ushort target)
    {
        for (int i = array.Length - 1; i >= 0; i--)
        {
            if (array[i] == target)
            {
                return i;
            }
        }
        return -1;
    }

    private static void AppendCharArray(StringBuilder sb, ushort[] values, ushort key, int indent)
    {
        var indentText = new string(' ', indent);
        for (int i = 0; i < values.Length; i++)
        {
            ushort value = values[i];
            sb.Append(indentText);
            sb.Append("0x");
            sb.Append(((int)value).ToString("X4", CultureInfo.InvariantCulture));
            sb.Append(',');
            sb.Append(" // [");
            sb.Append(i);
            sb.Append("] ");
            sb.AppendLine(FormatCharForComment((char)(value ^ key)));
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

    private static bool TryGetIdentifier(string key, HashSet<string> usedNames, out string identifier)
    {
        identifier = string.Empty;

        var builder = new StringBuilder(key.Length + 2);
        for (int i = 0; i < key.Length; i++)
        {
            var ch = key[i];
            if (i == 0)
            {
                builder.Append(SyntaxFacts.IsIdentifierStartCharacter(ch) ? ch : '_');
            }
            else
            {
                builder.Append(SyntaxFacts.IsIdentifierPartCharacter(ch) ? ch : '_');
            }
        }

        var candidate = builder.ToString();
        if (candidate.Length == 0)
        {
            return false;
        }

        if (!SyntaxFacts.IsValidIdentifier(candidate))
        {
            return false;
        }

        if (!usedNames.Add(candidate))
        {
            int suffix = 1;
            string unique;
            do
            {
                unique = candidate + "_" + suffix;
                suffix++;
            } while (!usedNames.Add(unique));

            candidate = unique;
        }

        identifier = candidate;
        return true;
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
    }

    private sealed class SeededRandomSource : IRandomSource
    {
        private readonly Random _random;

        public SeededRandomSource(int seed)
        {
            _random = new Random(MixSeed(seed));
            _random.Next(); // one spin-up
        }

        public int NextInt(int maxExclusive) => _random.Next(maxExclusive);
        public int NextInt(int minInclusive, int maxExclusive) => _random.Next(minInclusive, maxExclusive);
        public bool NextBool() => _random.Next(2) == 0;
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

        return (int)(minInclusive + (value % range));
    }

    private static int MixSeed(int seed)
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
