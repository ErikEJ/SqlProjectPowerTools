#if SSMS
using System.Collections;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Reflection;
using System.Threading.Tasks;
using DacFXToolLib.Common;

namespace SqlProjectsPowerTools
{
    internal class SsmsDataHelper
    {
        private const string ObjectExplorerServiceTypeName =
            "Microsoft.SqlServer.Management.UI.VSIntegration.ObjectExplorer.IObjectExplorerService";

        private const string NodeInformationTypeName =
            "Microsoft.SqlServer.Management.UI.VSIntegration.ObjectExplorer.INodeInformation";

        private static readonly Dictionary<string, Type> TypeCache = new Dictionary<string, Type>();

        internal async Task<Dictionary<string, DatabaseConnectionModel>> GetDataConnectionsAsync()
        {
            var databaseList = new Dictionary<string, DatabaseConnectionModel>();

            List<string> serverConnectionStrings;

            try
            {
                // Object Explorer APIs must be accessed on the main thread
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                var objectExplorer = GetObjectExplorerService();
                if (objectExplorer == null)
                {
                    return databaseList;
                }

                serverConnectionStrings = new List<string>(GetServerConnectionStrings(objectExplorer));
            }
            catch (Exception ex)
            {
                await VS.MessageBox.ShowErrorAsync(
                    $"Could not read connections from Object Explorer: {ex.Message}",
                    "SQL Database Project Power Tools");
                return databaseList;
            }

            // Switch to a background thread for network I/O so the SSMS UI is not blocked
            await TaskScheduler.Default;

            foreach (var serverConnectionString in serverConnectionStrings)
            {
                try
                {
                    await AddDatabasesFromServerAsync(databaseList, serverConnectionString);
                }
                catch (Exception)
                {
                    // Ignore per-server failures and continue with remaining servers
                }
            }

            return databaseList;
        }

        private static object GetObjectExplorerService()
        {
            var objectExplorerType = FindType(ObjectExplorerServiceTypeName);
            if (objectExplorerType == null)
            {
                return null;
            }

            return Package.GetGlobalService(objectExplorerType);
        }

        private static IEnumerable<string> GetServerConnectionStrings(object objectExplorer)
        {
            var nodeInfoType = FindType(NodeInformationTypeName);
            if (nodeInfoType == null)
            {
                yield break;
            }

            foreach (var hierarchy in GetExplorerHierarchies(objectExplorer))
            {
                if (hierarchy == null)
                {
                    continue;
                }

                var rootProp = hierarchy.GetType().GetProperty("Root");
                var root = rootProp?.GetValue(hierarchy, null);
                if (root == null)
                {
                    continue;
                }

                var serviceProvider = root as IServiceProvider;
                if (serviceProvider == null)
                {
                    continue;
                }

                var nodeInfo = serviceProvider.GetService(nodeInfoType);
                if (nodeInfo == null)
                {
                    continue;
                }

                var connectionProp = nodeInfoType.GetProperty("Connection");
                var connection = connectionProp?.GetValue(nodeInfo, null);
                if (connection == null)
                {
                    continue;
                }

                var connStringProp = connection.GetType().GetProperty("ConnectionString");
                var connectionString = connStringProp?.GetValue(connection, null) as string;
                if (!string.IsNullOrEmpty(connectionString))
                {
                    yield return connectionString;
                }
            }
        }

        private static IEnumerable<object> GetExplorerHierarchies(object objectExplorer)
        {
            var objectExplorerType = objectExplorer.GetType();
            var treeProperty = objectExplorerType.GetProperty(
                "Tree",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var objectTreeControl = treeProperty?.GetValue(objectExplorer, null);

            if (objectTreeControl == null)
            {
                yield break;
            }

            var objTreeType = objectTreeControl.GetType();
            var hierFieldInfo = objTreeType.GetField(
                "hierarchies",
                BindingFlags.Instance | BindingFlags.NonPublic);

            if (hierFieldInfo == null)
            {
                yield break;
            }

            var hierDictionary = hierFieldInfo.GetValue(objectTreeControl) as IEnumerable;
            if (hierDictionary == null)
            {
                yield break;
            }

            foreach (var keyValue in hierDictionary)
            {
                var valueProp = keyValue.GetType().GetProperty("Value");
                yield return valueProp?.GetValue(keyValue, null);
            }
        }

        private static async Task AddDatabasesFromServerAsync(
            Dictionary<string, DatabaseConnectionModel> databaseList,
            string serverConnectionString)
        {
            var builder = new SqlConnectionStringBuilder(serverConnectionString);
            var databaseNames = await GetUserDatabaseNamesAsync(builder);

            foreach (var databaseName in databaseNames)
            {
                builder.InitialCatalog = databaseName;
                var connectionString = builder.ConnectionString;

                if (!databaseList.ContainsKey(connectionString))
                {
                    databaseList.Add(connectionString, new DatabaseConnectionModel
                    {
                        ConnectionName = $"{builder.DataSource}.{databaseName}",
                        ConnectionString = connectionString,
                        DatabaseType = DatabaseType.SQLServer,
                        IsFromServerExplorer = true,
                    });
                }
            }
        }

        private static async Task<List<string>> GetUserDatabaseNamesAsync(SqlConnectionStringBuilder builder)
        {
            var sql = @"SELECT name FROM sys.databases
                WHERE state = 0 AND name NOT IN ('master', 'model', 'tempdb', 'msdb', 'Resource');";

            var result = new List<string>();
            builder.InitialCatalog = "master";
            using (var conn = new SqlConnection(builder.ConnectionString))
            {
                using (var command = new SqlCommand(sql, conn))
                {
                    await conn.OpenAsync();
                    command.CommandTimeout = 30;

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            result.Add(reader[0].ToString());
                        }
                    }
                }
            }

            return result;
        }

        private static Type FindType(string typeName)
        {
            if (TypeCache.TryGetValue(typeName, out var cached))
            {
                return cached;
            }

            Type found = null;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(typeName);
                if (type != null)
                {
                    found = type;
                    break;
                }
            }

            TypeCache[typeName] = found;
            return found;
        }
    }
}
#endif
