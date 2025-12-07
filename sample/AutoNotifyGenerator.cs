#:project ../src
#:property TargetFramework=netstandard2.0
#:property IsRoslynComponent=true
#:property PublishAot=false
#:property LangVersion=latest
#:property OutputType=Library

using FGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text;

namespace AutoNotifyGenerator
{
    /// <summary>
    /// Sample generator that implements INotifyPropertyChanged for classes marked with [AutoNotify].
    /// This demonstrates how to use the FGenerator framework.
    /// </summary>
    [Generator]
    public class AutoNotifyGenerator : FGeneratorBase
    {
        /// <summary>
        /// Diagnostic category for any errors/warnings this generator produces.
        /// </summary>
        protected override string DiagnosticCategory => "AutoNotifyGenerator";

        /// <summary>
        /// Prefix for diagnostic IDs (e.g., "AUTONOTIFY001").
        /// </summary>
        protected override string DiagnosticIdPrefix => "AUTONOTIFY";

        /// <summary>
        /// The name of the attribute to look for. Can be "AutoNotify" or "AutoNotifyAttribute".
        /// If null, all types in the compilation would be processed.
        /// </summary>
        protected override string? TargetAttributeName => "AutoNotify";

        /// <summary>
        /// Support contents that will be added to the compilation.
        /// This defines the [AutoNotify] attribute that users will apply to their classes.
        /// </summary>
        protected override string? PostInitializationOutput =>
@"using System;

namespace AutoNotifyGenerator
{
    /// <summary>
    /// Apply this attribute to a partial class to automatically implement INotifyPropertyChanged.
    /// Eligible private instance fields get corresponding properties with change notification.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    internal sealed class AutoNotifyAttribute : Attribute
    {
    }
}
";

        /// <summary>
        /// Main generation method called for each target found.
        /// </summary>
        /// <param name="target">The target symbol (class, method, etc.) to generate code for.</param>
        /// <param name="diagnostic">Output parameter for any diagnostic to report.</param>
        /// <returns>Generated code or null if nothing to generate.</returns>
        protected override CodeGeneration? Generate(Target target, out AnalyzeResult? diagnostic)
        {
            diagnostic = null;

            // Ensure the target is a named type (class, struct, etc.)
            if (target.RawSymbol is not INamedTypeSymbol typeSymbol)
            {
                diagnostic = new AnalyzeResult("001", "Invalid target", DiagnosticSeverity.Error, "AutoNotify can only be applied to classes");
                return null;
            }

            // Ensure the class is partial (required for source generators)
            if (!target.IsPartial)
            {
                diagnostic = new AnalyzeResult("002", "Class must be partial", DiagnosticSeverity.Error, $"Class '{typeSymbol.Name}' must be declared as partial to use AutoNotify");
                return null;
            }

            // Generate the implementation
            var source = GenerateNotifyPropertyChanged(target);

            // Use the Target extension method to generate a unique hint name
            var hintName = target.ToHintName();

            return new CodeGeneration(hintName, source);
        }

        /// <summary>
        /// Generates the INotifyPropertyChanged implementation for the given type.
        /// </summary>
        private string GenerateNotifyPropertyChanged(Target target)
        {
            var sb = new StringBuilder();

            sb.AppendLine("#nullable enable");
            sb.AppendLine();
            sb.AppendLine("using System.ComponentModel;");
            sb.AppendLine("using System.Runtime.CompilerServices;");
            sb.AppendLine();

            // Namespace and type declaration (partial to extend the user's class)
            sb.AppendLine($"{target.ToNamespaceAndContainingTypeDeclarations()}");
            sb.AppendLine($"    partial {target.ToDeclarationString()} : INotifyPropertyChanged");
            sb.AppendLine("    {");

            // PropertyChanged event
            sb.AppendLine("        /// <summary>");
            sb.AppendLine("        /// Occurs when a property value changes.");
            sb.AppendLine("        /// </summary>");
            sb.AppendLine("        public event PropertyChangedEventHandler? PropertyChanged;");
            sb.AppendLine();

            // OnPropertyChanged helper method
            sb.AppendLine("        /// <summary>");
            sb.AppendLine("        /// Raises the PropertyChanged event.");
            sb.AppendLine("        /// </summary>");
            sb.AppendLine("        /// <param name=\"propertyName\">Name of the property that changed.</param>");
            sb.AppendLine("        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)");
            sb.AppendLine("        {");
            sb.AppendLine("            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));");
            sb.AppendLine("        }");
            sb.AppendLine();

            // SetField helper method
            sb.AppendLine("        /// <summary>");
            sb.AppendLine("        /// Sets the field value and raises PropertyChanged if the value changed.");
            sb.AppendLine("        /// </summary>");
            sb.AppendLine("        /// <returns>True if the value changed, false otherwise.</returns>");
            sb.AppendLine("        protected bool SetField<A>(ref A field, A value, [CallerMemberName] string? propertyName = null)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (System.Collections.Generic.EqualityComparer<A>.Default.Equals(field, value))");
            sb.AppendLine("            {");
            sb.AppendLine("                return false;");
            sb.AppendLine("            }");
            sb.AppendLine();
            sb.AppendLine("            field = value;");
            sb.AppendLine("            OnPropertyChanged(propertyName);");
            sb.AppendLine("            return true;");
            sb.AppendLine("        }");

            var candidateFields = target.Members
                .OfType<IFieldSymbol>()
                .Where(ShouldGenerateProperty)
                .Select(field => (field, propertyName: GetPropertyName(field)))
                .ToList();

            // Auto-generated properties for eligible backing fields
            foreach (var item in candidateFields)
            {
                var typeName = item.field.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                sb.AppendLine();
                sb.AppendLine($"        public {typeName} {item.propertyName}");
                sb.AppendLine("        {");
                sb.AppendLine($"            get => {item.field.Name};");
                sb.AppendLine($"            set => SetField(ref {item.field.Name}, value);");
                sb.AppendLine("        }");
            }

            // Close class and namespace
            sb.AppendLine("    }");
            sb.AppendLine($"{target.ToNamespaceAndContainingTypeClosingBraces()}");

            return sb.ToString();

            bool ShouldGenerateProperty(IFieldSymbol field)
            {
                if (field.IsImplicitlyDeclared || field.AssociatedSymbol != null)
                    return false; // compiler generated or backing field
                if (field.IsConst || field.IsStatic)
                    return false;
                if (field.DeclaredAccessibility != Accessibility.Private)
                    return false;

                var propertyName = GetPropertyName(field);
                if (string.IsNullOrWhiteSpace(propertyName))
                    return false;

                return true;
            }

            string GetPropertyName(IFieldSymbol field)
            {
                var name = field.Name;
                if (string.IsNullOrWhiteSpace(name))
                {
                    return name;
                }

                var trimmed = name.TrimStart('_');
                if (trimmed.Length == 0)
                {
                    trimmed = name;
                }

                if (trimmed.Length == 1)
                {
                    return trimmed.ToUpperInvariant();
                }

                return char.ToUpperInvariant(trimmed[0]) + trimmed.Substring(1);
            }
        }
    }
}
