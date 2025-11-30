#:sdk FGenerator.Sdk@1.1.1

using FGenerator;
using Microsoft.CodeAnalysis;

[Generator]
public class BuiltWithSdkDirectiveGenerator : FGeneratorBase
{
    protected override string DiagnosticCategory => "BuiltWithSdkDirectiveGenerator";
    protected override string DiagnosticIdPrefix => "BWSDK";

    protected override string? TargetAttributeName => "BuiltWithSdkDirectiveAttribute";
    protected override string? PostInitializationOutput => "// Built with FGenerator.Sdk";

    protected override CodeGeneration? Generate(Target target, out AnalyzeResult? diagnostic)
    {
        diagnostic = null;
        return new CodeGeneration(target.ToHintName(), "// Built with FGenerator.Sdk");
    }
}
