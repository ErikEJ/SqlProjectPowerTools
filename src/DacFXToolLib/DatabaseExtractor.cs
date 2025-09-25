using Microsoft.Data.SqlClient;
using Microsoft.SqlServer.Dac;

namespace DacFXToolLib
{
    public class DatabaseExtractor
    {
        private readonly DacServices dac;
        private readonly SqlConnectionStringBuilder builder;

        public DatabaseExtractor(string connectionString)
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
    }
}