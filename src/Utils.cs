// Licensed under the Apache-2.0 License
// https://github.com/sator-imaging/FGenerator

using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace FGenerator
{
    /// <summary>
    /// Helper extensions for rendering symbol names and composing generated source scaffolding.
    /// </summary>
    public static class Utils
    {
        /// <summary>
        /// Builds a generated file hint name for the target (including ".g.cs").
        /// </summary>
        public static string ToHintName(this Target target) => ToHintName(target.RawSymbol);

        /// <summary>
        /// Builds a generated file hint name (including ".g.cs") for the symbol.
        /// </summary>
        public static string ToHintName(this ISymbol symbol) => ToAssemblyUniqueIdentifier(symbol, separator: ".") + ".g.cs";


        const string DefaultSeparator = "_";

        /// <inheritdoc cref="ToAssemblyUniqueIdentifier(ISymbol, string)"/>
        public static string ToAssemblyUniqueIdentifier(this Target target, string separator = DefaultSeparator)
            => ToAssemblyUniqueIdentifier(target.RawSymbol, separator);

        /// <summary>
        /// Builds an identifier string that is intended to be unique within an assembly
        /// by including the symbol's containing namespace, types, and signature.
        /// Uses <paramref name="separator"/> to separate namespaces, types, and signature components.
        /// </summary>
        public static string ToAssemblyUniqueIdentifier(this ISymbol symbol, string separator = DefaultSeparator)
        {
            var sb = new StringBuilder(capacity: 128);

            if (symbol.ContainingNamespace != null && !symbol.ContainingNamespace.IsGlobalNamespace)
            {
                sb.Append(symbol.ContainingNamespace.ToDisplayString());
                sb.Append(separator);
            }

            foreach (var containing in GetContainingTypes(symbol, out _))
            {
                AppendNameWithGenericTypeParameterCount(sb, containing);
                sb.Append(separator);
            }

            if (GetExplicitInterfaceImplementationSymbol(symbol) is ISymbol iface)
            {
                AppendNameWithGenericTypeParameterCount(sb, iface);
                sb.Append(separator);
            }

            AppendNameWithGenericTypeParameterCount(sb, symbol);

            if (symbol is IPropertySymbol property &&
                property.IsIndexer)
            {
                for (int i = 0; i < property.Parameters.Length; i++)
                {
                    sb.Append(separator);
                    AppendNameWithGenericTypeParameterCount(sb, property.Parameters[i].Type);
                }
            }
            else if (symbol is IMethodSymbol method)
            {
                for (int i = 0; i < method.Parameters.Length; i++)
                {
                    sb.Append(separator);

                    var p = method.Parameters[i];
                    if (p.RefKind != RefKind.None)
                    {
                        sb.Append(p.RefKind.ToString().ToLowerInvariant());
                        sb.Append(separator);
                    }

                    AppendNameWithGenericTypeParameterCount(sb, p.Type);
                }
            }

            sb.Replace(".", separator);  // namespace
            sb.Replace("+", separator);  // nested type (maybe)

            return sb.ToString();
        }


        internal static ImmutableStack<INamedTypeSymbol> GetContainingTypes(ISymbol target, out int numberOfTypes)
        {
            var result = ImmutableStack<INamedTypeSymbol>.Empty;

            int count = 0;
            for (var current = target.ContainingType; current != null; current = current.ContainingType)
            {
                count++;
                result = result.Push(current);
            }

            numberOfTypes = count;
            return result;
        }

        private static void AppendNameWithGenericTypeParameterCount(StringBuilder sb, ISymbol symbol)
        {
            sb.Append(symbol.Name);

            int typeParamCount = 0;
            if (symbol is INamedTypeSymbol nts)
            {
                typeParamCount = nts.TypeParameters.Length;
            }
            else if (symbol is IMethodSymbol ms)
            {
                typeParamCount = ms.TypeParameters.Length;
            }

            if (typeParamCount > 0)
            {
                sb.Append('T');
                sb.Append(typeParamCount);
            }
        }


        const string DeclarationOpenBrace = " {";
        const int IndentSize = 0;
        const char IndentChar = ' ';

        /// <summary>
        /// Builds the namespace and containing type openings for the target (partial, nested/generic aware).
        /// </summary>
        public static string ToNamespaceAndContainingTypeDeclarations(this Target target, int indentSize = IndentSize, char indentChar = IndentChar)
            => ToNamespaceAndContainingTypeDeclarations(target.RawSymbol, indentSize, indentChar);

        /// <summary>
        /// Builds the namespace and containing type openings for the symbol (partial, nested/generic aware).
        /// </summary>
        public static string ToNamespaceAndContainingTypeDeclarations(this ISymbol symbol, int indentSize = IndentSize, char indentChar = IndentChar)
        {
            var sb = new StringBuilder(capacity: 256);

            if (symbol is not ITypeSymbol typeSymbol)
            {
                typeSymbol = symbol.ContainingType;
            }

            var hasNamespace = false;
            if (!typeSymbol.ContainingNamespace.IsGlobalNamespace)
            {
                sb.Append("namespace ");
                sb.Append(typeSymbol.ContainingNamespace.ToDisplayString());
                hasNamespace = true;
            }

            AppendContainingTypeDeclarations(sb, typeSymbol, hasNamespace, indentSize, indentChar);

            return sb.ToString();
        }


        /// <summary>
        /// Builds containing type openings for the target (partial, nested/generic aware).
        /// </summary>
        public static string ToContainingTypeDeclarations(this Target target, int indentSize = IndentSize, char indentChar = IndentChar)
            => ToContainingTypeDeclarations(target.RawSymbol, indentSize, indentChar);

        /// <summary>
        /// Builds containing type openings for the symbol (partial, nested/generic aware).
        /// </summary>
        public static string ToContainingTypeDeclarations(this ISymbol symbol, int indentSize = IndentSize, char indentChar = IndentChar)
        {
            var sb = new StringBuilder(capacity: 256);

            if (symbol is not ITypeSymbol typeSymbol)
            {
                typeSymbol = symbol.ContainingType;
            }

            AppendContainingTypeDeclarations(sb, typeSymbol, addNamespaceOpenBrace: false, indentSize, indentChar);

            return sb.ToString();
        }

        private static int AppendContainingTypeDeclarations(
            StringBuilder sb,
            ITypeSymbol typeSymbol,
            bool addNamespaceOpenBrace,
            int indentSize,
            char indentChar
        )
        {
            var containingTypes = GetContainingTypes(typeSymbol, out var numContainingTypes);

            int i = -1;
            foreach (var con in containingTypes)
            {
                i++;
                if (i != 0 || addNamespaceOpenBrace)
                {
                    sb.AppendLine(DeclarationOpenBrace);
                }

                if (indentSize > 0)
                {
                    sb.Append(indentChar, indentSize * (1 + i));
                }

                sb.Append("partial ");
                sb.Append(con.ToDeclarationString());
            }

            if (!containingTypes.IsEmpty || addNamespaceOpenBrace)
            {
                sb.AppendLine();
                sb.Append('{');
            }

            return numContainingTypes;
        }


        /// <summary>
        /// Builds closing braces for namespace/containing types opened by <see cref="ToNamespaceAndContainingTypeDeclarations(Target, int, char)"/>.
        /// </summary>
        public static string ToNamespaceAndContainingTypeClosingBraces(this Target target, int indentSize = IndentSize, char indentChar = IndentChar)
            => ToNamespaceAndContainingTypeClosingBraces(target.RawSymbol, indentSize, indentChar);

        /// <summary>
        /// Builds closing braces for namespace/containing types opened by <see cref="ToNamespaceAndContainingTypeDeclarations(ISymbol, int, char)"/>.
        /// </summary>
        public static string ToNamespaceAndContainingTypeClosingBraces(this ISymbol symbol, int indentSize = IndentSize, char indentChar = IndentChar)
        {
            var sb = new StringBuilder(capacity: 16);

            AppendContainingTypeClosingBraces(sb, symbol, !symbol.ContainingNamespace.IsGlobalNamespace, indentSize, indentChar);

            var result = sb.ToString();
            return result;
        }


        /// <summary>
        /// Builds closing braces for containing types opened by <see cref="ToContainingTypeDeclarations(Target, int, char)"/>.
        /// </summary>
        public static string ToContainingTypeClosingBraces(this Target target, int indentSize = IndentSize, char indentChar = IndentChar)
            => ToContainingTypeClosingBraces(target.RawSymbol, indentSize, indentChar);

        /// <summary>
        /// Builds closing braces for containing types opened by <see cref="ToContainingTypeDeclarations(ISymbol, int, char)"/>.
        /// </summary>
        public static string ToContainingTypeClosingBraces(this ISymbol symbol, int indentSize = IndentSize, char indentChar = IndentChar)
        {
            var sb = new StringBuilder(capacity: 16);

            AppendContainingTypeClosingBraces(sb, symbol, addNamespaceCloseBrace: false, indentSize, indentChar);

            var result = sb.ToString();
            return result;
        }

        private static int AppendContainingTypeClosingBraces(
            StringBuilder sb,
            ISymbol symbol,
            bool addNamespaceCloseBrace,
            int indentSize,
            char indentChar)
        {
            var containingTypes = GetContainingTypes(symbol, out var numContainingTypes);

            int indentLevel = numContainingTypes;

            int i = -1;
            foreach (var _ in containingTypes)
            {
                i++;
                if (i != 0)
                {
                    sb.AppendLine();
                }

                if (indentSize > 0)
                {
                    sb.Append(indentChar, indentSize * indentLevel);
                    indentLevel--;
                }

                sb.Append('}');
            }

            if (addNamespaceCloseBrace)
            {
                if (numContainingTypes != 0)
                {
                    sb.AppendLine();
                }
                sb.Append('}');
            }

            return numContainingTypes;
        }


        static readonly Dictionary<(bool, bool, bool), SymbolDisplayFormat> cache_ToNameStringFormat
            = new(capacity: 2 * 2 * 2);

        /// <summary>
        /// Renders a display name for the target with options for qualification, generics, and nullability.
        /// When fully qualified (default), includes the 'global::' prefix.
        /// </summary>
        public static string ToNameString(
            this Target target,
            bool localName = false,
            bool noGeneric = false,
            bool noNullable = false)
            => ToNameString(target.RawSymbol, localName, noGeneric, noNullable);

        /// <summary>
        /// Renders a display name for the symbol with options for qualification, generics, and nullability.
        /// When fully qualified (default), includes the 'global::' prefix.
        /// </summary>
        public static string ToNameString(
            this ISymbol symbol,
            bool localName = false,
            bool noGeneric = false,
            bool noNullable = false)
        {
            var key = (localName, noGeneric, noNullable);

            if (!cache_ToNameStringFormat.TryGetValue(key, out var format))
            {
                cache_ToNameStringFormat[key]
                = format
                = new(
                    globalNamespaceStyle:
                        localName
                            ? SymbolDisplayGlobalNamespaceStyle.Omitted
                            : SymbolDisplayGlobalNamespaceStyle.Included,
                    typeQualificationStyle:
                        localName
                            ? SymbolDisplayTypeQualificationStyle.NameOnly
                            : SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
                    genericsOptions:
                        noGeneric
                            ? SymbolDisplayGenericsOptions.None
                            : SymbolDisplayGenericsOptions.IncludeTypeParameters |
                              SymbolDisplayGenericsOptions.IncludeVariance,
                    memberOptions: SymbolDisplayMemberOptions.None,
                    delegateStyle: SymbolDisplayDelegateStyle.NameOnly,
                    extensionMethodStyle: SymbolDisplayExtensionMethodStyle.Default,
                    parameterOptions: SymbolDisplayParameterOptions.None,
                    propertyStyle: SymbolDisplayPropertyStyle.NameOnly,
                    localOptions: SymbolDisplayLocalOptions.None,
                    kindOptions: SymbolDisplayKindOptions.None,
                    miscellaneousOptions:
                        SymbolDisplayMiscellaneousOptions.UseSpecialTypes |
                        SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers |
                        SymbolDisplayMiscellaneousOptions.AllowDefaultLiteral
                        | (noNullable
                            ? SymbolDisplayMiscellaneousOptions.None
                            : //SymbolDisplayMiscellaneousOptions.IncludeNotNullableReferenceTypeModifier |
                              SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier
                        )
                );
            }

            var result = symbol.ToDisplayString(format);

            if (!localName)
            {
                var eii = GetExplicitInterfaceImplementationSymbol(symbol);
                if (eii != null)
                {
                    // DO NOT forward arguments!!
                    var prefix = eii.ToNameString(localName: false, noGeneric: false, noNullable: false);

                    result = $"{prefix}.{result}";
                }
            }

            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ISymbol? GetExplicitInterfaceImplementationSymbol(ISymbol symbol)
        {
            if (symbol is IMethodSymbol ms && !ms.ExplicitInterfaceImplementations.IsEmpty)
            {
                return ms.ExplicitInterfaceImplementations.FirstOrDefault()?.ContainingType;
            }
            else if (symbol is IPropertySymbol ps && !ps.ExplicitInterfaceImplementations.IsEmpty)
            {
                return ps.ExplicitInterfaceImplementations.FirstOrDefault()?.ContainingType;
            }
            else if (symbol is IEventSymbol es && !es.ExplicitInterfaceImplementations.IsEmpty)
            {
                return es.ExplicitInterfaceImplementations.FirstOrDefault()?.ContainingType;
            }
            else
            {
                return null;
            }
        }


        /// <summary>
        /// Renders a declaration-style string for the target (optionally including modifiers and generic constraints).
        /// </summary>
        public static string ToDeclarationString(
            this Target target,
            bool modifiers = false,
            bool genericConstraints = false)
            => ToDeclarationString(target.RawSymbol, modifiers, genericConstraints);

        /// <summary>
        /// Renders a declaration-style string for the symbol (optionally including modifiers and generic constraints).
        /// </summary>
        public static string ToDeclarationString(
            this ISymbol symbol,
            bool modifiers = false,
            bool genericConstraints = false)
        {
            // NOTE: if enabled, 'private' modifier will be emitted.
            if (GetExplicitInterfaceImplementationSymbol(symbol) != null)
            {
                modifiers = false;
            }

            var format = new SymbolDisplayFormat(
                globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
                typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameOnly,
                genericsOptions:
                    SymbolDisplayGenericsOptions.IncludeTypeParameters |
                    SymbolDisplayGenericsOptions.IncludeVariance
                    | (!genericConstraints
                        ? SymbolDisplayGenericsOptions.None
                        : SymbolDisplayGenericsOptions.IncludeTypeConstraints
                    ),
                memberOptions:
                    SymbolDisplayMemberOptions.IncludeConstantValue |
                    SymbolDisplayMemberOptions.IncludeExplicitInterface |
                    SymbolDisplayMemberOptions.IncludeParameters |
                    SymbolDisplayMemberOptions.IncludeRef |
                    SymbolDisplayMemberOptions.IncludeType
                    | (!modifiers
                        ? SymbolDisplayMemberOptions.None
                        : SymbolDisplayMemberOptions.IncludeAccessibility |
                          SymbolDisplayMemberOptions.IncludeModifiers
                    ),
                delegateStyle: SymbolDisplayDelegateStyle.NameAndSignature,
                extensionMethodStyle: SymbolDisplayExtensionMethodStyle.InstanceMethod,
                parameterOptions:
                    SymbolDisplayParameterOptions.IncludeDefaultValue |
                    SymbolDisplayParameterOptions.IncludeExtensionThis |
                    SymbolDisplayParameterOptions.IncludeName |
                    SymbolDisplayParameterOptions.IncludeOptionalBrackets |
                    SymbolDisplayParameterOptions.IncludeParamsRefOut |
                    SymbolDisplayParameterOptions.IncludeType,
                propertyStyle: SymbolDisplayPropertyStyle.ShowReadWriteDescriptor,
                localOptions:
                    SymbolDisplayLocalOptions.IncludeConstantValue |
                    SymbolDisplayLocalOptions.IncludeRef |
                    SymbolDisplayLocalOptions.IncludeType,
                kindOptions:
                    SymbolDisplayKindOptions.IncludeMemberKeyword |
                    SymbolDisplayKindOptions.IncludeNamespaceKeyword |
                    SymbolDisplayKindOptions.IncludeTypeKeyword,
                miscellaneousOptions:
                    SymbolDisplayMiscellaneousOptions.UseSpecialTypes |
                    SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers |
                    SymbolDisplayMiscellaneousOptions.AllowDefaultLiteral |
                    //SymbolDisplayMiscellaneousOptions.IncludeNotNullableReferenceTypeModifier |
                    SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier
            );

            var result = symbol.ToDisplayString(format);

            // TODO: support 'async' by SymbolDisplayFormat.
            // NOTE: always include 'async' keyword as it changes actual method implementation.
            if (//modifiers &&
                symbol is IMethodSymbol method && method.IsAsync)
            {
                result = "async " + result;
            }

            if (modifiers &&
                symbol is ITypeSymbol typeSymbol)
            {
                result = GetTypeModifiers(typeSymbol) + result;
            }

            // NOTE: keyword order 'partial (readonly|ref) struct' is invalid.
            if (!modifiers)
            {
                result = result.Replace("readonly ", string.Empty)
                               .Replace("ref ", string.Empty);
            }

            return result;
        }

        private static string GetTypeModifiers(ITypeSymbol symbol)
        {
            var sb = new StringBuilder(capacity: 64);

            sb.Append(symbol.ToVisibilityString());

            // ===== Modifiers =====
            // abstract (class/interface)
            if (symbol.IsAbstract &&
                symbol.TypeKind != TypeKind.Interface) // interfaces implied
            {
                sb.Append("abstract ");
            }

            // sealed
            if (symbol.IsSealed &&
                !symbol.IsValueType &&
                symbol.TypeKind != TypeKind.Enum && // enums are implicitly sealed
                symbol.TypeKind != TypeKind.Delegate)
            {
                sb.Append("sealed ");
            }

            // static
            if (symbol.IsStatic)
            {
                sb.Append("static ");
            }

            return sb.ToString();
        }


        /// <summary>
        /// Converts the target's declared accessibility into a keyword string that includes a trailing space (e.g., "public ").
        /// </summary>
        public static string ToVisibilityString(this Target target) => ToVisibilityString(target.RawSymbol);

        /// <summary>
        /// Converts the symbol's declared accessibility into a keyword string that includes a trailing space (e.g., "public ").
        /// </summary>
        public static string ToVisibilityString(this ISymbol symbol)
        {
            return symbol.DeclaredAccessibility switch
            {
                Accessibility.Public => "public ",
                Accessibility.Private => "private ",
                Accessibility.Internal => "internal ",
                Accessibility.Protected => "protected ",
                Accessibility.ProtectedOrInternal => "protected internal ",
                Accessibility.ProtectedAndInternal => "private protected ",
                Accessibility.NotApplicable or _ => string.Empty,
            };
        }
    }
}
