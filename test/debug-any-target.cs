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
public class AnyTargetGenerator : FGeneratorBase
{
    protected override string DiagnosticCategory => "AnyTargetGenerator";
    protected override string DiagnosticIdPrefix => "FGDEBUG";

    protected override string? TargetAttributeName => "AnyTargetAttribute";
    protected override string? PostInitializationOutput =>
@"using System;

[AttributeUsage(AttributeTargets.All)]
internal sealed class AnyTargetAttribute : Attribute { }
";

    protected override CodeGeneration? Generate(Target target, out AnalyzeResult? diagnostic)
    {
        diagnostic = null;
        return new CodeGeneration(target.ToHintName(), "// " + string.Join("\n// ", target.ToNameString()));
    }
}
