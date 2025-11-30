#:project ../src
#:property TargetFramework=netstandard2.0
#:property IsRoslynComponent=true
#:property PublishAot=false
#:property LangVersion=latest
#:property OutputType=Library

using FGenerator;
using Microsoft.CodeAnalysis;
using System.Text;

[Generator]
public class FGDebugAllTypes : FGeneratorBase
{
    protected override string DiagnosticCategory => "FGDebugAllTypes";
    protected override string DiagnosticIdPrefix => "FGDEBUG";

    protected override string? TargetAttributeName => null;
    protected override string? PostInitializationOutput => @"// FGDebugAllTypes";

    protected override CodeGeneration? Generate(Target target, out AnalyzeResult? diagnostic)
    {
        diagnostic = null;

        return new CodeGeneration(target.ToHintName(), "// " + string.Join("\n// ", target.Members.Select(x => x.ToNameString())));
    }
}
