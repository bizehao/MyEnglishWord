using System.CommandLine;
using System.Formats.Tar;
using System.IO.Compression;

namespace WordNetSqliteDbGen
{
    public static class Program
    {
        public static async Task<int> Main(string[] args)
        {
            var dictDirOption = new Option<string?>("--dict-dir")
            {
                Description = "Directory containing the WordNet dictionary files (data.noun, index.noun, ...).",
            };
            var dictArchiveOption = new Option<string?>("--dict-archive")
            {
                Description = "A .tar.gz of the dictionary files, such as the wn3.1.dict.tar.gz of Princeton. Extracted to a temporary directory first.",
            };
            var outputOption = new Option<string>("--output")
            {
                Description = "Path of the SQLite database to generate.",
                Required = true,
            };
            var forceOption = new Option<bool>("--force")
            {
                Description = "Generate the database even when it is newer than the dictionary and this tool.",
            };

            var rootCommand = new RootCommand("Converts the WordNet dictionary files into a SQLite database.")
            {
                dictDirOption,
                dictArchiveOption,
                outputOption,
                forceOption,
            };

            rootCommand.SetAction((parseResult) =>
            {
                var dictDir = parseResult.GetValue(dictDirOption);
                var dictArchive = parseResult.GetValue(dictArchiveOption);
                var output = parseResult.GetValue(outputOption)!;

                if (string.IsNullOrEmpty(dictDir) == string.IsNullOrEmpty(dictArchive))
                {
                    Console.Error.WriteLine("Pass either --dict-dir or --dict-archive.");
                    return Task.FromResult(1);
                }

                return RunAsync(dictDir, dictArchive, output, parseResult.GetValue(forceOption));
            });

            return await rootCommand.Parse(args).InvokeAsync();
        }

        private static async Task<int> RunAsync(string? dictDir, string? dictArchive, string output, bool force)
        {
            // Building the dictionary takes minutes, so a build only regenerates it when the
            // dictionary or this tool changed. A build system can rely on that instead of keeping
            // its own list of what this tool depends on.
            if (!force && IsUpToDate(dictDir, dictArchive, output))
            {
                Console.WriteLine($"{output} is up to date");
                return 0;
            }

            string? extractedDirectory = null;
            if (dictArchive != null)
            {
                if (!File.Exists(dictArchive))
                {
                    Console.Error.WriteLine($"Dictionary archive '{dictArchive}' does not exist.");
                    return 1;
                }

                // The archive is unpacked in the physical form of the temporary directory, which is
                // the same place with any symbolic link in its path resolved. The tar extractor of
                // the runtime compares the directory it is given against that physical path and
                // throws as soon as the two differ, and on macOS the temporary directory is always
                // behind the /var link.
                extractedDirectory = ResolveSymlinks(Path.Combine(Path.GetTempPath(), "WordNetSqliteDbGen", Guid.NewGuid().ToString("N")));
                Directory.CreateDirectory(extractedDirectory);
                Console.WriteLine($"Extracting {dictArchive}");
                await ExtractArchiveAsync(dictArchive, extractedDirectory);
                dictDir = FindDictionaryDirectory(extractedDirectory);

                if (dictDir == null)
                {
                    Console.Error.WriteLine($"'{dictArchive}' does not contain a WordNet dictionary.");
                    return 1;
                }
            }

            try
            {
                if (!Directory.Exists(dictDir))
                {
                    Console.Error.WriteLine($"Dictionary directory '{dictDir}' does not exist.");
                    return 1;
                }

                foreach (var pos in new[] { "noun", "verb", "adj", "adv" })
                {
                    var dataFile = Path.Combine(dictDir!, $"data.{pos}");
                    if (!File.Exists(dataFile))
                    {
                        Console.Error.WriteLine($"'{dataFile}' does not exist; '{dictDir}' is not a WordNet dictionary directory.");
                        return 1;
                    }
                }

                var outputDirectory = Path.GetDirectoryName(Path.GetFullPath(output));
                if (!string.IsNullOrEmpty(outputDirectory))
                {
                    Directory.CreateDirectory(outputDirectory);
                }

                // Generated next to the target and moved into place, so that a build that runs this
                // for several target frameworks at once cannot leave a half written database behind.
                // The temporary name is unique per run for the same reason.
                var temporaryOutput = $"{output}.{Guid.NewGuid():N}.tmp";
                var start = DateTime.UtcNow;
                var builder = new WordNetDbBuilder(dictDir!, temporaryOutput, Console.WriteLine);
                builder.Build();
                try
                {
                    File.Move(temporaryOutput, output, overwrite: true);
                }
                catch (IOException exception)
                {
                    File.Delete(temporaryOutput);
                    Console.Error.WriteLine(
                        $"'{output}' could not be replaced ({exception.Message}). A build that read it may still be running; " +
                        "rebuild after it finished, or delete the file and build again.");
                    return 1;
                }
                Console.WriteLine($"Wrote {output} in {(DateTime.UtcNow - start).TotalSeconds:F1}s");

                return 0;
            }
            finally
            {
                if (extractedDirectory != null && Directory.Exists(extractedDirectory))
                {
                    Directory.Delete(extractedDirectory, recursive: true);
                }
            }
        }

        /// <summary>
        /// Whether the database is newer than the dictionary and this tool, so that a build can skip
        /// generating it.
        /// </summary>
        private static bool IsUpToDate(string? dictDir, string? dictArchive, string output)
        {
            if (!File.Exists(output))
            {
                return false;
            }

            var built = File.GetLastWriteTimeUtc(output);
            var inputs = new List<string>();
            if (dictArchive != null)
            {
                inputs.Add(dictArchive);
            }
            if (dictDir != null && Directory.Exists(dictDir))
            {
                inputs.AddRange(Directory.EnumerateFiles(dictDir));
            }

            // The assembly of this tool: a build system only rebuilds it when its sources change,
            // which is what makes the database follow the generator.
            var assembly = System.Reflection.Assembly.GetExecutingAssembly().Location;
            if (!string.IsNullOrEmpty(assembly))
            {
                inputs.Add(assembly);
            }

            return inputs.All(input => !File.Exists(input) || File.GetLastWriteTimeUtc(input) <= built);
        }

        private static async Task ExtractArchiveAsync(string archivePath, string targetDirectory)
        {
            await using var file = File.OpenRead(archivePath);
            await using var gzip = new GZipStream(file, CompressionMode.Decompress);
            await TarFile.ExtractToDirectoryAsync(gzip, targetDirectory, overwriteFiles: true);
        }

        /// <summary>
        /// The path with every symbolic link in it resolved, so that it names the same place without
        /// naming a link. The tar extractor of the runtime resolves the destination on its own and
        /// then miscomputes against it, so it has to be handed a path that resolves to itself.
        /// </summary>
        private static string ResolveSymlinks(string path)
        {
            var fullPath = Path.GetFullPath(path);
            var root = Path.GetPathRoot(fullPath)!;

            var resolved = root;
            var components = fullPath[root.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
            foreach (var component in components)
            {
                resolved = Path.Combine(resolved, component);
                if (Directory.Exists(resolved))
                {
                    resolved = new DirectoryInfo(resolved).ResolveLinkTarget(returnFinalTarget: true)?.FullName ?? resolved;
                }
            }

            return resolved;
        }

        /// <summary>The dictionary files sit in a "dict" folder inside the Princeton archives.</summary>
        private static string? FindDictionaryDirectory(string root)
        {
            if (File.Exists(Path.Combine(root, "data.noun")))
            {
                return root;
            }

            var candidates = Directory.GetDirectories(root, "dict", SearchOption.AllDirectories);
            return candidates.FirstOrDefault(directory => File.Exists(Path.Combine(directory, "data.noun")));
        }
    }
}
