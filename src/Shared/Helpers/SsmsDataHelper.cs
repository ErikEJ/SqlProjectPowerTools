#if SSMS
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SqlProjectsPowerTools
{
    internal class SsmsDataHelper
    {
        internal async Task<Dictionary<string, DatabaseConnectionModel>> GetDataConnectionsAsync()
        {
            // http://www.mztools.com/articles/2007/MZ2007018.aspx
            Dictionary<string, DatabaseConnectionModel> databaseList = new Dictionary<string, DatabaseConnectionModel>();

            Guid providerSqlServerDotNet = new Guid(Resources.SqlServerDotNetProvider);
            Guid providerMicrosoftSqlServerDotNet = new Guid(Resources.MicrosoftSqlServerDotNetProvider);

            ////try
            ////{
            ////    if (dataExplorerConnectionManager?.Connections?.Values != null)
            ////    {
            ////        foreach (var connection in dataExplorerConnectionManager.Connections.Values)
            ////        {
            ////            try
            ////            {
            ////                var sConnectionString = DataProtection.DecryptString(connection.EncryptedConnectionString);
            ////                var info = new DatabaseConnectionModel()
            ////                {
            ////                    ConnectionName = connection.DisplayName,
            ////                    DatabaseType = DatabaseType.Undefined,
            ////                    ConnectionString = sConnectionString,
            ////                    IsFromServerExplorer = true,
            ////                };

            ////                var objProviderGuid = connection.Provider;

            ////                if (objProviderGuid == providerSqlServerDotNet
            ////                    || objProviderGuid == providerMicrosoftSqlServerDotNet)
            ////                {
            ////                    info.DatabaseType = DatabaseType.SQLServer;
            ////                    if (!databaseList.ContainsKey(sConnectionString))
            ////                    {
            ////                        databaseList.Add(sConnectionString, info);
            ////                    }
            ////                }
            ////            }
            ////            catch (Exception ex)
            ////            {
            ////                await VS.MessageBox.ShowErrorAsync($"Could not read connection {connection.DisplayName}: {ex.Message}", "EF Core Power Tools");
            ////            }
            ////        }
            ////    }
            ////}
            ////catch (Exception ex)
            ////{
            ////    await VS.MessageBox.ShowErrorAsync($"Could not read connections: {ex.Message}", "EF Core Power Tools");
            ////}

            return databaseList;
        }
    }
}
#endif
