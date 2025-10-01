using System.Threading.Tasks;

namespace SqlProjectsPowerTools
{
    internal static class ProjectExtension
    {
        public static bool IsSqlDatabaseProject(this Project project)
        {
            if (project == null)
            {
                return false;
            }

            return project.FullPath.EndsWith(".sqlproj", StringComparison.OrdinalIgnoreCase)
                || project.IsMsBuildSdkSqlDatabaseProject()
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

        public static bool IsMicrosoftSdkSqlDatabaseProject(this Project project)
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
