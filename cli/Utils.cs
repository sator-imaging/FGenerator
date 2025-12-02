using System.Diagnostics;

namespace FGenerator.Cli
{
    internal static class Utils
    {
        public static int ExecuteProcessAndCapture(string exe, string arguments, out string stdout, out string stderr)
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = exe,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            process.Start();
            stdout = process.StandardOutput.ReadToEnd();
            stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();

            return process.ExitCode;
        }

        public static int ExecuteProcess(string exe, string arguments, bool disableSucceededBuildStdout = false)
        {
            if (!disableSucceededBuildStdout)
            {
                Console.WriteLine(new string('-', 42));
                Console.WriteLine($"Executing...: {exe} {arguments}");
                Console.WriteLine(new string('-', 42));
            }

            var exitCode = ExecuteProcessAndCapture(exe, arguments, out var output_text, out var error_text);

            if (!disableSucceededBuildStdout ||
                // Stdout may contain error messages...
                exitCode != 0)
            {
                Console.WriteLine(output_text);
            }

            if (!string.IsNullOrWhiteSpace(error_text))
            {
                Console.Error.WriteLine(error_text);
            }

            return exitCode != 0 ? exitCode : 0;
        }


        public static bool PromptOverwrite(string filePath, bool force)
        {
            if (File.Exists(filePath))
            {
                if (force)
                {
                    Console.WriteLine($"Overwriting: {Path.GetFileName(filePath)}");
                    return true;
                }
                else
                {
                    Console.Write($"{Path.GetFileName(filePath)} already exists. Overwrite? (y/N): ");
                    var response = Console.ReadLine()?.Trim().ToLowerInvariant();
                    if (response != "y")
                    {
                        Console.WriteLine($"Skipped: {Path.GetFileName(filePath)}");
                        return false;
                    }
                }
            }
            return true;
        }

        public static void GenerateUnityMeta(FileInfo dllFile, bool force)
        {
            var metaPath = dllFile.FullName + ".meta";

            if (!PromptOverwrite(metaPath, force))
            {
                return;
            }

            // 2022.3.12 or newer: https://qiita.com/amenone_games/items/762cbea245f95b212cfa
            var metaContent =
$@"fileFormatVersion: 2
guid: {Guid.NewGuid():N}
labels:
- RoslynAnalyzer
PluginImporter:
  externalObjects: {{}}
  serializedVersion: 2
  iconMap: {{}}
  executionOrder: {{}}
  defineConstraints:
  - UNITY_2022_3_OR_NEWER
  - !UNITY_2022_3_11
  - !UNITY_2022_3_10
  - !UNITY_2022_3_9
  - !UNITY_2022_3_8
  - !UNITY_2022_3_7
  - !UNITY_2022_3_6
  - !UNITY_2022_3_5
  - !UNITY_2022_3_4
  - !UNITY_2022_3_3
  - !UNITY_2022_3_2
  - !UNITY_2022_3_1
  isPreloaded: 0
  isOverridable: 0
  isExplicitlyReferenced: 0
  validateReferences: 1
  platformData:
    Any:
      enabled: 0
  userData: 
  assetBundleName: 
  assetBundleVariant: 
";
            File.WriteAllText(metaPath, metaContent);
            Console.WriteLine($"Generated meta for: {dllFile.Name}");
        }
    }
}
