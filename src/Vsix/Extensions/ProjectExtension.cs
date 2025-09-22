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
                || project.IsCapabilityMatch(VsixPackage.SdkProjCapability);
        }
    }
}
