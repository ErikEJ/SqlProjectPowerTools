using System.ComponentModel.Design;
using System.Threading.Tasks;

namespace SqlProjectsPowerTools;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "Extension method receiver scopes the API to menu commands.")]
internal static class MenuCommandExtensions
{
    public static async Task<bool> IsEnabledForAnySqlProjectAsync(this MenuCommand command)
    {
        var activeProject = await VS.Solutions.GetActiveProjectAsync();
        return activeProject != null && activeProject.IsAnySqlDatabaseProject();
    }

    public static async Task<bool> IsEnabledForModernSqlProjectAsync(this MenuCommand command)
    {
        var activeProject = await VS.Solutions.GetActiveProjectAsync();
        return activeProject != null && activeProject.IsModernSqlDatabaseProject();
    }

    public static async Task<bool> IsEnabledForMsBuildSdkSqlProjectAsync(this MenuCommand command)
    {
        var activeProject = await VS.Solutions.GetActiveProjectAsync();
        return activeProject != null && activeProject.IsMsBuildSdkSqlDatabaseProject();
    }
}
