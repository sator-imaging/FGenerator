#:project ../src
#:property TargetFramework=netstandard2.0
#:property IsRoslynComponent=true
#:property PublishAot=false
#:property LangVersion=latest
#:property OutputType=Library

using FGenerator;
using Microsoft.CodeAnalysis;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text;

namespace StackListGenerator
{
    /// <summary>
    /// Generates a fixed-capacity list implementation for structs annotated with [StackList].
    /// Uses the first generic type parameter as the element type and enforces a maximum capacity.
    /// </summary>
    [Generator]
    public sealed class StackListGenerator : FGeneratorBase
    {
        protected override string DiagnosticCategory => "StackListGenerator";
        protected override string DiagnosticIdPrefix => "StackList";
        protected override string? TargetAttributeName => "StackList";

        protected override string? PostInitializationOutput => @"
using System;

namespace StackListGenerator
{
    [AttributeUsage(AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
    internal sealed class StackListAttribute : Attribute
    {
        public StackListAttribute(int length) => Length = length;

        public int Length { get; }
        public bool SwapRemove { get; set; }
    }
}
";

        protected override CodeGeneration? Generate(Target target, out AnalyzeResult? diagnostic)
        {
            diagnostic = null;

            if (target.RawSymbol is not INamedTypeSymbol typeSymbol)
            {
                diagnostic = new AnalyzeResult("001", "Invalid target", DiagnosticSeverity.Error, "StackList can only be applied to structs.");
                return null;
            }

            if (typeSymbol.TypeKind != TypeKind.Struct)
            {
                diagnostic = new AnalyzeResult("002", "Struct required", DiagnosticSeverity.Error, $"'{typeSymbol.Name}' must be a struct to use StackList.");
                return null;
            }

            if (typeSymbol.IsReadOnly)
            {
                diagnostic = new AnalyzeResult("003", "Readonly not supported", DiagnosticSeverity.Error, $"Struct '{typeSymbol.Name}' cannot be readonly when using StackList.");
                return null;
            }

            if (typeSymbol.IsRefLikeType)
            {
                diagnostic = new AnalyzeResult("011", "Ref-like not supported", DiagnosticSeverity.Error, $"Struct '{typeSymbol.Name}' cannot be ref-like when using StackList.");
                return null;
            }

            if (!target.IsPartial)
            {
                diagnostic = new AnalyzeResult("004", "Struct must be partial", DiagnosticSeverity.Error, $"Struct '{typeSymbol.Name}' must be declared as partial for StackList generation.");
                return null;
            }

            var attributeData = target.RawAttributes.FirstOrDefault();
            if (attributeData == null)
            {
                diagnostic = new AnalyzeResult("005", "Attribute missing", DiagnosticSeverity.Error, "StackList attribute could not be resolved.");
                return null;
            }

            int length = (attributeData.ConstructorArguments.Length != 0 && attributeData.ConstructorArguments[0].Value is int len) ? len : -1;
            if (length <= 0)
            {
                diagnostic = new AnalyzeResult("006", "Length must be positive", DiagnosticSeverity.Error, "Specify a positive length in [StackList].");
                return null;
            }

            var swapRemove = false;
            foreach (var kv in attributeData.NamedArguments)
            {
                if (kv.Key == "SwapRemove" && kv.Value.Value is bool sr)
                {
                    swapRemove = sr;
                    break;
                }
            }

            var elementTypeParameter = target.GenericTypeParameters.FirstOrDefault();
            if (elementTypeParameter == null)
            {
                diagnostic = new AnalyzeResult("007", "Generic type required", DiagnosticSeverity.Error, $"Struct '{typeSymbol.Name}' must declare at least one type parameter to use StackList.");
                return null;
            }

            if (elementTypeParameter.IsRefLikeType)
            {
                diagnostic = new AnalyzeResult("008", "Ref-like types unsupported", DiagnosticSeverity.Error, $"Element type '{elementTypeParameter.Name}' is ref-like and not supported.");
                return null;
            }
            var unmanagedConstraint = elementTypeParameter.HasUnmanagedTypeConstraint;
            if (!unmanagedConstraint)
            {
                diagnostic = new AnalyzeResult("009", "Unmanaged type required", DiagnosticSeverity.Error, $"Element type '{elementTypeParameter.Name}' must be constrained to unmanaged.");
                return null;
            }

            var usesEquatable = elementTypeParameter.ConstraintTypes.Any(ct =>
                ct.OriginalDefinition.ToNameString(nameOnly: false, noGeneric: true, noNullable: true) == "global::System.IEquatable");

            if (!usesEquatable)
            {
                diagnostic = new AnalyzeResult(
                    "010",
                    "IEquatable constraint recommended",
                    DiagnosticSeverity.Warning,
                    $"Element type '{elementTypeParameter.ToNameString(nameOnly: false, noGeneric: false, noNullable: false)}' is not constrained to IEquatable<T>; Contains/IndexOf will fall back to EqualityComparer and may be slower.");
            }

            var source = GenerateStackList(target, elementTypeParameter, length, swapRemove, usesEquatable);
            return new CodeGeneration(target.ToHintName(), source);
        }

        private static string GenerateStackList(Target target, ITypeSymbol elementTypeSymbol, int length, bool swapRemove, bool usesEquatable)
        {
            // Pre-size to reduce StringBuilder reallocations; body grows with configured length.
            var sb = new StringBuilder(768 + (length * 120));
            var elementTypeName = elementTypeSymbol.ToNameString(nameOnly: false, noGeneric: false, noNullable: false);
            var typeName = target.RawSymbol.ToNameString(nameOnly: false, noGeneric: false, noNullable: false);

            sb.AppendLine("#nullable enable");
            sb.AppendLine();
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using System.Runtime.InteropServices;");
            sb.AppendLine("using System.Diagnostics.CodeAnalysis;");
            sb.AppendLine("using System.ComponentModel;");
            sb.AppendLine("using System.Linq;");
            sb.AppendLine("using System.Text;");
            sb.AppendLine();

            sb.AppendLine(target.ToNamespaceAndContainingTypeDeclarations());
            sb.AppendLine("    [StructLayout(LayoutKind.Sequential, Pack = 1)]");
            sb.AppendLine($"    partial {target.ToDeclarationString()} : IList<{elementTypeName}>, IEnumerable<{elementTypeName}>, IEnumerator<{elementTypeName}>, IEquatable<{typeName}>");
            sb.AppendLine("    {");

            for (int i = 0; i < length; i++)
            {
                sb.Append("        private ");
                sb.Append(elementTypeName);
                sb.Append(" _value");
                sb.Append(i);
                sb.AppendLine(";");
            }

            sb.AppendLine();
            sb.AppendLine("        private int _count;");
            sb.AppendLine("        private int _enumeratorIndex;");
            sb.AppendLine();
            sb.AppendLine($"        public const int MaxCount = {length};");
            sb.AppendLine($"        public int Count => _count;");
            sb.AppendLine();
            sb.AppendLine("        bool ICollection<" + elementTypeName + ">.IsReadOnly => false;");
            sb.AppendLine();
            sb.AppendLine($"        public Span<{elementTypeName}> AsSpan() => MemoryMarshal.CreateSpan(ref _value0, _count);");
            sb.AppendLine($"        public Span<{elementTypeName}> AsFullSpan() => MemoryMarshal.CreateSpan(ref _value0, MaxCount);");
            sb.AppendLine();
            sb.AppendLine($"        public {elementTypeName} this[int index]");
            sb.AppendLine("        {");
            sb.AppendLine("            get");
            sb.AppendLine("            {");
            sb.AppendLine("                return AsSpan()[index];");
            sb.AppendLine("            }");
            sb.AppendLine("            set");
            sb.AppendLine("            {");
            sb.AppendLine("                AsSpan()[index] = value;");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        public void Add(" + elementTypeName + " item)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (_count >= MaxCount)");
            sb.AppendLine("            {");
            sb.AppendLine("                ThrowArgumentOutOfRange(\"capacity\", \"List has reached its maximum capacity.\");");
            sb.AppendLine("            }");
            sb.AppendLine();
            sb.AppendLine("            AsFullSpan()[_count] = item;");
            sb.AppendLine("            _count++;");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        public void Clear()");
            sb.AppendLine("        {");
            sb.AppendLine("            AsSpan().Clear();");
            sb.AppendLine("            _count = 0;");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine($"        public void CopyTo({elementTypeName}[] array, int arrayIndex)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (arrayIndex < 0) ThrowArgumentOutOfRange(nameof(arrayIndex));");
            sb.AppendLine("            if (array == null) throw new ArgumentNullException(nameof(array));");
            sb.AppendLine("            if (array.Length - arrayIndex < _count) throw new ArgumentException(\"Destination array is not long enough.\");");
            sb.AppendLine();
            sb.AppendLine("            AsSpan().CopyTo(array.AsSpan(arrayIndex));");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine($"        IEnumerator<{elementTypeName}> IEnumerable<{elementTypeName}>.GetEnumerator() => GetEnumerator();");
            sb.AppendLine("        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();");
            sb.AppendLine();
            sb.AppendLine("        [EditorBrowsable(EditorBrowsableState.Never)]");
            sb.AppendLine($"        public {typeName} GetEnumerator()");
            sb.AppendLine("        {");
            sb.AppendLine("            _enumeratorIndex = -1;");
            sb.AppendLine("            return this;");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        public bool Contains(" + elementTypeName + " item) => IndexOf(item) >= 0;");
            sb.AppendLine();
            sb.AppendLine("        public int IndexOf(" + elementTypeName + " item)");
            sb.AppendLine("        {");
            if (usesEquatable)
            {
                sb.AppendLine("            // Use Span<T>.IndexOf for IEquatable<T> to allow vectorized search and avoid comparer allocations.");
                sb.AppendLine("            return AsSpan().IndexOf(item);");
            }
            else
            {
                sb.AppendLine("            // Fall back to EqualityComparer<T> for unconstrained T; slower but works for any unmanaged T.");
                sb.AppendLine("            var span = AsSpan();");
                sb.AppendLine("            for (int i = 0; i < span.Length; i++)");
                sb.AppendLine("            {");
                sb.AppendLine("                if (EqualityComparer<" + elementTypeName + ">.Default.Equals(span[i], item))");
                sb.AppendLine("                {");
                sb.AppendLine("                    return i;");
                sb.AppendLine("                }");
                sb.AppendLine("            }");
                sb.AppendLine("            return -1;");
            }
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        public void Insert(int index, " + elementTypeName + " item)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (unchecked((uint)index > (uint)_count)) ThrowArgumentOutOfRange(nameof(index));");
            sb.AppendLine("            if (_count >= MaxCount) ThrowArgumentOutOfRange(\"capacity\", \"List has reached its maximum capacity.\");");
            sb.AppendLine();
            sb.AppendLine("            var fullSpan = AsFullSpan();");
            sb.AppendLine("            int moveCount = _count - index;");
            sb.AppendLine("            if (moveCount > 0)");
            sb.AppendLine("            {");
            sb.AppendLine("                fullSpan.Slice(index, moveCount).CopyTo(fullSpan.Slice(index + 1));");
            sb.AppendLine("            }");
            sb.AppendLine();
            sb.AppendLine("            fullSpan[index] = item;");
            sb.AppendLine("            _count++;");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        public void RemoveAt(int index)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (unchecked((uint)index >= (uint)_count)) ThrowArgumentOutOfRange(nameof(index));");
            sb.AppendLine();
            sb.AppendLine("            var fullSpan = AsFullSpan();");
            sb.AppendLine("            int lastIndex = _count - 1;");
            sb.AppendLine();
            if (swapRemove)
            {
                sb.AppendLine("            // Swap-remove: move last element into the removed slot to keep removal O(1) without preserving order.");
                sb.AppendLine("            if (_count > 1 && index != lastIndex)");
                sb.AppendLine("            {");
                sb.AppendLine("                fullSpan[index] = fullSpan[lastIndex];");
                sb.AppendLine("            }");
            }
            else
            {
                sb.AppendLine("            // Stable remove: shift elements left to preserve ordering at the cost of O(n) copies.");
                sb.AppendLine("            int moveCount = lastIndex - index;");
                sb.AppendLine("            if (moveCount > 0)");
                sb.AppendLine("            {");
                sb.AppendLine("                fullSpan.Slice(index + 1, moveCount).CopyTo(fullSpan.Slice(index));");
                sb.AppendLine("            }");
            }
            sb.AppendLine();
            sb.AppendLine("            fullSpan[lastIndex] = default!;");
            sb.AppendLine("            _count--;");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        public bool Remove(" + elementTypeName + " item)");
            sb.AppendLine("        {");
            sb.AppendLine("            int index = IndexOf(item);");
            sb.AppendLine("            if (index < 0)");
            sb.AppendLine("            {");
            sb.AppendLine("                return false;");
            sb.AppendLine("            }");
            sb.AppendLine();
            sb.AppendLine("            RemoveAt(index);");
            sb.AppendLine("            return true;");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        [EditorBrowsable(EditorBrowsableState.Never)]");
            sb.AppendLine($"        public {elementTypeName} Current => AsSpan()[_enumeratorIndex];");
            sb.AppendLine();
            sb.AppendLine("        object IEnumerator.Current => Current!;");
            sb.AppendLine();
            sb.AppendLine("        [EditorBrowsable(EditorBrowsableState.Never)]");
            sb.AppendLine("        public bool MoveNext()");
            sb.AppendLine("        {");
            sb.AppendLine("            if (_enumeratorIndex >= _count - 1)");
            sb.AppendLine("            {");
            sb.AppendLine("                return false;");
            sb.AppendLine("            }");
            sb.AppendLine();
            sb.AppendLine("            _enumeratorIndex++;");
            sb.AppendLine("            return true;");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        [EditorBrowsable(EditorBrowsableState.Never)]");
            sb.AppendLine("        public void Dispose() { }");
            sb.AppendLine();
            sb.AppendLine("        void IEnumerator.Reset() => throw new NotSupportedException();");
            sb.AppendLine();
            sb.AppendLine($"        public bool Equals({typeName} other)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (_count != other._count)");
            sb.AppendLine("            {");
            sb.AppendLine("                return false;");
            sb.AppendLine("            }");
            sb.AppendLine();
            sb.AppendLine("            return AsSpan().SequenceEqual(other.AsSpan());");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        public override bool Equals(object? obj) => obj is " + typeName + " other && Equals(other);");
            sb.AppendLine();
            sb.AppendLine("        public override int GetHashCode()");
            sb.AppendLine("        {");
            sb.AppendLine("            if (_count == 0) return HashCode.Combine(0);");
            sb.AppendLine();
            sb.AppendLine("            // Hash combines count plus first/middle/last samples (small counts may reuse the same index) to keep HashCode.Combine arity small.");
            sb.AppendLine("            var span = AsSpan();");
            sb.AppendLine("            return HashCode.Combine(_count, span[0], span[_count >> 1], span[_count - 1]);");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine($"        public static bool operator ==({typeName} left, {typeName} right) => left.Equals(right);");
            sb.AppendLine($"        public static bool operator !=({typeName} left, {typeName} right) => !left.Equals(right);");
            sb.AppendLine();
            sb.AppendLine("        public override string ToString()");
            sb.AppendLine("        {");
            sb.AppendLine("            var span = AsSpan();");
            sb.AppendLine("            var builder = new StringBuilder();");
            sb.AppendLine("            builder.Append('[');");
            sb.AppendLine("            for (int i = 0; i < span.Length; i++)");
            sb.AppendLine("            {");
            sb.AppendLine("                if (i > 0)");
            sb.AppendLine("                {");
            sb.AppendLine("                    builder.Append(\", \");");
            sb.AppendLine("                }");
            sb.AppendLine();
            sb.AppendLine("                builder.Append(span[i]);");
            sb.AppendLine("            }");
            sb.AppendLine();
            sb.AppendLine("            builder.Append(']');");
            sb.AppendLine("            return builder.ToString();");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        [DoesNotReturn]");
            sb.AppendLine("        private static void ThrowArgumentOutOfRange(string paramName, string? message = null)");
            sb.AppendLine("            => throw new ArgumentOutOfRangeException(paramName, message);");
            sb.AppendLine();
            sb.AppendLine($"        public bool AddUnique({elementTypeName} item)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (IndexOf(item) >= 0) return false;");
            sb.AppendLine();
            sb.AppendLine("            Add(item);");
            sb.AppendLine("            return true;");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        /// <summary>");
            sb.AppendLine($"        /// Adds all items from <paramref name=\"collection\"/> and returns the resulting <see cref=\"Count\"/>.");
            sb.AppendLine("        /// </summary>");
            sb.AppendLine("        /// <returns>The new total count after the items are appended.</returns>");
            sb.AppendLine($"        public int AddRange(IEnumerable<{elementTypeName}> collection)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (collection is null) throw new ArgumentNullException(nameof(collection));");
            sb.AppendLine();
            sb.AppendLine("            int incomingCount = collection switch");
            sb.AppendLine("            {");
            sb.AppendLine($"                ICollection<{elementTypeName}> x => x.Count,");
            sb.AppendLine($"                IReadOnlyCollection<{elementTypeName}> x => x.Count,");
            sb.AppendLine("                _ => -1,");
            sb.AppendLine("            };");
            sb.AppendLine();
            sb.AppendLine("            if (incomingCount > 0)");
            sb.AppendLine("            {");
            sb.AppendLine("                int newCount = _count + incomingCount;");
            sb.AppendLine("                if (newCount > MaxCount)");
            sb.AppendLine("                {");
            sb.AppendLine("                    ThrowArgumentOutOfRange(\"capacity\", \"List has reached its maximum capacity.\");");
            sb.AppendLine("                }");
            sb.AppendLine();
            sb.AppendLine("                int writeIndex = _count;");
            sb.AppendLine("                var destination = AsFullSpan();");
            sb.AppendLine("                foreach (var item in collection)");
            sb.AppendLine("                {");
            sb.AppendLine("                    destination[writeIndex] = item;");
            sb.AppendLine("                    writeIndex++;");
            sb.AppendLine("                }");
            sb.AppendLine();
            sb.AppendLine("                _count = newCount;");
            sb.AppendLine("                return _count;");
            sb.AppendLine("            }");
            sb.AppendLine("            else if (incomingCount < 0)");
            sb.AppendLine("            {");
            sb.AppendLine("                return AddRangeSlow(collection);");
            sb.AppendLine("            }");
            sb.AppendLine("            else");
            sb.AppendLine("            {");
            sb.AppendLine("                return _count;");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine($"        private int AddRangeSlow(IEnumerable<{elementTypeName}> collection)");
            sb.AppendLine("        {");
            sb.AppendLine("            foreach (var item in collection)");
            sb.AppendLine("            {");
            sb.AppendLine("                Add(item);");
            sb.AppendLine("            }");
            sb.AppendLine();
            sb.AppendLine("            return _count;");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        /// <summary>");
            sb.AppendLine($"        /// Adds items until capacity is reached; excess items are ignored.");
            sb.AppendLine("        /// </summary>");
            sb.AppendLine("        /// <returns>The new total count after copying up to available capacity.</returns>");
            sb.AppendLine($"        public int AddRangeTruncateOverflow(ReadOnlySpan<{elementTypeName}> items)");
            sb.AppendLine("        {");
            sb.AppendLine("            int available = MaxCount - _count;");
            sb.AppendLine("            if (available <= 0 || items.IsEmpty)");
            sb.AppendLine("            {");
            sb.AppendLine("                return _count;");
            sb.AppendLine("            }");
            sb.AppendLine();
            sb.AppendLine("            int copyLength = items.Length;");
            sb.AppendLine("            if (copyLength > available)");
            sb.AppendLine("            {");
            sb.AppendLine("                copyLength = available;");
            sb.AppendLine("            }");
            sb.AppendLine();
            sb.AppendLine("            items.Slice(0, copyLength).CopyTo(AsFullSpan().Slice(_count, copyLength));");
            sb.AppendLine("            _count += copyLength;");
            sb.AppendLine("            return _count;");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        /// <summary>");
            sb.AppendLine($"        /// Adds or replaces items while retaining only the most recent elements; drops oldest existing items first, or if incoming alone exceeds capacity keeps only its last MaxCount items.");
            sb.AppendLine("        /// </summary>");
            sb.AppendLine("        /// <returns>The new total count after the operation (never exceeds capacity).</returns>");
            sb.AppendLine($"        public int AddRangeDropOldest(ReadOnlySpan<{elementTypeName}> incoming)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (incoming.IsEmpty)");
            sb.AppendLine("            {");
            sb.AppendLine("                return _count;");
            sb.AppendLine("            }");
            sb.AppendLine();
            sb.AppendLine("            var existing = AsFullSpan();");
            sb.AppendLine("            int total = _count + incoming.Length;");
            sb.AppendLine("            if (total <= MaxCount)");
            sb.AppendLine("            {");
            sb.AppendLine("                incoming.CopyTo(existing.Slice(_count));");
            sb.AppendLine("                return (_count = total);");
            sb.AppendLine("            }");
            sb.AppendLine();
            sb.AppendLine("            // Incoming alone overflows capacity; keep the most recent portion of incoming.");
            sb.AppendLine("            if (incoming.Length >= MaxCount)");
            sb.AppendLine("            {");
            sb.AppendLine("                incoming.Slice(incoming.Length - MaxCount, MaxCount).CopyTo(existing);");
            sb.AppendLine("                return (_count = MaxCount);");
            sb.AppendLine("            }");
            sb.AppendLine("            else");
            sb.AppendLine("            {");
            sb.AppendLine("                int dropExisting = _count - (MaxCount - incoming.Length);");
            sb.AppendLine("                int existingCount = _count - dropExisting;");
            sb.AppendLine("                existing.Slice(dropExisting, existingCount).CopyTo(existing);");
            sb.AppendLine();
            sb.AppendLine("                incoming.CopyTo(existing.Slice(existingCount));");
            sb.AppendLine("                return (_count = MaxCount);");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        /// <summary>");
            sb.AppendLine($"        /// Adds unique items from <paramref name=\"collection\"/>.");
            sb.AppendLine("        /// </summary>");
            sb.AppendLine("        /// <returns>The number of items that were added.</returns>");
            sb.AppendLine($"        public int AddRangeUnique(IEnumerable<{elementTypeName}> collection)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (collection is null) throw new ArgumentNullException(nameof(collection));");
            sb.AppendLine();
            sb.AppendLine("            int added = 0;");
            sb.AppendLine("            foreach (var item in collection)");
            sb.AppendLine("            {");
            sb.AppendLine("                if (IndexOf(item) >= 0)");
            sb.AppendLine("                {");
            sb.AppendLine("                    continue;");
            sb.AppendLine("                }");
            sb.AppendLine();
            sb.AppendLine("                Add(item);");
            sb.AppendLine("                added++;");
            sb.AppendLine("            }");
            sb.AppendLine();
            sb.AppendLine("            return added;");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine(target.ToNamespaceAndContainingTypeClosingBraces());

            return sb.ToString();
        }
    }
}
