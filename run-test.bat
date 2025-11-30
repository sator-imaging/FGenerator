@echo off

dotnet run --project cli -- build ^
    "test/*.cs" -f -o "./.z__merged_dll__" ^
    || (echo === ERROR === & exit 1)

dotnet run --project cli -- build ^
    %* ^
    --unity --force --output "./.z__merged_dll__" ^
    "./sample/*.cs" ^
    || (echo === ERROR === & exit 1)

dotnet clean         sample/SampleConsumer  || (echo === ERROR === & exit 1)
dotnet run --project sample/SampleConsumer  || (echo === ERROR === & exit 1)
