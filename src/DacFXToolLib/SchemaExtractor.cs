using System.Reflection;
using System.Text;
using System.Text.Json;
using DacFXToolLib.Common;
using Microsoft.Data.SqlClient;
using Microsoft.SqlServer.Dac;
using Microsoft.SqlServer.Dac.Model;
using ObjectType = DacFXToolLib.Common.ObjectType;

namespace DacFXToolLib
{
    public class SchemaExtractor
    {
        private readonly DacServices dac;
        private readonly SqlConnectionStringBuilder builder;

        public SchemaExtractor(string connectionString)
        {
            dac = new DacServices(connectionString);
            builder = new SqlConnectionStringBuilder(connectionString);
        }

        public void Extract(string outputPath, DacExtractTarget target)
        {
            var options = new DacExtractOptions
            {
                ExtractTarget = target,
                CommandTimeout = 300,
            };

            dac.Extract(outputPath, builder.InitialCatalog, "SQL Database Projects Power Tools", new Version(1, 0, 0, 0), extractOptions: options);
        }

        public string GetDatabaseOptions()
        {
            var modelExtractOptions = new ModelExtractOptions
            {
                ExtractReferencedServerScopedElements = false,
                ExtractUsageProperties = false,
                ExtractApplicationScopedObjectsOnly = true,
                IgnoreExtendedProperties = true,
                IgnorePermissions = true,
                IgnoreUserLoginMappings = true,
                VerifyExtraction = false,
            };

            var model = TSqlModel.LoadFromDatabase(builder.ConnectionString, modelExtractOptions);

            var modelOptions = model.CopyModelOptions();

            var dict = new Dictionary<string, string?>();

            var properties = modelOptions.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public);

            foreach (var prop in properties)
            {
                try
                {
                    // Some properties throw NotSupportedException when accessed
                    var value = prop.GetValue(modelOptions, null);
                    dict[prop.Name] = value?.ToString() ?? null;
                }
                catch (NotSupportedException)
                {
                    // Ignore properties that are not supported
                }
            }

            var optionsJson = JsonSerializer.Serialize(dict);

            var tempFile = Path.Join(Path.GetTempPath(), Path.GetRandomFileName() + ".json");
            File.WriteAllText(tempFile, optionsJson, Encoding.UTF8);

            return tempFile;
        }

        public List<TableModel> GetTables()
        {
            var tables = new List<TableModel>();

            using var connection = new SqlConnection(builder.ConnectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = "SELECT TABLE_SCHEMA, TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE' ORDER BY TABLE_SCHEMA, TABLE_NAME;";

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var schema = reader.GetString(0);
                var name = reader.GetString(1);
                tables.Add(new TableModel(name, schema, DatabaseType.SQLServer, ObjectType.Table, []));
            }

            return tables;
        }
    }
}