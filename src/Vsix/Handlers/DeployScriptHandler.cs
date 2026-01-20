using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace SqlProjectsPowerTools
{
    internal static class DeployScriptHandler
    {
        public static async Task AddDeploymentScriptsAsync(Project project)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            try
            {
                if (await VSHelper.IsDebugModeAsync())
                {
                    VSHelper.ShowError("Cannot add deployment scripts while debugging");
                    return;
                }

                await GenerateScriptAsync(project);
            }
            catch (AggregateException ae)
            {
                foreach (var innerException in ae.Flatten().InnerExceptions)
                {
                    VSHelper.ShowError(innerException.Message);
                }
            }
            catch (Exception exception)
            {
                VSHelper.ShowError(exception.Message);
            }
            finally
            {
                await VS.StatusBar.ClearAsync();
            }
        }

        private static async Task GenerateScriptAsync(Project project)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var projectDirectory = Path.GetDirectoryName(project.FullPath);

            var preStandardText = """                
/*
 Pre-Deployment Script Template
--------------------------------------------------------------------------------------
 This file contains SQL statements that will be executed before the build script.
 Use SQLCMD syntax to include a file in the pre-deployment script.
 Example:      :r .\myfile.sql
 Use SQLCMD syntax to reference a variable in the pre-deployment script.
 Example:      :setvar TableName MyTable
               SELECT * FROM [$(TableName)]
--------------------------------------------------------------------------------------
*/
""";

            var postStandardText = """                
/*
Post-Deployment Script Template
--------------------------------------------------------------------------------------
    This file contains SQL statements that will be appended to the build script.
    Use SQLCMD syntax to include a file in the post-deployment script.
    Example:      :r .\myfile.sql
    Use SQLCMD syntax to reference a variable in the post-deployment script.
    Example:      :setvar TableName MyTable
                SELECT * FROM [$(TableName)]
--------------------------------------------------------------------------------------
*/
""";

            var postDeployFilePath = Path.Combine(projectDirectory, "Post-Deployment", "Script.PostDeployment.sql");

            if (!File.Exists(postDeployFilePath))
            {
                if (!Directory.Exists(Path.GetDirectoryName(postDeployFilePath)))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(postDeployFilePath)!);
                }

                File.WriteAllText(postDeployFilePath, postStandardText, Encoding.UTF8);

                AddDeployToProject(project.FullPath, "Post-Deployment/Script.PostDeployment.sql", "PostDeploy");
            }

            var preDeployFilePath = Path.Combine(projectDirectory, "Pre-Deployment", "Script.PreDeployment.sql");

            if (!File.Exists(preDeployFilePath))
            {
                if (!Directory.Exists(Path.GetDirectoryName(preDeployFilePath)))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(preDeployFilePath)!);
                }

                File.WriteAllText(preDeployFilePath, preStandardText, Encoding.UTF8);

                AddDeployToProject(project.FullPath, "Pre-Deployment/Script.PreDeployment.sql", "PreDeploy");
            }
        }

        private static void AddDeployToProject(string projectFilePath, string itemInclude, string section)
        {
            if (!File.Exists(projectFilePath))
            {
                return;
            }

            var doc = XDocument.Load(projectFilePath);
            var ns = doc.Root?.Name.Namespace ?? XNamespace.None;

            // Check if the item already exists
            var existingDeploy = doc.Descendants(ns + section)
                .FirstOrDefault(e => e.Attribute("Include")?.Value == itemInclude);

            if (existingDeploy != null)
            {
                // Item already exists, no need to add it again
                return;
            }

            // Find an existing ItemGroup with elements of this section type, or create a new one
            var itemGroup = doc.Descendants(ns + "ItemGroup")
                .FirstOrDefault(ig => ig.Elements(ns + section).Any());

            const string indent = "  ";
            const string itemIndent = "    ";

            if (itemGroup == null)
            {
                // Create a new ItemGroup
                itemGroup = new XElement(ns + "ItemGroup");

                // Add blank line and indent before the new ItemGroup
                doc.Root?.Add(new XText($"\n\n{indent}"));
                doc.Root?.Add(itemGroup);
                doc.Root?.Add(new XText("\n"));
            }

            // Add newline and indent before the element
            itemGroup.Add(new XText($"\n{itemIndent}"));

            // Add the deploy item
            var deployElement = new XElement(ns + section);
            deployElement.SetAttributeValue("Include", itemInclude);
            itemGroup.Add(deployElement);

            // Add newline after the element
            itemGroup.Add(new XText($"\n{indent}"));

            // Save with proper formatting
            var settings = new XmlWriterSettings
            {
                OmitXmlDeclaration = true,
                Indent = true,
                IndentChars = indent,
                Encoding = new UTF8Encoding(false),
            };

            using (var writer = XmlWriter.Create(projectFilePath, settings))
            {
                doc.Save(writer);
            }
        }
    }
}
