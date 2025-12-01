#:project ../src
#:property TargetFramework=netstandard2.0
#:property IsRoslynComponent=true
#:property PublishAot=false
#:property LangVersion=latest
#:property OutputType=Library

using FGenerator;
using Microsoft.CodeAnalysis;
using System.Text;

namespace FGDebugGenerator
{
    [Generator]
    public class FGDebugGenerator : FGeneratorBase
    {
        protected override string DiagnosticCategory => "FGDebugGenerator";
        protected override string DiagnosticIdPrefix => "FGDEBUG";

        protected override string? TargetAttributeName => "FGDebugAttribute";
        protected override string? PostInitializationOutput => @"
using System;

namespace FGDebugGenerator
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    internal sealed class FGDebugAttribute : Attribute
    {
    }
}
";

        protected override CodeGeneration? Generate(Target target, out AnalyzeResult? diagnostic)
        {
            diagnostic = null;

            var sb = new StringBuilder();

            sb.AppendLine("#nullable enable");
            sb.AppendLine();
            sb.AppendLine("using System.ComponentModel;");
            sb.AppendLine("using System.Runtime.CompilerServices;");
            sb.AppendLine();
            sb.AppendLine($"{target.ToNamespaceAndContainingTypeDeclarations()}");
            sb.AppendLine($"    partial {target.ToDeclarationString()}");
            sb.AppendLine("    {");
            sb.AppendLine("        public string HelperMethodTests()");
            sb.AppendLine("        {");
            sb.AppendLine("            var sb = new System.Text.StringBuilder();");
            {
                const string HR = "sb.AppendLine(new string('-', 64));";

                // toName
                sb.AppendLine(HR);
                sb.AppendLine("sb.AppendLine(\" ToNameString(localName: false, noGeneric: false, noNullable: false)\");");
                sb.AppendLine(HR);
                foreach (var member in target.Members)
                {
                    sb.Append("sb.AppendLine(\"* ");
                    sb.Append(member.ToNameString(localName: false, noGeneric: false, noNullable: false));
                    sb.AppendLine("\");");
                }

                sb.AppendLine(HR);
                sb.AppendLine("sb.AppendLine(\" ToNameString(localName: true, noGeneric: true, noNullable: true)\");");
                sb.AppendLine(HR);
                foreach (var member in target.Members)
                {
                    var fullName = member.ToNameString(localName: true, noGeneric: true, noNullable: true);
                    var shortName = member.ToNameString(localName: false, noGeneric: false, noNullable: false);
                    sb.Append("sb.AppendLine(\"* ");
                    sb.Append(shortName == fullName ? "<same>" : fullName);
                    sb.AppendLine("\");");
                }

                // toDeclaration
                sb.AppendLine(HR);
                sb.AppendLine("sb.AppendLine(\" ToDeclarationString(modifiers: false, genericConstraints: false)\");");
                sb.AppendLine(HR);
                foreach (var member in target.Members)
                {
                    sb.Append("sb.AppendLine(\"* ");
                    sb.Append(member.ToDeclarationString(modifiers: false, genericConstraints: false));
                    sb.AppendLine("\");");
                }

                sb.AppendLine(HR);
                sb.AppendLine("sb.AppendLine(\" ToDeclarationString(modifiers: true, genericConstraints: true)\");");
                sb.AppendLine(HR);
                foreach (var member in target.Members)
                {
                    sb.Append("sb.AppendLine(\"* ");
                    sb.Append(member.ToDeclarationString(modifiers: true, genericConstraints: true));
                    sb.AppendLine("\");");
                }

                // types
                sb.AppendLine(HR);
                sb.AppendLine("sb.AppendLine(\" NestedTypes\");");
                sb.AppendLine(HR);
                foreach (var member in target.NestedTypes)
                {
                    sb.Append("sb.AppendLine(\"* ");
                    sb.Append(member.ToDeclarationString(modifiers: false, genericConstraints: false));
                    sb.AppendLine("\");");
                }
                sb.AppendLine(HR);
                foreach (var member in target.NestedTypes)
                {
                    sb.Append("sb.AppendLine(\"* ");
                    sb.Append(member.ToDeclarationString(modifiers: true, genericConstraints: true));
                    sb.AppendLine("\");");
                }
            }
            sb.AppendLine("            return sb.ToString();");
            sb.AppendLine("        }");

            sb.AppendLine("    }");
            sb.AppendLine(target.ToNamespaceAndContainingTypeClosingBraces());

            return new CodeGeneration(target.ToHintName(), sb.ToString());
        }
    }
}
