using System.Threading.Tasks;

namespace SqlProjectsPowerTools
{
    internal static class ProjectExtension
    {
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
    }
}
