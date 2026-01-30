// Licensed under the Apache-2.0 License
// https://github.com/sator-imaging/FGenerator

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

#pragma warning disable IDE0301  // Use collection expression for empty
#pragma warning disable IDE0045  // Use conditional expression for assignment

namespace FGenerator
{
    /// <summary>
    /// Wraps a symbol discovered by the generator along with metadata used during code generation.
    /// </summary>
    public sealed class Target : IEquatable<Target>
    {
        /// <summary>
        /// Original symbol discovered by the generator (type, method, etc.).
        /// </summary>
        public readonly ISymbol RawSymbol;

        /// <summary>
        /// Attributes that matched the target attribute filter.
        /// </summary>
        public readonly ImmutableArray<AttributeData> RawAttributes;

        /// <summary>
        /// Compilation that contains the target when it was discovered.
        /// </summary>
        public Compilation? Compilation;

        /// <summary>
        /// Creates a new target for the discovered symbol and matching attributes.
        /// </summary>
        /// <param name="rawSymbol">Symbol representing the target (type, method, etc.).</param>
        /// <param name="rawAttributes">Attributes that matched the target attribute filter.</param>
        /// <param name="compilation">Compilation that contains the target when it was discovered.</param>
        public Target(
            ISymbol rawSymbol,
            ImmutableArray<AttributeData> rawAttributes,
            Compilation? compilation = null
        )
        {
            RawSymbol = rawSymbol;
            RawAttributes = rawAttributes;
            Compilation = compilation;

            IsPartial = rawSymbol.DeclaringSyntaxReferences.Any(sr =>
            {
                var syntax = sr.GetSyntax();
                return syntax is TypeDeclarationSyntax typeDecl &&
                       typeDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword));
            });

            SpecialType = rawSymbol is ITypeSymbol ts ? ts.SpecialType : SpecialType.None;

            if (rawSymbol is INamedTypeSymbol nts)
            {
                IsGeneric = nts.IsGenericType;
            }
            else if (rawSymbol is IMethodSymbol ms)
            {
                IsGeneric = ms.IsGenericMethod;
            }
            else
            {
                IsGeneric = false;
            }
        }

        /// <summary>
        /// Returns a display string for the target symbol with full qualification.
        /// </summary>
        public override string ToString() => this.ToNameString();

        /// <summary>
        /// Indicates whether the target symbol is declared partial.
        /// </summary>
        public bool IsPartial { get; }

        /// <summary>
        /// Special type classification for the symbol when it is a type symbol.
        /// </summary>
        public SpecialType SpecialType { get; }

        /// <summary>
        /// Indicates whether the symbol is generic (type or method).
        /// </summary>
        public bool IsGeneric { get; }

        /// <summary>
        /// Generic type parameters when the symbol is generic; otherwise an empty array.
        /// </summary>
        public ImmutableArray<ITypeParameterSymbol> GenericTypeParameters
        {
            get
            {
                if (IsGeneric)
                {
                    if (RawSymbol is INamedTypeSymbol nts)
                    {
                        return nts.TypeParameters;
                    }
                    else if (RawSymbol is IMethodSymbol ms)
                    {
                        return ms.TypeParameters;
                    }
                }

                return ImmutableArray<ITypeParameterSymbol>.Empty;
            }
        }

        /// <summary>
        /// Enumerates non-implicit members of the type, excluding nested types and property accessors.
        /// </summary>
        public IEnumerable<ISymbol> Members
        {
            get
            {
                if (RawSymbol is ITypeSymbol type)
                {
                    foreach (var member in type.GetMembers())
                    {
                        if (member.IsImplicitlyDeclared ||
                            member is ITypeSymbol)
                        {
                            continue;
                        }

                        if (member is IMethodSymbol method &&
                            method.MethodKind is MethodKind.PropertyGet or MethodKind.PropertySet)
                        {
                            continue;
                        }

                        yield return member;
                    }
                }
            }
        }

        /// <summary>
        /// Enumerates nested types declared within the target using a depth-first traversal (includes nested descendants).
        /// </summary>
        public IEnumerable<ITypeSymbol> NestedTypes
        {
            get
            {
                if (RawSymbol is ITypeSymbol type)
                {
                    return enumerateDepthFirst(type);

                    static IEnumerable<ITypeSymbol> enumerateDepthFirst(ITypeSymbol type)
                    {
                        foreach (var member in type.GetMembers())
                        {
                            if (member is not ITypeSymbol nestedType)
                            {
                                continue;
                            }

                            yield return nestedType;

                            foreach (var deep in enumerateDepthFirst(nestedType))
                            {
                                yield return deep;
                            }
                        }
                    }
                }

                return ImmutableArray<ITypeSymbol>.Empty;
            }
        }

        /// <summary>
        /// Gets the containing type stack for this target, from outermost to innermost.
        /// </summary>
        public ImmutableStack<INamedTypeSymbol> ContainingTypes
        {
            get => Utils.GetContainingTypes(RawSymbol, out _);
        }

        // IEquatable<T>
        /// <inheritdoc/>
        public override int GetHashCode() => SymbolEqualityComparer.Default.GetHashCode(this.RawSymbol);
        /// <inheritdoc/>
        public override bool Equals(object? obj) => obj is Target other && Equals(other);
        /// <inheritdoc/>
        public bool Equals(Target? other)
        {
            return SymbolEqualityComparer.Default.Equals(this.RawSymbol, other?.RawSymbol);
        }
        /// <inheritdoc/>
        public static bool operator ==(Target? left, Target? right) => ReferenceEquals(left, right) || left?.Equals(right) == true;
        /// <inheritdoc/>
        public static bool operator !=(Target? left, Target? right) => !(left == right);
    }
}
