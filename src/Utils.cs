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
        public static string ToHintName(this Target target) => target.RawSymbol.ToHintName();

        /// <summary>
        /// Builds a generated file hint name without extension for the target.
        /// </summary>
        public static string ToHintNameWithoutExtension(this Target target) => target.RawSymbol.ToHintNameWithoutExtension();

        /// <summary>
        /// Builds a generated file hint name (including ".g.cs") for the symbol.
        /// </summary>
        public static string ToHintName(this ISymbol symbol) => symbol.ToHintNameWithoutExtension() + ".g.cs";

        /// <summary>
        /// Builds a generated file hint name without extension for the symbol.
        /// </summary>
        public static string ToHintNameWithoutExtension(this ISymbol symbol)
        {
            var sb = new StringBuilder(capacity: 64);

            if (symbol.ContainingNamespace != null && !symbol.ContainingNamespace.IsGlobalNamespace)
            {
                sb.Append(symbol.ContainingNamespace.ToDisplayString());
                sb.Append(".");
            }

            foreach (var containing in GetContainingTypes(symbol))
            {
                AppendNameWithGeneric(sb, containing);
                sb.Append(".");
            }

            AppendNameWithGeneric(sb, symbol);

            return sb.ToString();
        }

        private static ImmutableStack<ITypeSymbol> GetContainingTypes(ISymbol target)
        {
            var result = ImmutableStack<ITypeSymbol>.Empty;

            if (target is ITypeSymbol containing)
            {
                while ((containing = containing.ContainingType) != null)
                {
                    result = result.Push(containing);
                }
            }

            return result;
        }

        private static void AppendNameWithGeneric(StringBuilder sb, ISymbol symbol)
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
                sb.Append("T");
                sb.Append(typeParamCount);
            }
        }


        /// <summary>
        /// Builds the namespace and containing type openings for the target (partial, nested/generic aware).
        /// </summary>
        public static string ToNamespaceAndContainingTypeDeclarations(this Target target, int indentSize = 4, char indentChar = ' ')
            => ToNamespaceAndContainingTypeDeclarations(target.RawSymbol, indentSize, indentChar);

        /// <summary>
        /// Builds the namespace and containing type openings for the symbol (partial, nested/generic aware).
        /// </summary>
        public static string ToNamespaceAndContainingTypeDeclarations(this ISymbol symbol, int indentSize = 4, char indentChar = ' ')
        {
            const string OPEN = " {";

            var sb = new StringBuilder(capacity: 256);

            if (symbol is not ITypeSymbol typeSymbol)
            {
                typeSymbol = symbol.ContainingType;
            }

            var hasOpen = false;

            if (!typeSymbol.ContainingNamespace.IsGlobalNamespace)
            {
                sb.Append("namespace ");
                sb.Append(typeSymbol.ContainingNamespace.ToDisplayString());
                sb.Append(OPEN);
                hasOpen = true;
            }

            foreach (var con in GetContainingTypes(typeSymbol))
            {
                if (hasOpen)
                {
                    sb.AppendLine();
                }
                sb.Append("partial ");
                sb.Append(con.ToDeclarationString());
                sb.Append(OPEN);
                hasOpen = true;
            }

            if (hasOpen && sb.Length >= OPEN.Length)
            {
                sb.Length -= OPEN.Length;
                sb.AppendLine();
                sb.Append("{");
            }

            return sb.ToString();
        }


        /// <summary>
        /// Builds closing braces for namespace/containing types opened by <see cref="ToNamespaceAndContainingTypeDeclarations(Target, int, char)"/>.
        /// </summary>
        public static string ToNamespaceAndContainingTypeClosingBraces(this Target target, int indentSize = 4, char indentChar = ' ')
            => ToNamespaceAndContainingTypeClosingBraces(target.RawSymbol, indentSize, indentChar);

        /// <summary>
        /// Builds closing braces for namespace/containing types opened by <see cref="ToNamespaceAndContainingTypeDeclarations(ISymbol, int, char)"/>.
        /// </summary>
        public static string ToNamespaceAndContainingTypeClosingBraces(this ISymbol symbol, int indentSize = 4, char indentChar = ' ')
        {
            var sb = new StringBuilder(capacity: 16);

            foreach (var _ in GetContainingTypes(symbol))
            {
                sb.AppendLine("}");
            }

            if (!symbol.ContainingNamespace.IsGlobalNamespace)
            {
                sb.AppendLine("}");
            }

            var result = sb.ToString().TrimEnd();
            return result;
        }


        static readonly Dictionary<(bool, bool, bool), SymbolDisplayFormat> cache_ToNameStringFormat
            = new(capacity: 2 * 2 * 2);

        /// <summary>
        /// Renders a display name for the target with options for qualification, generics, and nullability.
        /// When fully qualified (default), includes the 'global::' prefix.
        /// </summary>
        public static string ToNameString(
            this Target target,
            bool nameOnly = false,
            bool noGeneric = false,
            bool noNullable = false)
            => ToNameString(target.RawSymbol, nameOnly, noGeneric, noNullable);

        /// <summary>
        /// Renders a display name for the symbol with options for qualification, generics, and nullability.
        /// When fully qualified (default), includes the 'global::' prefix.
        /// </summary>
        public static string ToNameString(
            this ISymbol symbol,
            bool nameOnly = false,
            bool noGeneric = false,
            bool noNullable = false)
        {
            var key = (nameOnly, noGeneric, noNullable);

            if (!cache_ToNameStringFormat.TryGetValue(key, out var format))
            {
                cache_ToNameStringFormat[key]
                = format
                = new(
                    globalNamespaceStyle:
                        nameOnly
                            ? SymbolDisplayGlobalNamespaceStyle.Omitted
                            : SymbolDisplayGlobalNamespaceStyle.Included,
                    typeQualificationStyle:
                        nameOnly
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

            if (!nameOnly)
            {
                var eii = GetExplicitInterfaceImplementationSymbol(symbol);
                if (eii != null)
                {
                    // DO NOT forward arguments!!
                    var prefix = eii.ToNameString(nameOnly: false, noGeneric: false, noNullable: false);

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


        static readonly Dictionary<(bool, bool), SymbolDisplayFormat> cache_ToDeclarationStringFormat
            = new(capacity: 2 * 2);

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

            var key = (modifiers, genericConstraints);

            if (!cache_ToDeclarationStringFormat.TryGetValue(key, out var format))
            {
                cache_ToDeclarationStringFormat[key]
                = format
                = new(
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
            }

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
