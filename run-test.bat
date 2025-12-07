@echo off

dotnet run --project cli -- build ^
    "test/*.cs" -f -o "./.z__merged_dll__" ^
    || (echo === ERROR === & exit 1)

dotnet run --project cli -- build ^
    %* ^
    --unity --output "./.z__merged_dll__" ^
    "./sample/*.cs" ^
    || (echo === ERROR === & exit 1)

:: 'dnx' is .bat file so need to explicitly call it with 'call' to avoid exiting unexpectedly.
call dnx -y FGenerator.Cli -- build ^
    %* ^
    --unity --output "./.z__merged_dll__" ^
    "./Unity/*.cs" ^
    || (echo === ERROR === & exit 1)

dotnet clean         sample/SampleConsumer  || (echo === ERROR === & exit 1)
dotnet run --project sample/SampleConsumer ^
    -p:EmitCompilerGeneratedFiles=true ^
    -p:CompilerGeneratedFilesOutputPath="../../.generated" ^
    || (echo === ERROR === & exit 1)
