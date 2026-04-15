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
                    dict[prop.Name] = GetModelOptionValue(prop.Name, value);
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

        private static string? GetModelOptionValue(string propertyName, object? value)
        {
            // TODO: Remove this special-case conversion when DacFX returns On/Off for these options.
            if ((propertyName is "DbScopedConfigLegacyCardinalityEstimation" or "DbScopedConfigParameterSniffing")
                && value is bool boolValue)
            {
                return boolValue ? "On" : "Off";
            }

            return value?.ToString();
        }

        public List<TableModel> GetTables()
        {
            var tables = new List<TableModel>();
            var primaryKeyColumns = GetPrimaryKeyColumns();

            using var connection = new SqlConnection(builder.ConnectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = "SELECT TABLE_SCHEMA, TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE' ORDER BY TABLE_SCHEMA, TABLE_NAME;";

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var schema = reader.GetString(0);
                var name = reader.GetString(1);
                var tableKey = $"{schema}.{name}";

                var columns = primaryKeyColumns.TryGetValue(tableKey, out var pkColumns)
                    ? pkColumns
                    : [];

                tables.Add(new TableModel(name, schema, DatabaseType.SQLServer, ObjectType.Table, columns));
            }

            return tables;
        }

        private Dictionary<string, List<ColumnModel>> GetPrimaryKeyColumns()
        {
            var result = new Dictionary<string, List<ColumnModel>>(StringComparer.OrdinalIgnoreCase);

            using var connection = new SqlConnection(builder.ConnectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT 
                    s.name AS SchemaName,
                    t.name AS TableName,
                    c.name AS ColumnName,
                    ty.name AS DataType
                FROM sys.indexes i
                INNER JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
                INNER JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
                INNER JOIN sys.tables t ON i.object_id = t.object_id
                INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
                INNER JOIN sys.types ty ON c.user_type_id = ty.user_type_id
                WHERE i.is_primary_key = 1
                ORDER BY s.name, t.name, ic.key_ordinal;";

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var schema = reader.GetString(0);
                var tableName = reader.GetString(1);
                var columnName = reader.GetString(2);
                var dataType = reader.GetString(3);
                var tableKey = $"{schema}.{tableName}";

                if (!result.TryGetValue(tableKey, out var columns))
                {
                    columns = [];
                    result[tableKey] = columns;
                }

                columns.Add(new ColumnModel(columnName, dataType, isPrimaryKey: true, isForeignKey: false));
            }

            return result;
        }
    }
}
