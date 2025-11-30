using ILRepacking;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace FGenerator.Cli
{
    public static class DllMerger
    {
        public static FileInfo? Merge(FileInfo input, DirectoryInfo outputDir)
        {
            var tempDllFileList = outputDir.GetFiles("*.dll")
                .Select(f => f.FullName)
                .ToList();

            if (tempDllFileList.Count == 0)
            {
                Console.WriteLine("No DLLs found to merge.");
                return null;
            }

            var outputDllFileName = $"{Path.GetFileNameWithoutExtension(input.Name)}.dll";

            var primaryDll = tempDllFileList
                .FirstOrDefault(f => Path.GetFileName(f) == outputDllFileName)
                ?? throw new Exception("Primary DLL not found.");

            var otherDlls = tempDllFileList.Where(f => f != primaryDll).ToList();

            EnsureMissingReferencesStubbed(new(primaryDll), outputDir, tempDllFileList);

            // If we only have one DLL and it's the primary one, nothing to merge? 
            // Or maybe we want to repack it anyway? 
            // Usually merge implies > 1.
            if (otherDlls.Count == 0)
            {
                Console.WriteLine("Only one DLL found. Skipping merge.");
                return new(primaryDll);
            }

            var outputPath = Path.Combine(outputDir.FullName, "__merged__", outputDllFileName);
            Console.WriteLine($"Merging {tempDllFileList.Count} assemblies into {outputPath}...");
            Console.WriteLine($"- [MAIN] {primaryDll}");
            Console.WriteLine($"- {string.Join("\n- ", otherDlls)}");

            try
            {
                var repackOptions = new RepackOptions
                {
                    OutputFile = outputPath,
                    InputAssemblies = [primaryDll, .. otherDlls],
                    TargetKind = ILRepack.Kind.SameAsPrimaryAssembly,
                    AllowDuplicateResources = false,
                    AllowAllDuplicateTypes = false,
                    AllowMultipleAssemblyLevelAttributes = false,
                    CopyAttributes = true,

                    // no internalize and empty search dirs to skip unresolved DLLs
                    SearchDirectories = [], //[outputDir.FullName],
                    Internalize = false,
                    InternalizeAssemblies = [nameof(FGenerator)],

                    // Parallel = true, // Optional
                    // DebugInfo = true, // Optional
                    // RenameInternalized = true, // Optional
                    UnionMerge = true, // Keep external assembly references
                };
                var repacker = new ILRepack(repackOptions);
                repacker.Repack();

                Console.WriteLine("Merge complete.");

                return new(outputPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Merge failed: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                throw;
            }
        }

        private static void EnsureMissingReferencesStubbed(FileInfo primaryDll, DirectoryInfo outputDir, List<string> existingDlls)
        {
            var existingNames = existingDlls
                .Select(Path.GetFileNameWithoutExtension)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var referenceName in GetReferencedAssemblyNames(primaryDll))
            {
                if (existingNames.Contains(referenceName))
                {
                    continue;
                }

                Console.WriteLine(new string('=', 42));
                Console.WriteLine($"Referenced assembly not found; generating placeholder for workaround");
                GenerateEmptyDll(outputDir, referenceName);
                Console.WriteLine(new string('=', 42));
                Console.WriteLine();
            }
        }

        private static IEnumerable<string> GetReferencedAssemblyNames(FileInfo assemblyFile)
        {
            try
            {
                using var stream = assemblyFile.OpenRead();
                using var peReader = new PEReader(stream);
                if (!peReader.HasMetadata)
                {
                    return Array.Empty<string>();
                }

                var metadata = peReader.GetMetadataReader();
                var names = new List<string>(metadata.AssemblyReferences.Count);

                foreach (var handle in metadata.AssemblyReferences)
                {
                    var reference = metadata.GetAssemblyReference(handle);
                    names.Add(metadata.GetString(reference.Name));
                }

                return names;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to read assembly references from {assemblyFile.Name}: {ex.Message}");
                return Array.Empty<string>();
            }
        }

        private static void GenerateEmptyDll(DirectoryInfo outputDir, string assemblyName)
        {
            var existing = Path.Combine(outputDir.FullName, $"{assemblyName}.dll");
            if (File.Exists(existing))
            {
                Console.WriteLine($"Placeholder already exists: {existing}");
                return;
            }

            var csFile = new FileInfo(Path.Combine(outputDir.FullName, $"{assemblyName}.cs"));

            File.WriteAllText(csFile.FullName,
@"#:property TargetFramework=netstandard2.0
#:property DebugType=none
#:property PublishAot=false
#:property LangVersion=latest
#:property OutputType=Library
");

            Console.WriteLine($"Generating empty {csFile.Name}...");

            var args = $"build \"{csFile.FullName}\" -o \"{outputDir.FullName}\"";

            var exitCode = Utils.ExecuteProcess("dotnet", args, disableSucceededBuildStdout: true);
            if (exitCode != 0)
            {
                Console.Error.WriteLine($"Build failed with exit code {exitCode}");
                return;
            }

            Console.WriteLine($"Empty dll generation succeeded: {assemblyName}");
        }
    }
}
