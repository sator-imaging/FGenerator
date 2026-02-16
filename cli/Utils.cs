// Licensed under the Apache-2.0 License
// https://github.com/sator-imaging/FGenerator

using System.Diagnostics;
using System.Text.RegularExpressions;

namespace FGenerator.Cli
{
    internal static class Utils
    {
        const string AnsiColorRed = "\u001b[97;41m";
        const string AnsiColorYellow = "\u001b[97;43m";
        const string AnsiColorReset = "\u001b[0m";

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

            stdout = Colorize(stdout);
            stderr = Colorize(stderr);

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
            if (!File.Exists(filePath))
            {
                return true;
            }

            if (force)
            {
                Console.WriteLine($"Overwriting: {Path.GetFileName(filePath)}");
                return true;
            }

            Console.Write($"{Path.GetFileName(filePath)} already exists. Overwrite? (y/N): ");
            var response = Console.ReadLine()?.Trim().ToLowerInvariant();
            if (response == "y")
            {
                return true;
            }

            Console.WriteLine($"Skipped: {Path.GetFileName(filePath)}");
            return false;
        }

        public static void GenerateUnityMeta(FileInfo dllFile, bool force)
        {
            var metaPath = dllFile.FullName + ".meta";

            if (!PromptOverwrite(metaPath, force))
            {
                return;
            }

            // HACK: We can omit the 'serializedVersion' property.
            //       It is 3 for Unity 6+ but older versions of Unity require 2.
            //       There is no way to determine the target version, but, just omit it.

            // 2022.3.12 or newer: https://qiita.com/amenone_games/items/762cbea245f95b212cfa
            var metaContent =
$@"fileFormatVersion: 2
guid: {Guid.NewGuid():N}
labels:
- RoslynAnalyzer
PluginImporter:
  externalObjects: {{}}
  iconMap: {{}}
  executionOrder: {{}}
  defineConstraints:
  - UNITY_2022_3_OR_NEWER
  - '!UNITY_2022_3_11'
  - '!UNITY_2022_3_10'
  - '!UNITY_2022_3_9'
  - '!UNITY_2022_3_8'
  - '!UNITY_2022_3_7'
  - '!UNITY_2022_3_6'
  - '!UNITY_2022_3_5'
  - '!UNITY_2022_3_4'
  - '!UNITY_2022_3_3'
  - '!UNITY_2022_3_2'
  - '!UNITY_2022_3_1'
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

        // https://github.com/sator-imaging/FUnit/blob/v1.8.1/cli/Program.cs#L562-L606
        static string Colorize(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return message;
            }

            var ansiEscapeIndex = message.IndexOf('\u001b');
            if (ansiEscapeIndex == -1)
            {
                message = ColorizeInternal(message);
            }
            else
            {
                var head = message[..ansiEscapeIndex];
                var tail = message[ansiEscapeIndex..];
                message = ColorizeInternal(head) + tail;
            }
            return message;
        }

        static string ColorizeInternal(string text)
        {
            return LogRegex.WarningOrError().Replace(text, match =>
            {
                var color = match.Value.StartsWith("err", StringComparison.OrdinalIgnoreCase) ? AnsiColorRed : AnsiColorYellow;
                return $"{color}{match.Value}{AnsiColorReset}";
            });
        }
    }

    internal static partial class LogRegex
    {
        [GeneratedRegex(@"\b(warning(\(s\)|s)?|error(\(s\)|s)?|warn|err)(?!\w)", RegexOptions.IgnoreCase)]
        public static partial Regex WarningOrError();
    }
}
