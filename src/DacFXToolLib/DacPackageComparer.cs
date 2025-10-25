using System.Globalization;
using System.Text;
using Microsoft.Data.SqlClient;
using Microsoft.SqlServer.Dac.Compare;

namespace DacFXToolLib
{
    public static class DacPackageComparer
    {
        public static string Compare(string dacpacPath, string connectionString, bool databaseIsSource)
        {
            var databaseName = new SqlConnectionStringBuilder(connectionString).InitialCatalog;

            var dacpac = new SchemaCompareDacpacEndpoint(dacpacPath);
            var database = new SchemaCompareDatabaseEndpoint(connectionString);

            var comparison = databaseIsSource
                ? new SchemaComparison(database, dacpac)
                : new SchemaComparison(dacpac, database);
            var compareResult = comparison.Compare();

            var errors = string.Empty;

            if (compareResult.GetErrors().Any())
            {
                errors = string.Join(Environment.NewLine, compareResult.GetErrors().Select(e => "--" + e.Message));
            }

            if (!compareResult.IsValid)
            {
                throw new InvalidOperationException("Schema comparison failed to complete.");
            }

            if (compareResult.IsEqual)
            {
                return string.Empty;
            }

            if (!databaseIsSource)
            {
                var result = compareResult.GenerateScript(databaseName);

                if (!result.Success)
                {
                    if (result.Exception != null)
                    {
                        throw result.Exception;
                    }

                    throw new InvalidOperationException($"Script generation encountered errors: {result.Message}");
                }

                return errors + Environment.NewLine + result.Script;
            }
            else
            {
                var diffScript = new StringBuilder();

                diffScript.Append(string.Empty);

                foreach (var difference in compareResult.Differences)
                {
                    diffScript.AppendLine();
                    diffScript.AppendLine(CultureInfo.InvariantCulture, $"-- Difference: {difference.SourceObject.Name} ({difference.SourceObject.ObjectType.Name})");
                    diffScript.AppendLine(compareResult.GetDiffEntrySourceScript(difference));
                }

                return diffScript.ToString();
            }
        }
    }
}
