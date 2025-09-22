using System.ComponentModel.Design;
using System.Threading.Tasks;

namespace SqlProjectsPowerTools
{
    internal static class MenuCommandExtensions
    {
        public static async Task<bool> IsEnabledForSqlProjectAsync(this MenuCommand command)
        {
            var activeProject = await VS.Solutions.GetActiveProjectAsync();
            if (activeProject == null)
            {
                return false;
            }

            return activeProject.IsSqlDatabaseProject();
        }

    }
}
