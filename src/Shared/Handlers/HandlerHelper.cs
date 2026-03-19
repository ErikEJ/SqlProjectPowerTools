using System.IO;
using System.Threading.Tasks;
using DacFXToolLib.Common;
using DacFXToolLib.Dab;

namespace SqlProjectsPowerTools;

public static class HandlerHelper
{
    public static async Task<DatabaseConnectionModel> GetDatabaseInfoAsync(DataApiBuilderOptions options)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

        var dbInfo = new DatabaseConnectionModel();

        dbInfo.DatabaseType = DatabaseType.SQLServerDacpac;
        dbInfo.ConnectionString = $"Data Source=(local);Initial Catalog={Path.GetFileNameWithoutExtension(options.Dacpac)};Integrated Security=true;";
        options.ConnectionString = dbInfo.ConnectionString;
        options.DatabaseType = dbInfo.DatabaseType;

        options.Dacpac = await SqlProjHelper.BuildSqlProjectAsync(options.Dacpac);
        if (string.IsNullOrEmpty(options.Dacpac))
        {
            VSHelper.ShowMessage("Unable to build the database project");
            return null;
        }

        return dbInfo;
    }
}
