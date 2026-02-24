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
public class DiscoveryTargetsTestGenerator : FGeneratorBase
{
    protected override string DiagnosticCategory => "DiscoveryTargetsTest";
    protected override string DiagnosticIdPrefix => "DSCT";

    protected override string? TargetAttributeName => "DiscoverTargets";

    protected override DiscoveryTargets DiscoveryTargets =>
        DiscoveryTargets.Type |
        DiscoveryTargets.Field |
        DiscoveryTargets.Property |
        DiscoveryTargets.Event |
        DiscoveryTargets.Method;

    protected override string? PostInitializationOutput =>
@"using System;
[AttributeUsage(AttributeTargets.All)]
internal sealed class DiscoverTargetsAttribute : Attribute { }
";

    protected override CodeGeneration? Generate(Target target, out AnalyzeResult? diagnostic)
    {
        diagnostic = null;

        var sb = new StringBuilder();
        sb.AppendLine(target.ToNamespaceAndContainingTypeDeclarations());
        sb.AppendLine("/*");
        sb.AppendLine($"ToDeclarationString: {target.ToDeclarationString()}");
        sb.AppendLine($"ToNameString: {target.ToNameString()}");
        sb.AppendLine($"ToAssemblyUniqueIdentifier: {target.ToAssemblyUniqueIdentifier()}");
        sb.AppendLine("*/");
        sb.AppendLine(target.ToNamespaceAndContainingTypeClosingBraces());

        return new CodeGeneration(target.ToHintName(), sb.ToString());
    }
}
