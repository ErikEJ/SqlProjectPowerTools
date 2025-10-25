using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.SqlServer.Dac;
using Microsoft.SqlServer.Dac.Model;

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
    }
}
