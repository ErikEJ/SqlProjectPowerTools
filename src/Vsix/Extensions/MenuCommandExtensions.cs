using System.ComponentModel.Design;
using System.Threading.Tasks;

namespace SqlProjectsPowerTools
{
    internal static class MenuCommandExtensions
    {
        public static async Task<bool> IsEnabledForAnySqlProjectAsync(this MenuCommand command)
        {
            var activeProject = await VS.Solutions.GetActiveProjectAsync();
            if (activeProject == null)
            {
                return false;
            }

            return activeProject.IsAnySqlDatabaseProject();
        }

        public static async Task<bool> IsEnabledForModernSqlProjectAsync(this MenuCommand command)
        {
            var activeProject = await VS.Solutions.GetActiveProjectAsync();
            if (activeProject == null)
            {
                return false;
            }

            return activeProject.IsModernSqlDatabaseProject();
        }
    }
}
