using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;
using System.CommandLine;

namespace FGenerator.Cli
{
    static class Program
    {
        private const int CleanupRetryCount = 3;
        private const int CleanupRetryDelayMs = 500;

        static async Task<int> Main(string[] args)
        {
            if (!EnsureRequiredDotnetVersion())
            {
                return 1;
            }

            var outputOption = new Option<DirectoryInfo>(name: "--output", "-o")
            {
                Description = "Output directory (defaults to current directory)",
            };

            var debugOption = new Option<bool>(name: "--debug")
            {
                Description = "Build using Debug configuration (defaults to Release)",
            };

            var mergeOption = new Option<bool>(name: "--merge")
            {
                Description = "Merge resulting .DLL files into one",
            };

            var unityOption = new Option<bool>(name: "--unity")
            {
                Description = "Enable Unity .meta file generation (Unity 2022.3.12 or newer)",
            };

            var forceOption = new Option<bool>(name: "--force", "-f")
            {
                Description = "Force overwrite existing files without prompting",
            };

            var inputArgument = new Argument<string>(name: "input")
            {
                Description = "Input .cs file or glob pattern to process. Supports * (match files) and ** (recursive). e.g., file.cs, **/*.cs",
                Arity = ArgumentArity.ExactlyOne,
            };


            var buildCmd = new Command("build")
            {
                outputOption,
                unityOption,
                mergeOption,
                forceOption,
                debugOption,
                inputArgument
            };

            buildCmd.SetAction(parseResult =>
            {
                var input = parseResult.GetRequiredValue(inputArgument);
                var output = parseResult.GetValue(outputOption) ?? new DirectoryInfo(Directory.GetCurrentDirectory());
                var unity = parseResult.GetValue(unityOption);
                var merge = parseResult.GetValue(mergeOption);
                var force = parseResult.GetValue(forceOption);
                var debug = parseResult.GetValue(debugOption);

                return RunBuildWithGlobPattern(input, output, unity, merge, force, debug);
            });


            var rootCommand = new RootCommand("FGenerator Build Tool")
            {
                buildCmd,
            };

            return await rootCommand.Parse(args).InvokeAsync();
        }


        static int RunBuildWithGlobPattern(string inputPattern, DirectoryInfo output, bool unity, bool merge, bool force, bool debug)
        {
            // Resolve glob pattern to list of files (also works with single file paths)
            var matcher = new Matcher();
            var baseDirectory = Directory.GetCurrentDirectory();

            matcher.AddInclude(inputPattern);

            // Execute the glob pattern match
            var result = matcher.Execute(new DirectoryInfoWrapper(new DirectoryInfo(baseDirectory)));
            var matchedFiles = result.Files.Select(f => new FileInfo(Path.Combine(baseDirectory, f.Path))).ToList();

            // Filter to only .cs files
            matchedFiles = matchedFiles.Where(f => f.Extension.Equals(".cs", StringComparison.OrdinalIgnoreCase)).ToList();

            if (matchedFiles.Count == 0)
            {
                WriteFailure($"No .cs files matched the pattern: {inputPattern}");
                return 1;
            }

            Console.WriteLine($"Found {matchedFiles.Count} file(s) matching pattern: {inputPattern}");
            Console.WriteLine();

            int successCount = 0;
            int failureCount = 0;
            var failedFiles = new List<string>();

            // Process each matched file
            for (int i = 0; i < matchedFiles.Count; i++)
            {
                var file = matchedFiles[i];
                Console.WriteLine($"[{i + 1}/{matchedFiles.Count}] Processing: {file.Name}");

                int exitCode = RunBuild(file, output, unity, merge, force, debug);

                if (exitCode == 0)
                {
                    successCount++;
                }
                else
                {
                    failureCount++;
                    failedFiles.Add(file.Name);
                }

                // Add separator between files
                if (i < matchedFiles.Count - 1)
                {
                    Console.WriteLine();
                    Console.WriteLine(new string('=', 42));
                    Console.WriteLine();
                }
            }

            // Print summary
            Console.WriteLine();
            Console.WriteLine(new string('=', 42));
            Console.WriteLine("SUMMARY");
            Console.WriteLine(new string('=', 42));
            Console.WriteLine($"Total files: {matchedFiles.Count}");
            WriteSuccess($"Succeeded: {successCount}");
            if (failureCount > 0)
            {
                WriteFailure($"Failed: {failureCount}");
                Console.WriteLine("Failed files:");
                foreach (var failedFile in failedFiles)
                {
                    Console.WriteLine($"  - {failedFile}");
                }
            }

            return failureCount > 0 ? 1 : 0;
        }

        static int RunBuild(FileInfo input, DirectoryInfo output, bool unity, bool merge, bool force, bool debug)
        {
            Console.WriteLine(new string('-', 42));
            Console.WriteLine($"Input File: {input.FullName}");
            Console.WriteLine($"Output Directory: {output.FullName}");
            Console.WriteLine($"Unity Mode: {unity}");
            Console.WriteLine($"Merge Mode: {merge}");
            Console.WriteLine($"Force Overwrite: {force}");
            var configuration = debug ? "Debug" : "Release";
            Console.WriteLine($"Configuration: {configuration}");
            Console.WriteLine(new string('-', 42));

            if (!input.Exists)
            {
                WriteFailure($"Input file does not exist: {input.FullName}");
                return 1;
            }

            // Always use temp directory for build output
            var tempDir = new DirectoryInfo(Path.Combine(Path.GetTempPath(), $"{nameof(FGenerator)}.{nameof(Cli)}_{Guid.NewGuid():N}"));
            tempDir.Create();
            Console.WriteLine($"Build output directory (temp): {tempDir.FullName}");

            try
            {
                // Run dotnet build on input file
                Console.WriteLine($"Building {input.Name}...");

                var args = $"build \"{input.FullName}\" -c {configuration} -o \"{tempDir.FullName}\"";

                var exitCode = Utils.ExecuteProcess("dotnet", args);
                if (exitCode != 0)
                {
                    WriteFailure($"Build failed with exit code {exitCode}");
                    return exitCode;
                }

                Console.WriteLine("Build succeeded.");

                if (merge)
                {
                    int mergeResult = PerformMerge(input, tempDir, output, force);
                    if (mergeResult != 0)
                    {
                        return mergeResult;
                    }
                }
                else
                {
                    // Move .dll files from temp to output directory
                    int moveResult = MoveDllFiles(tempDir, output, force);
                    if (moveResult != 0)
                    {
                        return moveResult;
                    }
                }

                if (unity)
                {
                    Console.WriteLine("Unity mode enabled. Generating .meta files...");
                    // Generate .meta files in the actual output directory
                    foreach (var file in output.GetFiles("*.dll"))
                    {
                        Utils.GenerateUnityMeta(file, force);
                    }
                }
            }
            finally
            {
                // Clean up temp directory
                if (tempDir.Exists)
                {
                    // Force garbage collection to release any file handles
                    GC.Collect();
                    GC.WaitForPendingFinalizers();

                    // Retry cleanup with delays to allow file handles to be released
                    for (int i = 0; i < CleanupRetryCount; i++)
                    {
                        try
                        {
                            Console.WriteLine($"Cleaning up temporary directory: {tempDir.FullName}");
                            tempDir.Delete(recursive: true);
                            break;
                        }
                        catch (Exception ex)
                        {
                            if (i < CleanupRetryCount - 1)
                            {
                                Console.WriteLine($"Cleanup attempt {i + 1} failed, retrying in {CleanupRetryDelayMs}ms...");
                                Thread.Sleep(CleanupRetryDelayMs);
                            }
                            else
                            {
                                Console.WriteLine($"Warning: Failed to delete temporary directory after {CleanupRetryCount} attempts: {ex.Message}");
                                Console.WriteLine($"Please manually delete: {tempDir.FullName}");
                            }
                        }
                    }
                }
            }

            WriteSuccess("Build completed successfully.");
            return 0;
        }

        static int PerformMerge(FileInfo input, DirectoryInfo buildOutputDir, DirectoryInfo finalOutputDir, bool force)
        {
            try
            {
                Console.WriteLine("Merge mode enabled.");

                // Use input filename for merged DLL
                var mergedFileName = Path.GetFileNameWithoutExtension(input.Name) + ".dll";
                var mergedDllPath = Path.Combine(buildOutputDir.FullName, mergedFileName);
                var destPath = Path.Combine(finalOutputDir.FullName, mergedFileName);

                var mergedDll = DllMerger.Merge(input, buildOutputDir);

                // Rename Merged.dll to input filename
                if (mergedDll?.Exists != true)
                {
                    WriteFailure("Merged.dll was not created.");
                    return 1;
                }

                mergedDll.MoveTo(mergedDllPath, overwrite: true);

                // Check if destination exists and handle force flag
                if (File.Exists(destPath) && !force)
                {
                    Console.Write($"{mergedFileName} already exists in output directory. Overwrite? (y/N): ");
                    var response = Console.ReadLine()?.Trim().ToLowerInvariant();
                    if (response != "y")
                    {
                        Console.WriteLine($"Skipped: {mergedFileName}");
                        return 0;
                    }
                }

                // Copy to actual output directory
                Console.WriteLine($"Copying {mergedFileName} to {finalOutputDir.FullName}");
                finalOutputDir.Create();
                File.Copy(mergedDllPath, destPath, overwrite: true);

                return 0;
            }
            catch (Exception ex)
            {
                WriteFailure($"Error during merge: {ex.Message}");
                return 1;
            }
        }

        static int MoveDllFiles(DirectoryInfo sourceDir, DirectoryInfo destDir, bool force)
        {
            try
            {
                Console.WriteLine($"Moving .dll files to {destDir.FullName}...");

                var dllFiles = sourceDir.GetFiles("*.dll");
                if (dllFiles.Length == 0)
                {
                    WriteFailure("No .dll files found in build output.");
                    return 1;
                }

                // Ensure destination directory exists
                destDir.Create();

                foreach (var dllFile in dllFiles)
                {
                    var destPath = Path.Combine(destDir.FullName, dllFile.Name);

                    // Check if destination exists and handle force flag
                    if (File.Exists(destPath) && !force)
                    {
                        Console.Write($"{dllFile.Name} already exists in output directory. Overwrite? (y/N): ");
                        var response = Console.ReadLine()?.Trim().ToLowerInvariant();
                        if (response != "y")
                        {
                            Console.WriteLine($"Skipped: {dllFile.Name}");
                            continue;
                        }
                    }

                    Console.WriteLine($"Moving: {dllFile.Name}");
                    File.Copy(dllFile.FullName, destPath, overwrite: true);
                }

                return 0;
            }
            catch (Exception ex)
            {
                WriteFailure($"Error moving .dll files: {ex.Message}");
                return 1;
            }
        }

        static void WriteSuccess(string message)
        {
            var previousColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"✓ {message}");
            Console.ForegroundColor = previousColor;
        }

        static void WriteFailure(string message)
        {
            var previousColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine($"× {message}");
            Console.ForegroundColor = previousColor;
        }

        static bool EnsureRequiredDotnetVersion()
        {
            var exitCode = Utils.ExecuteProcessAndCapture("dotnet", "--version", out var stdout, out var stderr);
            if (exitCode != 0)
            {
                var message = string.IsNullOrWhiteSpace(stderr) ? "Unable to determine .NET SDK version." : stderr.Trim();
                WriteFailure($"{message} Install .NET 10 or newer from https://dotnet.microsoft.com/download.");
                return false;
            }

            var versionString = stdout.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (string.IsNullOrWhiteSpace(versionString))
            {
                WriteFailure("Unable to determine .NET SDK version. Install .NET 10 or newer from https://dotnet.microsoft.com/download.");
                return false;
            }

            var normalized = versionString.Split('-')[0];
            if (!Version.TryParse(normalized, out var version))
            {
                WriteFailure($"Unable to parse .NET SDK version: {versionString}");
                return false;
            }

            if (version.Major < 10)
            {
                WriteFailure($".NET SDK 10 or newer is required. Detected {version}.");
                return false;
            }

            return true;
        }
    }
}
