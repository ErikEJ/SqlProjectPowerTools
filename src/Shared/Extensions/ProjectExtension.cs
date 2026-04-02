using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using NuGet.ProjectModel;

namespace SqlProjectsPowerTools
{
    internal static class ProjectExtension
    {
        private const string SqlServerRulesPackageId = "ErikEJ.DacFX.SqlServer.Rules";
        private const string TSqlSmellRulesPackageId = "ErikEJ.DacFX.TSQLSmellSCA";
        private const string Indent = "\n    ";
        private const string EndElementIndent = "\n  ";
        private const string NewLine = "\n";

        /// <summary>
        /// Returns true if the project is any SQL Database Project.
        /// </summary>
        /// <param name="project">The project to evaluate</param>
        /// <returns>true if it is a SQL Database Project of any kind</returns>
        public static bool IsAnySqlDatabaseProject(this Project project)
        {
            if (project == null)
            {
                return false;
            }

            return project.FullPath.EndsWith(".sqlproj", StringComparison.OrdinalIgnoreCase)
                || project.IsMsBuildSdkSqlDatabaseProject()
                || project.IsMicrosoftSdkSqlDatabaseProject();
        }

        /// <summary>
        /// Returns true if the project is a modern MsBuild.Sdk.SqlProj or Microsoft.Build.Sql SQL Database Project.
        /// </summary>
        /// <param name="project">The project to evaluate</param>
        /// <returns>true if it is a modern SQL Database Project</returns>
        public static bool IsModernSqlDatabaseProject(this Project project)
        {
            if (project == null)
            {
                return false;
            }

            return project.IsMsBuildSdkSqlDatabaseProject()
                || project.IsMicrosoftSdkSqlDatabaseProject();
        }

        public static bool IsMsBuildSdkSqlDatabaseProject(this Project project)
        {
            if (project == null)
            {
                return false;
            }
#if SSMS
            return false;
#else
            return project.IsCapabilityMatch(VsixPackage.SdkProjCapability);
#endif
        }

        public static async Task<bool> IsInstalledAsync(this Project project, string packageId)
        {
            var projectAssetsFile = await project.GetAttributeAsync("ProjectAssetsFile");

            if (projectAssetsFile != null && File.Exists(projectAssetsFile))
            {
                var lockFile = LockFileUtilities.GetLockFile(projectAssetsFile, NuGet.Common.NullLogger.Instance);

                if (lockFile != null)
                {
                    foreach (var lib in lockFile.Libraries)
                    {
                        if (string.Equals(lib.Name, packageId, StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private static bool IsMicrosoftSdkSqlDatabaseProject(this Project project)
        {
            if (project == null)
            {
                return false;
            }

            return project.IsCapabilityMatch(VsixPackage.MicrosoftSdkCapability);
        }

        public static async Task<string> GetDacpacPathAsync(this Project project)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var assemblyName = await project.GetAttributeAsync("SqlTargetPath");

            if (string.IsNullOrEmpty(assemblyName))
            {
                assemblyName = await project.GetAttributeAsync("TargetPath");
            }

            return assemblyName;
        }

        public static async Task SetPropertyDirectAsync(this Project project, string propertyName, string value)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var projectFilePath = project.FullPath;
            if (!File.Exists(projectFilePath))
            {
                return;
            }

            var doc = XDocument.Load(projectFilePath, LoadOptions.PreserveWhitespace);
            var ns = doc.Root?.Name.Namespace ?? XNamespace.None;

            var existingElement = doc.Descendants(ns + propertyName).FirstOrDefault();

            if (existingElement != null)
            {
                existingElement.Value = value;
            }
            else
            {
                var propertyGroup = doc.Descendants(ns + "PropertyGroup").FirstOrDefault();
                if (propertyGroup == null)
                {
                    propertyGroup = new XElement(ns + "PropertyGroup");
                    doc.Root?.Add(new XText(EndElementIndent));
                    doc.Root?.Add(propertyGroup);
                    doc.Root?.Add(new XText(NewLine));
                }

                var lastElement = propertyGroup.Elements().LastOrDefault();
                if (lastElement != null)
                {
                    lastElement.AddAfterSelf(
                        new XText(Indent),
                        new XElement(ns + propertyName, value));
                }
                else
                {
                    propertyGroup.Add(new XText(Indent));
                    propertyGroup.Add(new XElement(ns + propertyName, value));
                    propertyGroup.Add(new XText(EndElementIndent));
                }
            }

            var settings = new XmlWriterSettings
            {
                OmitXmlDeclaration = doc.Declaration == null,
                Indent = false,
                NewLineOnAttributes = false,
                Encoding = Encoding.UTF8,
            };

            using (var writer = XmlWriter.Create(projectFilePath, settings))
            {
                doc.Save(writer);
            }
        }

        public static async Task<bool> HasRulesPackagesAsync(this Project project)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            if (project == null)
            {
                return false;
            }

            return await project.IsInstalledAsync(SqlServerRulesPackageId)
                || await project.IsInstalledAsync(TSqlSmellRulesPackageId);
        }

        public static async Task<string> AddRulesPackagesAsync(this Project project)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            if (project == null)
            {
                return "No project selected.";
            }

            var projectPath = project.FullPath;
            var projectDirectory = Path.GetDirectoryName(projectPath);
            if (string.IsNullOrWhiteSpace(projectPath) || string.IsNullOrWhiteSpace(projectDirectory) || !File.Exists(projectPath))
            {
                return "Unable to locate the selected project file.";
            }

            foreach (var packageId in new[] { SqlServerRulesPackageId, TSqlSmellRulesPackageId })
            {
                if (await project.IsInstalledAsync(packageId))
                {
                    continue;
                }

                var startInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    WorkingDirectory = projectDirectory,
                    Arguments = $"add \"{projectPath.Replace("\"", "\\\"")}\" package {packageId}",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };

                var result = await ExternalProcessLauncher.RunProcessAsync(startInfo);
                if (result.Contains("Error:", StringComparison.OrdinalIgnoreCase))
                {
                    return $"Failed to install package '{packageId}'.{Environment.NewLine}{result}";
                }

                if (!await project.IsInstalledAsync(packageId))
                {
                    return $"Failed to verify package '{packageId}' after install.";
                }
            }

            return null;
        }

        public static void AddDeployToProject(this Project project, string itemInclude, string section)
        {
            var projectFilePath = project.FullPath;

            if (!File.Exists(projectFilePath))
            {
                return;
            }

            var doc = XDocument.Load(projectFilePath, LoadOptions.PreserveWhitespace);
            var ns = doc.Root?.Name.Namespace ?? XNamespace.None;

            // Check if the deploy item already exists
            var existingDeploy = doc.Descendants(ns + section)
                .FirstOrDefault(e => e.Attribute("Include")?.Value == itemInclude);

            if (existingDeploy != null)
            {
                // Item already exists, no need to add it again
                return;
            }

            // Find an existing ItemGroup with deploy elements, or create a new one
            var itemGroup = doc.Descendants(ns + "ItemGroup")
                .FirstOrDefault(ig => ig.Elements(ns + section).Any());

            if (itemGroup == null)
            {
                // Create a new ItemGroup for deploy item
                itemGroup = new XElement(
                    ns + "ItemGroup",
                    new XText(Indent),
                    new XElement(
                        ns + section,
                        new XAttribute("Include", itemInclude)),
                    new XText(EndElementIndent));

                doc.Root?.Add(new XText(EndElementIndent));
                doc.Root?.Add(itemGroup);
                doc.Root?.Add(new XText(NewLine));
            }
            else
            {
                // Add to existing ItemGroup
                var lastElement = itemGroup.Elements().LastOrDefault();
                if (lastElement != null)
                {
                    lastElement.AddAfterSelf(
                        new XText(Indent),
                        new XElement(
                            ns + section,
                            new XAttribute("Include", itemInclude)));
                }
            }

            // Save with proper settings to preserve formatting
            var settings = new XmlWriterSettings
            {
                OmitXmlDeclaration = doc.Declaration == null,
                Indent = false,
                NewLineOnAttributes = false,
                Encoding = Encoding.UTF8,
            };

            using (var writer = XmlWriter.Create(projectFilePath, settings))
            {
                doc.Save(writer);
            }
        }

        public static async Task<(bool Run, string Rules, string SqlVersion)> IsInSqlProjAsync(this Project project)
        {
            var rulesExpression = await project.GetAttributeAsync("SqlCodeAnalysisRules")
                ?? await project.GetAttributeAsync("CodeAnalysisRules")
                ?? string.Empty;
            var runCodeAnalysisValue = await project.GetAttributeAsync("RunSqlCodeAnalysis") ?? string.Empty;
            var runCodeAnalysis = string.Equals(runCodeAnalysisValue, "True", StringComparison.OrdinalIgnoreCase);

            var serverVersion = await project.GetSqlServerVersionAsync();

            return (runCodeAnalysis, rulesExpression, serverVersion);
        }

        public static async Task<string> GetSqlServerVersionAsync(this Project project)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            // MsBuild.Sdk.SqlProj uses SqlServerVersion directly (e.g. "Sql160")
            var sqlServerVersion = await project.GetAttributeAsync("SqlServerVersion") ?? string.Empty;
            if (!string.IsNullOrEmpty(sqlServerVersion))
            {
                return sqlServerVersion;
            }

            // Classic .sqlproj / Microsoft.Build.Sql uses DSP property
            var dsp = await project.GetAttributeAsync("DSP") ?? string.Empty;
            var version = ParseVersionFromDsp(dsp);
            return version ?? "Sql160";
        }

        private static string ParseVersionFromDsp(string dsp)
        {
            if (string.IsNullOrEmpty(dsp))
            {
                return null;
            }

            var trimmedDsp = dsp.Replace("V12", string.Empty);

            // Extract version from e.g. "Microsoft.Data.Tools.Schema.Sql.Sql160DatabaseSchemaProvider"
            // Microsoft.Data.Tools.Schema.Sql.SqlAzureV12DatabaseSchemaProvider (Azure SQL Database)
            var marker = "DatabaseSchemaProvider";
            var markerIndex = trimmedDsp.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (markerIndex <= 0)
            {
                return null;
            }

            var beforeMarker = trimmedDsp.Substring(0, markerIndex);
            var lastDot = beforeMarker.LastIndexOf('.');
            if (lastDot < 0 || lastDot >= beforeMarker.Length - 1)
            {
                return null;
            }

            return beforeMarker.Substring(lastDot + 1);
        }
    }
}
