// Licensed under the Apache-2.0 License
// https://github.com/sator-imaging/FGenerator

#:sdk FGenerator.Sdk@2.4.0

using FGenerator;
using Microsoft.CodeAnalysis;
using System.Globalization;
using System.Text;

[Generator]
public sealed class FinalEnumGenerator : FGeneratorBase
{
    protected override string DiagnosticCategory => "FinalEnumGenerator";
    protected override string DiagnosticIdPrefix => "FINALENUM";
    protected override string? TargetAttributeName => "FinalEnum";

    protected override string? PostInitializationOutput =>
@"using System;
using System.ComponentModel;

namespace FinalEnumGenerator
{
    /// <summary>
    /// Apply this attribute to an enum to generate high-performance ToStringFast,
    /// TryParse, and Utf8 methods in the static partial class <c>FinalEnums.&lt;TargetTypeName&gt;</c>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Enum, AllowMultiple = false, Inherited = false)]
    internal sealed class FinalEnumAttribute : Attribute { }
}

namespace FinalEnums
{
    /// <summary>
    /// Shared helpers used by FinalEnumGenerator-generated code.
    /// </summary>
    internal static class FinalEnumUtility
    {
        internal static T ThrowArgumentOutOfRange<T>(string paramName) => throw new ArgumentOutOfRangeException(paramName);
        internal static ReadOnlyMemory<T> ThrowArgumentOutOfRangeRoMemory<T>(string paramName) => throw new ArgumentOutOfRangeException(paramName);

        /// <summary>
        /// Shared helper used by generated code for boundary-aware token checks on strings.
        /// </summary>
        internal static bool ContainsToken(ReadOnlySpan<char> source, ReadOnlySpan<char> token, StringComparison comparison)
        {
            // Walk all occurrences to catch later matches that align with delimiters.
            int index = source.IndexOf(token, comparison);
            while (index >= 0)
            {
                int end = index + token.Length;
                
                bool startOk = index == 0 || source[index - 1] is <= ' ' or ',';    // space first: '..., <START>'
                bool endOk = end == source.Length || source[end] is ',' or <= ' ';  // comma first: '<END>, ...'
                if (startOk && endOk) return true;
                
                int next = source.Slice(end).IndexOf(token, comparison);
                index = next >= 0 ? end + next : -1;
            }
            return false;
        }

        /// <summary>
        /// Shared helper used by generated code for boundary-aware token checks on UTF-8 bytes.
        /// </summary>
        internal static bool ContainsToken(ReadOnlySpan<byte> source, ReadOnlySpan<byte> token)
        {
            // Walk all occurrences to catch later matches that align with delimiters.
            int index = source.IndexOf(token);
            while (index >= 0)
            {
                int end = index + token.Length;
                
                bool startOk = index == 0 || source[index - 1] is <= (byte)' ' or (byte)',';    // space first: '..., <START>'
                bool endOk = end == source.Length || source[end] is (byte)',' or <= (byte)' ';  // comma first: '<END>, ...'
                if (startOk && endOk) return true;
                
                int next = source.Slice(end).IndexOf(token);
                index = next >= 0 ? end + next : -1;
            }
            return false;
        }

        /// <summary>
        /// Blazing fast, boundary-aware token checks on UTF-8 bytes (ASCII-only case-insensitive).
        /// </summary>
        internal static bool ContainsTokenIgnoreCase(
            ReadOnlySpan<byte> source,
            ReadOnlySpan<byte> token)
        {
            int sourceLength = source.Length;
            int tokenLength = token.Length;
            if (tokenLength == 0 || tokenLength > sourceLength)
            {
                return false;
            }

            // '|' operator implicitly casts to 'int'
            const int ToLower = 0x20;  // transform by `c | 0x20`
            const uint AlphabetCount = 26;

            // NOTE: Non-ASCII bytes are guarded; `c | 0x20` may alter UTF-8 lead bytes.

            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            static byte to_lowercase(byte c) => unchecked((uint)(c - 'A')) < AlphabetCount ? (byte)(c | ToLower) : c;

            byte firstUpper = unchecked((uint)(token[0] - 'a')) < AlphabetCount ? (byte)(token[0] & ~ToLower) : token[0];
            byte firstLower = to_lowercase(token[0]);
            byte lastLower  = to_lowercase(token[^1]);

            int head = 0;
            do
            {
                int start = firstUpper == firstLower
                    ? source.Slice(head).IndexOf(firstLower)
                    : source.Slice(head).IndexOfAny(firstLower, firstUpper);

                if (start < 0)
                {
                    break;
                }

                start += head;
                head = start + 1;

                int end = start + tokenLength;
                if (end > sourceLength)
                {
                    break;
                }

                // word boundary
                if (start > 0 && source[start - 1] is not <= (byte)' ' and not (byte)',')  // space first: '..., <START>'
                {
                    continue;
                }
                if (end < sourceLength && source[end] is not (byte)',' and not <= (byte)' ')  // comma first: '<END>, ...'
                {
                    continue;
                }

                if (to_lowercase(source[end - 1]) != lastLower)
                {
                    continue;
                }

                if (tokenLength > 2)
                {
                    // perf: slicing to avoid bounds check and property access
                    var inbetween = source.Slice(start + 1, tokenLength - 2);

                    // perf: for the better JIT-ing
                    bool match = true;

                    for (int j = 0; j < inbetween.Length; j++)
                    {
                        if (to_lowercase(inbetween[j]) != to_lowercase(token[j + 1]))
                        {
                            match = false;
                            break;
                        }
                    }

                    if (!match) continue;
                }

                return true;
            }
            while (true);

            return false;
        }

        /// <summary>
        /// Trims leading/trailing spaces from a UTF-8 span without allocations.
        /// </summary>
        internal static ReadOnlySpan<byte> TrimWhiteSpaces(ReadOnlySpan<byte> source)
        {
            int start = 0;
            int end = source.Length;

            while (start < end && source[start] is <= (byte)' ') start++;  // <-- <= ' ' includes \t, \r, \n
            while (end > start && source[end - 1] is <= (byte)' ') end--;

            return source.Slice(start, end - start);
        }
    } 
}
";

    protected override CodeGeneration? Generate(Target target, out AnalyzeResult? diagnostic)
    {
        diagnostic = null;

        if (target.RawSymbol is not INamedTypeSymbol typeSymbol || typeSymbol.TypeKind != TypeKind.Enum)
        {
            diagnostic = new AnalyzeResult("001", "Invalid target", DiagnosticSeverity.Error,
                "[FinalEnum] can only be applied to enums.");
            return null;
        }

        var containingType = typeSymbol.ContainingType;
        while (containingType != null)
        {
            if (containingType.DeclaredAccessibility != Accessibility.Public)
            {
                diagnostic = new AnalyzeResult("002", "Containing type must be public", DiagnosticSeverity.Error,
                    $"The enum '{typeSymbol.Name}' is nested inside a non-public type '{containingType.Name}', which is not supported.");
                return null;
            }
            containingType = containingType.ContainingType;
        }

        var hasFlagsAttribute = typeSymbol.GetAttributes().Any(attr => attr.AttributeClass?.ToDisplayString() == "System.FlagsAttribute");
        var displayNames = new System.Collections.Generic.List<(IFieldSymbol Field, string DisplayName, string Normalized)>();

        var isUnsigned = IsUnsignedEnum(typeSymbol.EnumUnderlyingType);
        long flagsValueMask = 0;

        // Emit diagnostics eagerly so consumer code can evolve enum shape without reusing an incompatible display name.
        foreach (var field in typeSymbol.GetMembers().OfType<IFieldSymbol>().Where(f => f.IsConst))
        {
            var displayName = GetDisplayName(field);
            if (hasFlagsAttribute && !string.IsNullOrEmpty(displayName) && displayName.Contains(","))
            {
                diagnostic = new AnalyzeResult("003", "Display name is ambiguous for flags enums", DiagnosticSeverity.Error,
                    $"The display name '{displayName}' on member '{field.Name}' cannot contain ',' when the enum is marked with [Flags].");
                return null;
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                diagnostic = new AnalyzeResult("004", "Display name must not be empty", DiagnosticSeverity.Error,
                    $"The display name for member '{field.Name}' cannot be empty or whitespace.");
                return null;
            }

            if (!string.IsNullOrEmpty(displayName) && displayName.Length != displayName.Trim().Length)
            {
                diagnostic = new AnalyzeResult("005", "Display name must not start or end with whitespace", DiagnosticSeverity.Error,
                    $"The display name '{displayName}' on member '{field.Name}' cannot start or end with whitespace.");
                return null;
            }

            var normalizedDisplayName = displayName.Trim();
            displayNames.Add((field, displayName, normalizedDisplayName));

            flagsValueMask |= isUnsigned
                ? unchecked((long)Convert.ToUInt64(field.ConstantValue, CultureInfo.InvariantCulture))
                : Convert.ToInt64(field.ConstantValue, CultureInfo.InvariantCulture);
        }

        // // Emit diagnostics if all the bits are set
        // if (flagsValueMask == ~0)
        // {
        //     diagnostic = new AnalyzeResult("007", "Flags enum has all bits set", DiagnosticSeverity.Error,
        //         $"The enum '{typeSymbol.Name}' has all bits set, which is not supported due to ambiguous on IsDefined.");
        //     return null;
        // }

        // Emit diagnostics if token overlaps
        for (int i = 0; i < displayNames.Count; i++)
        {
            for (int j = i + 1; j < displayNames.Count; j++)
            {
                static bool HasBoundaryOverlap(string haystack, string needle)
                {
                    return haystack.IndexOf(" " + needle, StringComparison.OrdinalIgnoreCase) >= 0
                        || haystack.IndexOf(needle + " ", StringComparison.OrdinalIgnoreCase) >= 0;
                }

                var a = displayNames[i];
                var b = displayNames[j];
                if (string.Equals(a.Normalized, b.Normalized, StringComparison.OrdinalIgnoreCase)
                    || HasBoundaryOverlap(a.Normalized, b.Normalized)
                    || HasBoundaryOverlap(b.Normalized, a.Normalized))
                {
                    diagnostic = new AnalyzeResult("006", "Display name is ambiguous with another member", DiagnosticSeverity.Error,
                        $"The display name '{a.DisplayName}' on member '{a.Field.Name}' overlaps with '{b.DisplayName}' on member '{b.Field.Name}', which would make parsing ambiguous.");
                    return null;
                }
            }
        }

        var source = GenerateEnumMethods(target, typeSymbol);
        var hintName = target.ToHintName();

        return new CodeGeneration(hintName, source);
    }

    private static string GenerateEnumMethods(Target target, INamedTypeSymbol enumSymbol)
    {
        var isFlagsEnum = enumSymbol.GetAttributes().Any(attr => attr.AttributeClass?.ToDisplayString() == "System.FlagsAttribute");
        var underlyingType = enumSymbol.EnumUnderlyingType;
        var containingTypes = target.ContainingTypes;
        // The generated class is a top-level type, so its accessibility must be public or internal.
        var visibility = target.RawSymbol.DeclaredAccessibility == Accessibility.Public
            ? "public "
            : "internal ";

        var isUnsigned = IsUnsignedEnum(underlyingType);

#pragma warning disable IDE0072 // Add missing cases
        var underlyingTypeName = underlyingType?.SpecialType switch
        {
            SpecialType.System_SByte => "sbyte",
            SpecialType.System_Byte => "byte",
            SpecialType.System_Int16 => "short",
            SpecialType.System_UInt16 => "ushort",
            SpecialType.System_Int32 => "int",
            SpecialType.System_UInt32 => "uint",
            SpecialType.System_Int64 => "long",
            SpecialType.System_UInt64 => "ulong",
            _ => "UNKNOWN UNDERLYING VALUE TYPE",
        };
#pragma warning restore IDE0072

        var fullyQualifiedEnumName = enumSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        var members = enumSymbol.GetMembers()
            .OfType<IFieldSymbol>()
            .Where(f => f.IsConst)
            .Select(f =>
            {
                var displayName = GetDisplayName(f);
                var displayNameLiteral = displayName
                    .Replace("\\", "\\\\")
                    .Replace("\"", "\\\"");

                return new
                {
                    FieldName = f.Name,
                    DisplayName = displayName,
                    DisplayNameLiteral = displayNameLiteral,
                    Utf8Bytes = Encoding.UTF8.GetBytes(displayName),
                    ValueUnsigned = isUnsigned
                        ? Convert.ToUInt64(f.ConstantValue, CultureInfo.InvariantCulture)
                        : unchecked((ulong)Convert.ToInt64(f.ConstantValue, CultureInfo.InvariantCulture)),
                    ValueSigned = isUnsigned
                        ? unchecked((long)Convert.ToUInt64(f.ConstantValue, CultureInfo.InvariantCulture))
                        : Convert.ToInt64(f.ConstantValue, CultureInfo.InvariantCulture),
                };
            })
            .ToList();

        var minDisplayNameLength = members.Min(m => m.DisplayName.Length);
        var maxDisplayNameLength = members.Max(m => m.DisplayName.Length);
        var minUtf8Length = members.Min(m => m.Utf8Bytes.Length);
        var maxUtf8Length = members.Max(m => m.Utf8Bytes.Length);
        var finalValueTypeName = isUnsigned ? "ulong" : "long";
        var flagsValueMaskSigned = members.Aggregate(0L, (acc, m) => acc | m.ValueSigned);
        var flagsValueMaskUnsigned = members.Aggregate(0UL, (acc, m) => acc | m.ValueUnsigned);
        var hasZeroDefined = members.Any(x => x.ValueSigned == 0);
        var enumKindLabel = $"{(isFlagsEnum ? "Flags" : "Non-Flags")} ({underlyingTypeName}: {string.Join(", ", members.Select(x => isUnsigned ? $"{x.ValueUnsigned}" : $"{x.ValueSigned}"))})";

        var sb = new StringBuilder(capacity: 4096);

        sb.AppendLine("using System;");
        sb.AppendLine("using System.Text;");
        sb.AppendLine("using System.Buffers;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine();

        sb.Append($"namespace FinalEnums");

        if (!enumSymbol.ContainingNamespace.IsGlobalNamespace)
        {
            sb.AppendLine(" {");
            sb.Append($"namespace {enumSymbol.ContainingNamespace.ToDisplayString()}");
        }
        if (!containingTypes.IsEmpty)
        {
            foreach (var ct in containingTypes)
            {
                sb.AppendLine(" {");
                sb.Append($"namespace {ct.Name}");  // Extension method cannot be declared in nested class
            }
        }

        sb.AppendLine();
        sb.AppendLine("{");

        sb.AppendLine($"    {visibility}static partial class {enumSymbol.Name}");
        sb.AppendLine("    {");

        // UTF-8 byte arrays
        foreach (var member in members)
        {
            var byteString = string.Join(", ", member.Utf8Bytes.Select(b => $"0x{b:X2}"));
            sb.AppendLine($"        // \"{member.DisplayNameLiteral}\"");
            sb.AppendLine($"        private static readonly byte[] {member.FieldName}_utf8 = new byte[] {{ {byteString} }};");
        }
        sb.AppendLine();

        // GetNames, GetValues, GetNamesUtf8, IsDefined
        sb.AppendLine($"        // {enumKindLabel}");
        sb.AppendLine($"        public static string[] GetNames() => new string[] {{");
        for (int i = 0; i < members.Count; i++)
        {
            var member = members[i];
            var suffix = i == members.Count - 1 ? string.Empty : ",";
            sb.AppendLine($"            \"{member.DisplayNameLiteral}\"{suffix}");
        }
        sb.AppendLine("        };");
        sb.AppendLine();
        sb.AppendLine($"        // {enumKindLabel}");
        sb.AppendLine($"        public static {fullyQualifiedEnumName}[] GetValues() => new {fullyQualifiedEnumName}[] {{");
        for (int i = 0; i < members.Count; i++)
        {
            var member = members[i];
            var suffix = i == members.Count - 1 ? string.Empty : ",";
            sb.AppendLine($"            {fullyQualifiedEnumName}.{member.FieldName}{suffix}");
        }
        sb.AppendLine("        };");
        sb.AppendLine();
        sb.AppendLine($"        // {enumKindLabel}");
        sb.AppendLine($"        public static ReadOnlyMemory<byte>[] GetNamesUtf8() => new ReadOnlyMemory<byte>[] {{");
        for (int i = 0; i < members.Count; i++)
        {
            var member = members[i];
            var suffix = i == members.Count - 1 ? string.Empty : ",";
            sb.AppendLine($"            {member.FieldName}_utf8{suffix}");
        }
        sb.AppendLine("        };");
        sb.AppendLine();
        sb.AppendLine($"        // {enumKindLabel}");
        sb.AppendLine($"        public static bool IsDefined({finalValueTypeName} value)");
        sb.AppendLine("        {");
        {
            if (isFlagsEnum)
            {
                sb.AppendLine("            /*");
            }
            sb.AppendLine("            return value switch");
            sb.AppendLine("            {");
            foreach (var member in members)
            {
                sb.AppendLine($"                {(isUnsigned ? member.ValueUnsigned : member.ValueSigned)} => true,");
            }
            sb.AppendLine("                _ => false,");
            sb.AppendLine("            };");

            if (isFlagsEnum)
            {
                sb.AppendLine("            */");
                sb.AppendLine($"            return value == 0 ? {hasZeroDefined.ToString().ToLowerInvariant()} : (value & ~(({finalValueTypeName}){(isUnsigned ? flagsValueMaskUnsigned : flagsValueMaskSigned)})) == 0;");
            }
        }
        sb.AppendLine("        }");
        sb.AppendLine();

        // ToStringFast
        sb.AppendLine($"        // {enumKindLabel}");
        sb.AppendLine($"        public static string ToStringFast(this {fullyQualifiedEnumName} value, bool throwOnUnknown = false)");
        sb.AppendLine("        {");
        sb.AppendLine("            return value switch");
        sb.AppendLine("            {");
        foreach (var member in members)
        {
            sb.AppendLine($"                {fullyQualifiedEnumName}.{member.FieldName} => \"{member.DisplayNameLiteral}\",");
        }
        if (isFlagsEnum)
        {
            sb.AppendLine($"                _ => ToStringFastFlags(value, throwOnUnknown),");
        }
        else
        {
            sb.AppendLine("                _ => throwOnUnknown ? FinalEnumUtility.ThrowArgumentOutOfRange<string>(nameof(value)) : string.Empty,");
        }
        sb.AppendLine("            };");
        sb.AppendLine("        }");
        sb.AppendLine();

        // ToStringUtf8
        sb.AppendLine($"        // {enumKindLabel}");
        sb.AppendLine($"        public static ReadOnlyMemory<byte> ToStringUtf8(this {fullyQualifiedEnumName} value, bool throwOnUnknown = false)");
        sb.AppendLine("        {");
        sb.AppendLine("            return value switch");
        sb.AppendLine("            {");
        foreach (var member in members)
        {
            sb.AppendLine($"                // \"{member.DisplayNameLiteral}\"");
            sb.AppendLine($"                {fullyQualifiedEnumName}.{member.FieldName} => {member.FieldName}_utf8,");
        }
        if (isFlagsEnum)
        {
            sb.AppendLine($"                _ => ToStringUtf8Flags(value, throwOnUnknown),");
        }
        else
        {
            sb.AppendLine("                _ => throwOnUnknown ? FinalEnumUtility.ThrowArgumentOutOfRangeRoMemory<byte>(nameof(value)) : ReadOnlyMemory<byte>.Empty,");
        }
        sb.AppendLine("            };");
        sb.AppendLine("        }");
        sb.AppendLine();

        const int SwitchThreshold = 10;
        string SW = members.Count >= SwitchThreshold ? "    " : "    ////";

        // TryParse(string?)
        sb.AppendLine($"        // {enumKindLabel}");
        sb.AppendLine($"        public static bool TryParse(string? s, out {fullyQualifiedEnumName} result, StringComparison comparison = StringComparison.OrdinalIgnoreCase)");
        sb.AppendLine("        {");
        sb.AppendLine($"            if (s is not null && s.Length >= {minDisplayNameLength})");
        sb.AppendLine("            {");
        sb.AppendLine("                var span = s.AsSpan().Trim();");
        sb.AppendLine();
        sb.AppendLine($"                // won't gain the performance, kept for reference --> //if (s.Length <= {maxDisplayNameLength})");
        sb.AppendLine($"                {{");
        sb.AppendLine($"                {SW}switch (span.Length)");
        sb.AppendLine($"                {SW}{{");
        var stringGroups = members.GroupBy(m => m.DisplayName.Length).OrderBy(x => x.Key);
        foreach (var group in stringGroups)
        {
            sb.AppendLine($"                {SW}    case {group.Key}:");
            foreach (var member in group)
            {
                sb.AppendLine($"                            if (span.Equals(\"{member.DisplayNameLiteral}\", comparison)) {{ result = {fullyQualifiedEnumName}.{member.FieldName}; return true; }}");
            }
            sb.AppendLine($"                {SW}        break;");
        }
        sb.AppendLine($"                {SW}}}");
        if (isFlagsEnum)
        {
            sb.AppendLine();
            sb.AppendLine("                    return TryParseFlags(span, out result, comparison);");
        }
        sb.AppendLine($"                }}");
        sb.AppendLine("            }");
        sb.AppendLine("            result = default;");
        sb.AppendLine("            return false;");
        sb.AppendLine("        }");
        sb.AppendLine();

        // TryParse(ROSpan)
        sb.AppendLine($"        // {enumKindLabel}");
        sb.AppendLine($"        public static bool TryParse(ReadOnlySpan<byte> utf8, out {fullyQualifiedEnumName} result, bool ignoreWhiteSpace = false, bool ignoreCase = false)");
        sb.AppendLine("        {");
        sb.AppendLine($"            if (utf8.Length >= {minUtf8Length})");
        sb.AppendLine("            {");
        sb.AppendLine("                if (ignoreWhiteSpace) utf8 = FinalEnumUtility.TrimWhiteSpaces(utf8);");
        sb.AppendLine();
        sb.AppendLine($"                // won't gain the performance, kept for reference --> //if (utf8.Length <= {maxUtf8Length})");
        sb.AppendLine($"                {{");
        // NOTE: TryParse(utf8) on Flags enum with ignoreCase requires length check (Contains helper exists but no EqualsIgnoreCase).
        //       For simplicity, always use switch statement.
        sb.AppendLine($"                    switch (utf8.Length)");
        sb.AppendLine($"                    {{");
        var utf8Groups = members.GroupBy(m => m.Utf8Bytes.Length).OrderBy(x => x.Key);
        foreach (var group in utf8Groups)
        {
            sb.AppendLine($"                        case {group.Key}:");
            foreach (var member in group)
            {
                var tokenUtf8FieldName = $"{member.FieldName}_utf8";
                sb.AppendLine($"                            // \"{member.DisplayNameLiteral}\"");
                sb.AppendLine($"                            if (ignoreCase ? FinalEnumUtility.ContainsTokenIgnoreCase(utf8, {tokenUtf8FieldName}) : utf8.SequenceEqual({tokenUtf8FieldName})) {{ result = {fullyQualifiedEnumName}.{member.FieldName}; return true; }}");
            }
            sb.AppendLine($"                            break;");
        }
        sb.AppendLine($"                    }}");
        if (isFlagsEnum)
        {
            sb.AppendLine();
            sb.AppendLine("                    return TryParseFlags(utf8, out result, ignoreCase);");
        }
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine("            result = default;");
        sb.AppendLine("            return false;");
        sb.AppendLine("        }");
        sb.AppendLine();

        // Flags enum helpers
        if (isFlagsEnum)
        {
            // 'ToString' fallback method for Flags enums
            sb.AppendLine($"        // {enumKindLabel}");
            sb.AppendLine($"        private static string ToStringFastFlags({fullyQualifiedEnumName} value, bool throwOnUnknown)");
            sb.AppendLine("        {");
            sb.AppendLine($"            var numericValue = ({finalValueTypeName})value;");
            sb.AppendLine("            var remaining = numericValue;");
            sb.AppendLine($"            var sb = new StringBuilder(capacity: {maxDisplayNameLength} * 2);");
            sb.AppendLine("            var any = false;");
            sb.AppendLine();
            foreach (var member in members)
            {
                if (member.ValueSigned == 0)
                {
                    continue;
                }
                sb.AppendLine($"            if ((numericValue & ({finalValueTypeName}){(isUnsigned ? member.ValueUnsigned : member.ValueSigned)}) != 0)");
                sb.AppendLine("            {");
                sb.AppendLine("                if (any) sb.Append(\", \");");
                sb.AppendLine($"                sb.Append(\"{member.DisplayNameLiteral}\");");
                sb.AppendLine($"                remaining &= ~({finalValueTypeName}){(isUnsigned ? member.ValueUnsigned : member.ValueSigned)};");
                sb.AppendLine("                any = true;");
                sb.AppendLine("            }");
            }
            sb.AppendLine();
            sb.AppendLine("            if (any && remaining == 0) return sb.ToString();");
            sb.AppendLine("            return throwOnUnknown ? FinalEnumUtility.ThrowArgumentOutOfRange<string>(nameof(value)) : string.Empty;");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine($"        // {enumKindLabel}");
            sb.AppendLine($"        private static ReadOnlyMemory<byte> ToStringUtf8Flags({fullyQualifiedEnumName} value, bool throwOnUnknown)");
            sb.AppendLine("        {");
            sb.AppendLine($"            var numericValue = ({finalValueTypeName})value;");
            sb.AppendLine("            var remaining = numericValue;");
            sb.AppendLine($"            var bytes = new List<byte>(capacity: {maxUtf8Length} * 2);");
            sb.AppendLine("            var any = false;");
            sb.AppendLine();
            foreach (var member in members)
            {
                if (member.ValueSigned == 0)
                {
                    continue;
                }
                var tokenUtf8FieldName = $"{member.FieldName}_utf8";
                sb.AppendLine($"            if ((numericValue & ({finalValueTypeName}){(isUnsigned ? member.ValueUnsigned : member.ValueSigned)}) != 0)");
                sb.AppendLine("            {");
                sb.AppendLine("                if (any)");
                sb.AppendLine("                {");
                sb.AppendLine("                    bytes.Add((byte)',' );");
                sb.AppendLine("                    bytes.Add((byte)' ');");
                sb.AppendLine("                }");
                sb.AppendLine($"                bytes.AddRange({tokenUtf8FieldName});");
                sb.AppendLine($"                remaining &= ~({finalValueTypeName}){(isUnsigned ? member.ValueUnsigned : member.ValueSigned)};");
                sb.AppendLine("                any = true;");
                sb.AppendLine("            }");
            }
            sb.AppendLine();
            sb.AppendLine("            if (any && remaining == 0) return bytes.ToArray();");
            sb.AppendLine("            return throwOnUnknown ? FinalEnumUtility.ThrowArgumentOutOfRangeRoMemory<byte>(nameof(value)) : ReadOnlyMemory<byte>.Empty;");
            sb.AppendLine("        }");
            sb.AppendLine();

            // 'TryParse' fallback method for Flags enums
            sb.AppendLine();
            sb.AppendLine($"        // {enumKindLabel}");
            sb.AppendLine($"        private static bool TryParseFlags(ReadOnlySpan<char> s, out {fullyQualifiedEnumName} result, StringComparison comparison)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (s.IndexOf(\",\", StringComparison.Ordinal) >= 0)");
            sb.AppendLine("            {");
            sb.AppendLine($"                {finalValueTypeName} finalValue = 0;");
            sb.AppendLine($"                bool anyFound = false;");
            sb.AppendLine();
            foreach (var member in members)
            {
                if (member.ValueSigned == 0)
                {
                    continue;
                }
                sb.AppendLine($"                if (FinalEnumUtility.ContainsToken(s, \"{member.DisplayNameLiteral}\", comparison))");
                sb.AppendLine("                {");
                sb.AppendLine($"                    finalValue |= {(isUnsigned ? member.ValueUnsigned : member.ValueSigned)};");
                sb.AppendLine($"                    anyFound = true;");
                sb.AppendLine("                }");
            }
            sb.AppendLine();
            sb.AppendLine($"                if (anyFound)");
            sb.AppendLine("                {");
            sb.AppendLine($"                    result = ({fullyQualifiedEnumName})finalValue;");
            sb.AppendLine($"                    return true;");
            sb.AppendLine("                }");
            sb.AppendLine("            }");
            sb.AppendLine("            result = default;");
            sb.AppendLine("            return false;");
            sb.AppendLine("        }");
            sb.AppendLine();
            // TODO: TryParseFlags(utf8) can be optmized by separating IgnoreCase mode from shared method.
            //       - Current:   if (ignoreCase ? ContainsTokenIgnoreCase(...) : ContainsToken(...))
            //       - Optimized: if (ContainsToken[IgnoreCase](...))
            //       --> No huge impact on performance but code size will bloat in both this and target assembly.
            sb.AppendLine($"        // {enumKindLabel}");
            sb.AppendLine($"        private static bool TryParseFlags(ReadOnlySpan<byte> utf8, out {fullyQualifiedEnumName} result, bool ignoreCase)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (utf8.IndexOf((byte)',') >= 0)");
            sb.AppendLine("            {");
            sb.AppendLine($"                {finalValueTypeName} finalValue = 0;");
            sb.AppendLine($"                bool anyFound = false;");
            sb.AppendLine();
            foreach (var member in members)
            {
                if (member.ValueSigned == 0)
                {
                    continue;
                }
                var tokenUtf8FieldName = $"{member.FieldName}_utf8";
                sb.AppendLine($"                // \"{member.DisplayName}\"");
                sb.AppendLine($"                if (ignoreCase ? FinalEnumUtility.ContainsTokenIgnoreCase(utf8, {tokenUtf8FieldName}) : FinalEnumUtility.ContainsToken(utf8, {tokenUtf8FieldName}))");
                sb.AppendLine("                {");
                sb.AppendLine($"                    finalValue |= {(isUnsigned ? member.ValueUnsigned : member.ValueSigned)};");
                sb.AppendLine($"                    anyFound = true;");
                sb.AppendLine("                }");
            }
            sb.AppendLine();
            sb.AppendLine($"                if (anyFound)");
            sb.AppendLine("                {");
            sb.AppendLine($"                    result = ({fullyQualifiedEnumName})finalValue;");
            sb.AppendLine($"                    return true;");
            sb.AppendLine("                }");
            sb.AppendLine("            }");
            sb.AppendLine("            result = default;");
            sb.AppendLine("            return false;");
            sb.AppendLine("        }");
        }

        sb.AppendLine("    }");

        if (!containingTypes.IsEmpty)
        {
            foreach (var _ in containingTypes)
            {
                sb.AppendLine("}");
            }
        }
        if (!enumSymbol.ContainingNamespace.IsGlobalNamespace)
        {
            sb.AppendLine("}");
        }

        sb.AppendLine("}");

        return sb.ToString();
    }

    private static string GetDisplayName(IFieldSymbol field)
    {
        var inspectorAttr = field.GetAttributes().FirstOrDefault(attr => attr.AttributeClass?.Name is "InspectorNameAttribute");
        var categoryAttr = field.GetAttributes().FirstOrDefault(attr => attr.AttributeClass?.Name is "CategoryAttribute");

        var displayName = inspectorAttr?.ConstructorArguments.FirstOrDefault().Value as string
            ?? categoryAttr?.ConstructorArguments.FirstOrDefault().Value as string;

        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = field.Name;
        }

        return displayName!;
    }

    private static bool IsUnsignedEnum(INamedTypeSymbol? underlyingType)
    {
#pragma warning disable IDE0072 // Add missing cases
        return underlyingType?.SpecialType switch
        {
            SpecialType.System_Byte or
            SpecialType.System_UInt16 or
            SpecialType.System_UInt32 or
            SpecialType.System_UInt64 => true,
            _ => false
        };
#pragma warning restore IDE0072
    }
}
