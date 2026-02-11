#:sdk FGenerator.Sdk@2.5.0
//                   ~~~~~ Push to origin AFTER new nuget package is available

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
