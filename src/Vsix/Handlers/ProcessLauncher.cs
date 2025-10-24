using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using DacFXToolLib.Common;

namespace SqlProjectsPowerTools
{
    public class ProcessLauncher
    {
        private readonly string toolFolder;
        private readonly string toolRoot;
        private readonly string exeName;
        private readonly string zipName;

        public ProcessLauncher()
        {
            var versionSuffix = FileVersionInfo.GetVersionInfo(typeof(VsixPackage).Assembly.Location).FileVersion;
            toolFolder = $"dacfxtool.";
            exeName = $"dacfxtool.dll";
            zipName = $"dacfxtool.exe.zip";
            toolRoot = toolFolder;
            toolFolder += versionSuffix;
        }

        public async Task<List<TableModel>> GetTablesAsync(string connectionString, DatabaseType databaseType, SchemaInfo[] schemas, bool mergeDacpacs)
        {
            var arguments = mergeDacpacs.ToString() + " " + ((int)databaseType).ToString() + " \"" + connectionString.Replace("\"", "\\\"") + "\"";

            if (schemas != null)
            {
                arguments += $" \"{string.Join(",", schemas.Select(s => s.Name.Replace("\"", "\\\"")))}\"";
            }

            return await GetTablesInternalAsync(arguments);
        }

        public async Task<string> GetDiagramAsync(string connectionString, string optionsPath, List<string> schemaList)
        {
            var option = "dgml ";

            var arguments = option + " \"" + optionsPath.Replace("\"", "\\\"") + "\" " + " \"" + connectionString.Replace("\"", "\\\"") + "\" \"" + string.Join(",", schemaList) + "\"";

            if (schemaList.Count == 0)
            {
                arguments = option + " \"" + optionsPath.Replace("\"", "\\\"") + "\" " + " \"" + connectionString.Replace("\"", "\\\"") + "\"";
            }

            var filePath = await GetDiagramInternalAsync(arguments);

            return filePath;
        }

        public async Task<string> GetImportAsync(int fileGenerationMode, string optionsPath, string connectionString)
        {
            var option = "import ";

            var arguments = option + fileGenerationMode.ToString() + " \"" + optionsPath.Replace("\"", "\\\"") + "\" " + " \"" + connectionString.Replace("\"", "\\\"") + "\"";

            var filePath = await GetDiagramInternalAsync(arguments);

            return filePath;
        }

        public async Task<string> GetCompareAsync(bool databaseIsSource, string dacpacPath, string connectionString)
        {
            var option = "compare ";

            var arguments = option + databaseIsSource.ToString() + " \"" + dacpacPath.Replace("\"", "\\\"") + "\" " + " \"" + connectionString.Replace("\"", "\\\"") + "\"";

            var filePath = await GetDiagramInternalAsync(arguments);

            return filePath;
        }

        public async Task<string> GetDatabaseSettingsAsync(string connectionString)
        {
            var option = "getdboptions ";

            var arguments = option + " \"" + connectionString.Replace("\"", "\\\"") + "\"";

            var filePath = await GetDiagramInternalAsync(arguments);

            return filePath;
        }

        public async Task<string> GetErDiagramAsync(string optionsPath, string connectionString)
        {
            var arguments = "erdiagram " + " \"" + optionsPath.Replace("\"", "\\\"") + "\" " + " \"" + connectionString.Replace("\"", "\\\"") + "\" ";

            var filePath = await GetDiagramInternalAsync(arguments);

            return filePath;
        }

        public async Task<string> GetUnpackAsync(string optionsPath, string connectionString)
        {
            var arguments = "unpack " + " \"" + optionsPath.Replace("\"", "\\\"") + "\" " + " \"" + connectionString.Replace("\"", "\\\"") + "\" ";

            var filePath = await GetDiagramInternalAsync(arguments);

            return filePath;
        }

        public async Task<string> GetReportPathAsync(string path)
        {
            var option = "dacpacreport ";

            var arguments = option + " \"" + path.Replace("\"", "\\\"") + "\"";

            var filePath = await GetDiagramInternalAsync(arguments);

            return filePath;
        }

        public async Task<string> GetDabConfigPathAsync(string optionsPath, string connectionString)
        {
            var arguments = "dabbuilder " + " \"" + optionsPath.Replace("\"", "\\\"") + "\" " + " \"" + connectionString.Replace("\"", "\\\"") + "\" ";

            var filePath = await GetDiagramInternalAsync(arguments);

            return filePath;
        }

        private static async Task<string> RunProcessAsync(ProcessStartInfo startInfo)
        {
            return await ExternalProcessLauncher.RunProcessAsync(startInfo);
        }

        private async Task<string> GetDiagramInternalAsync(string arguments)
        {
            var startInfo = await CreateStartInfoAsync(arguments);

            try
            {
                File.WriteAllText(Path.Combine(Path.GetTempPath(), "dacfxtoolparams.txt"), startInfo.Arguments, Encoding.UTF8);
            }
            catch
            {
                // Ignore
            }

            var standardOutput = await RunProcessAsync(startInfo);

            return ResultDeserializer.BuildDiagramResult(standardOutput);
        }

        private async Task<List<TableModel>> GetTablesInternalAsync(string arguments)
        {
            var startInfo = await CreateStartInfoAsync(arguments);

            var standardOutput = await RunProcessAsync(startInfo);

            return ResultDeserializer.BuildTableResult(standardOutput);
        }

        private async Task<ProcessStartInfo> CreateStartInfoAsync(string arguments)
        {
            string version = "8.0";

            if (!await IsDotnetInstalledAsync(version))
            {
                throw new InvalidOperationException($"Reverse engineer error: Unable to launch 'dotnet' version {version}. Do you have the runtime installed? Check with 'dotnet --list-runtimes'");
            }

            var launchPath = DropNetCoreFiles();

            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"\"{launchPath}\" {arguments}",
            };
            return startInfo;
        }

        private async Task<bool> IsDotnetInstalledAsync(string version)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = "--list-runtimes",
            };

            var result = await RunProcessAsync(startInfo);

            if (string.IsNullOrWhiteSpace(result))
            {
                return false;
            }

            var sdks = result.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries).ToList();

            return sdks.Exists(s => s.StartsWith($"Microsoft.NETCore.App {version}.", StringComparison.OrdinalIgnoreCase));
        }

        private string DropNetCoreFiles()
        {
            var toDir = Path.Combine(Path.GetTempPath(), toolFolder);
            var fromDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            Debug.Assert(fromDir != null, nameof(fromDir) + " != null");
            Debug.Assert(toDir != null, nameof(toDir) + " != null");

            var fullPath = Path.Combine(toDir, exeName);

            if (Directory.Exists(toDir)
                && File.Exists(fullPath)
                && Directory.EnumerateFiles(toDir, "*", SearchOption.TopDirectoryOnly).Count() >= 97)
            {
                return fullPath;
            }

            if (Directory.Exists(toDir))
            {
                Directory.Delete(toDir, true);
            }

            Directory.CreateDirectory(toDir);

            using (var archive = ZipFile.Open(Path.Combine(fromDir, zipName), ZipArchiveMode.Read))
            {
                archive.ExtractToDirectory(toDir, true);
            }

            var dirs = Directory.GetDirectories(Path.GetTempPath(), toolRoot + "*");

            foreach (var dir in dirs.Where(dir => !dir.Equals(toDir)))
            {
                try
                {
                    Directory.Delete(dir, true);
                }
                catch
                {
                    // Ignore
                }
            }

            return fullPath;
        }
    }
}