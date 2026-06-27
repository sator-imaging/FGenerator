#:project ../src
#:property TargetFramework=netstandard2.0
#:property IsRoslynComponent=true
#:property PublishAot=false
#:property LangVersion=latest
#:property OutputType=Library

using FGenerator;
using Microsoft.CodeAnalysis;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System;

namespace StackArrayGenerator
{
    /// <summary>
    /// Generates inline array storage and helpers for structs annotated with [StackArray].
    /// Mirrors a subset of C# 12 InlineArray behavior with enumerable support.
    /// </summary>
    [Generator]
    public sealed class StackArrayGenerator : FGeneratorBase
    {
        protected override string DiagnosticCategory => "StackArrayGenerator";
        protected override string DiagnosticIdPrefix => "StackArray";
        protected override string? TargetAttributeName => "StackArray";

        protected override string? PostInitializationOutput =>
@"using System;

namespace StackArrayGenerator
{
    [AttributeUsage(AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
    internal sealed class StackArrayAttribute : Attribute
    {
        public StackArrayAttribute(int length, Type fieldType)
        {
            Length = length;
            FieldType = fieldType;
        }

        public int Length { get; }
        public Type FieldType { get; }
    }
}
";

        protected override CodeGeneration? Generate(Target target, out AnalyzeResult? diagnostic)
        {
            diagnostic = null;

            if (target.RawSymbol is not INamedTypeSymbol typeSymbol)
            {
                diagnostic = new AnalyzeResult("001", "Invalid target", DiagnosticSeverity.Error, "StackArray can only be applied to structs.");
                return null;
            }

            if (typeSymbol.TypeKind != TypeKind.Struct)
            {
                diagnostic = new AnalyzeResult("002", "Struct required", DiagnosticSeverity.Error, $"'{typeSymbol.Name}' must be a struct to use StackArray.");
                return null;
            }

            if (typeSymbol.IsReadOnly)
            {
                diagnostic = new AnalyzeResult("003", "Readonly not supported", DiagnosticSeverity.Error, $"Struct '{typeSymbol.Name}' cannot be readonly when using StackArray.");
                return null;
            }

            if (typeSymbol.IsRefLikeType)
            {
                diagnostic = new AnalyzeResult("010", "Ref-like not supported", DiagnosticSeverity.Error, $"Struct '{typeSymbol.Name}' cannot be ref-like when using StackArray.");
                return null;
            }

            if (!target.IsPartial)
            {
                diagnostic = new AnalyzeResult("004", "Struct must be partial", DiagnosticSeverity.Error, $"Struct '{typeSymbol.Name}' must be declared as partial for StackArray generation.");
                return null;
            }

            var attributeData = target.RawAttributes.FirstOrDefault();
            if (attributeData == null)
            {
                diagnostic = new AnalyzeResult("005", "Attribute missing", DiagnosticSeverity.Error, "StackArray attribute could not be resolved.");
                return null;
            }

            var length = (attributeData.ConstructorArguments.Length != 0 && attributeData.ConstructorArguments[0].Value is int len) ? len : -1;
            if (length <= 0)
            {
                diagnostic = new AnalyzeResult("006", "Length must be positive", DiagnosticSeverity.Error, "Specify a positive length in [StackArray].");
                return null;
            }

            var fieldTypeSymbol = attributeData.ConstructorArguments.Length > 1
                ? attributeData.ConstructorArguments[1].Value as ITypeSymbol
                : null;

            if (fieldTypeSymbol == null)
            {
                diagnostic = new AnalyzeResult("007", "Element type required", DiagnosticSeverity.Error, "Specify a field type in [StackArray(length, typeof(T))].");
                return null;
            }

            if (fieldTypeSymbol.IsRefLikeType)
            {
                diagnostic = new AnalyzeResult("008", "Ref-like types unsupported", DiagnosticSeverity.Error, $"Field type '{fieldTypeSymbol.Name}' is ref-like and not supported.");
                return null;
            }

            if (!fieldTypeSymbol.IsUnmanagedType)
            {
                diagnostic = new AnalyzeResult("009", "Element type must be unmanaged", DiagnosticSeverity.Error, $"Field type '{fieldTypeSymbol.Name}' must be unmanaged to use StackArray.");
                return null;
            }

            var source = GenerateStackArray(target, fieldTypeSymbol, length);
            return new CodeGeneration(target.ToHintName(), source);
        }

        private static string GenerateStackArray(Target target, ITypeSymbol elementType, int length)
        {
            // Pre-size to reduce StringBuilder reallocations; most content scales with element count.
            var sb = new StringBuilder(512 + (length * 80));
            var elementTypeName = elementType.ToNameString(localName: false, noGeneric: false, noNullable: false);
            var typeName = target.RawSymbol.ToNameString(localName: false, noGeneric: false, noNullable: false);
            var ctorTypeName = target.RawSymbol.ToNameString(localName: true, noGeneric: true, noNullable: true);

            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using System.ComponentModel;");
            sb.AppendLine("using System.Linq;");
            sb.AppendLine("using System.Runtime.CompilerServices;");
            sb.AppendLine("using System.Runtime.InteropServices;");
            sb.AppendLine("using System.Text;");
            sb.AppendLine();

            sb.AppendLine(target.ToNamespaceAndContainingTypeDeclarations());
            sb.AppendLine("    [StructLayout(LayoutKind.Sequential, Pack = 1)]");
            sb.AppendLine($"    partial {target.ToDeclarationString()} : IEnumerable<{elementTypeName}>, IEnumerator<{elementTypeName}>, IEquatable<{typeName}>");
            sb.AppendLine("    {");

            for (int i = 0; i < length; i++)
            {
                sb.Append("        private ");
                sb.Append(elementTypeName);
                sb.Append(" _value");
                sb.Append(i);
                sb.AppendLine(";");
            }

            sb.AppendLine($"        public const int Length = {length};");
            sb.AppendLine();
            sb.AppendLine("        private int _enumeratorIndex;");
            sb.AppendLine();
            sb.AppendLine($"        public {ctorTypeName}(ReadOnlySpan<{elementTypeName}> source, bool allowLengthMismatch = false)");
            sb.AppendLine("            : this()");
            sb.AppendLine("        {");
            sb.AppendLine("            int copyLength = source.Length;");
            sb.AppendLine("            if (!allowLengthMismatch && copyLength != Length)");
            sb.AppendLine("            {");
            sb.AppendLine("                throw new ArgumentException(\"Length mismatch.\", nameof(source));");
            sb.AppendLine("            }");
            sb.AppendLine();
            sb.AppendLine("            if (copyLength > Length)");
            sb.AppendLine("            {");
            sb.AppendLine("                copyLength = Length;");
            sb.AppendLine("            }");
            sb.AppendLine();
            sb.AppendLine("            var destination = AsSpan();");
            sb.AppendLine("            source.Slice(0, copyLength).CopyTo(destination);");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine($"        public Span<{elementTypeName}> AsSpan() => MemoryMarshal.CreateSpan(ref _value0, Length);");
            sb.AppendLine();
            sb.AppendLine($"        public ref {elementTypeName} this[int index] => ref AsSpan()[index];");
            sb.AppendLine();
            sb.AppendLine("        [EditorBrowsable(EditorBrowsableState.Never)]");
            sb.AppendLine($"        public {typeName} GetEnumerator()");
            sb.AppendLine("        {");
            sb.AppendLine("            _enumeratorIndex = -1;");
            sb.AppendLine("            return this;");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine($"        IEnumerator<{elementTypeName}> IEnumerable<{elementTypeName}>.GetEnumerator() => GetEnumerator();");
            sb.AppendLine("        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();");
            sb.AppendLine();
            sb.AppendLine("        [EditorBrowsable(EditorBrowsableState.Never)]");
            sb.AppendLine("        public bool MoveNext()");
            sb.AppendLine("        {");
            sb.AppendLine("            if (_enumeratorIndex >= Length - 1)");
            sb.AppendLine("            {");
            sb.AppendLine("                return false;");
            sb.AppendLine("            }");
            sb.AppendLine();
            sb.AppendLine("            _enumeratorIndex++;");
            sb.AppendLine("            return true;");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        void IEnumerator.Reset() => throw new NotSupportedException();");
            sb.AppendLine();
            sb.AppendLine("        [EditorBrowsable(EditorBrowsableState.Never)]");
            sb.AppendLine($"        public {elementTypeName} Current => AsSpan()[_enumeratorIndex];");
            sb.AppendLine();
            sb.AppendLine("        object IEnumerator.Current => Current!;");
            sb.AppendLine();
            sb.AppendLine("        [EditorBrowsable(EditorBrowsableState.Never)]");
            sb.AppendLine("        public void Dispose() { }");
            sb.AppendLine();
            sb.AppendLine($"        public bool Equals({typeName} other) => AsSpan().SequenceEqual(other.AsSpan());");
            sb.AppendLine();
            sb.AppendLine("        public override bool Equals(object? obj) => obj is " + typeName + " other && Equals(other);");
            sb.AppendLine();
            sb.AppendLine("        public override int GetHashCode()");
            sb.AppendLine("        {");
            sb.AppendLine("            // Hash combines Length plus up to 7 evenly spaced elements.");
            {
                var sampleCount = Math.Min(7, length);
                var diff = (float)length / sampleCount;
                var cursor = 0f;

                sb.Append("            return HashCode.Combine(Length");
                for (int i = 0; i < sampleCount; i++)
                {
                    var idx = (int)cursor;
                    if (idx >= length)
                    {
                        idx = length - 1;
                    }

                    sb.Append(", _value");
                    sb.Append(idx);

                    cursor += diff;
                }

                sb.AppendLine(");");
            }
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
            sb.AppendLine("    }");
            sb.AppendLine(target.ToNamespaceAndContainingTypeClosingBraces());

            return sb.ToString();
        }
    }
}
