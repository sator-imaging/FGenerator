#:project ../src
#:property TargetFramework=netstandard2.0
#:property IsRoslynComponent=true
#:property PublishAot=false
#:property LangVersion=latest
#:property OutputType=Library

using FGenerator;
using Microsoft.CodeAnalysis;
using System;
using System.Text;

[Generator]
public class DiscoverTestGenerator : FGeneratorBase
{
    protected override string DiagnosticCategory => "DiscoverTest";
    protected override string DiagnosticIdPrefix => "DISC";

    protected override string? TargetAttributeName => "Discover";

    protected override string? PostInitializationOutput =>
@"using System;
[AttributeUsage(AttributeTargets.All)]
internal sealed class DiscoverAttribute : Attribute { }
";

    protected override CodeGeneration? Generate(Target target, out AnalyzeResult? diagnostic)
    {
        diagnostic = null;

        var isTypeOrMember = target.RawSymbol is ITypeSymbol or IMethodSymbol or IPropertySymbol or IFieldSymbol or IEventSymbol;

        var sb = new StringBuilder();
        if (isTypeOrMember) sb.AppendLine(target.ToNamespaceAndContainingTypeDeclarations());
        sb.AppendLine("/*");
sb.AppendLine($"    {target.ToDeclarationString()}  // ToDeclarationString");
sb.AppendLine($"    {target.ToNameString()}  // ToNameString");
sb.AppendLine($"    {target.ToAssemblyUniqueIdentifier()}  // ToAssemblyUniqueIdentifier");
        sb.AppendLine("*/");
        if (isTypeOrMember) sb.AppendLine(target.ToNamespaceAndContainingTypeClosingBraces());

        return new CodeGeneration(target.ToHintName(), sb.ToString());
    }
}
