using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace SqlProjectsPowerTools
{
    internal static class ProjectExtension
    {
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

            return project.IsCapabilityMatch(VsixPackage.SdkProjCapability);
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
    }
}
