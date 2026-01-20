using System.IO;
using System.Text;

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

                project.AddDeployToProject("Post-Deployment/Script.PostDeployment.sql", "PostDeploy");
            }

            var preDeployFilePath = Path.Combine(projectDirectory, "Pre-Deployment", "Script.PreDeployment.sql");

            if (!File.Exists(preDeployFilePath))
            {
                if (!Directory.Exists(Path.GetDirectoryName(preDeployFilePath)))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(preDeployFilePath)!);
                }

                File.WriteAllText(preDeployFilePath, preStandardText, Encoding.UTF8);

                project.AddDeployToProject("Pre-Deployment/Script.PreDeployment.sql", "PreDeploy");
            }
        }
    }
}
