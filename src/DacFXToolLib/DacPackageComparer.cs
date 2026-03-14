using DacFXToolLib.Common;
using Microsoft.Data.SqlClient;
using Microsoft.SqlServer.Dac.Compare;

namespace DacFXToolLib
{
    public static class DacPackageComparer
    {
        public static VisualCompareResult CompareVisual(string dacpacPath, string connectionString, bool databaseIsSource)
        {
            var dacpac = new SchemaCompareDacpacEndpoint(dacpacPath);
            var database = new SchemaCompareDatabaseEndpoint(connectionString);

            var comparison = databaseIsSource
                ? new SchemaComparison(database, dacpac)
                : new SchemaComparison(dacpac, database);
            var compareResult = comparison.Compare();

            if (!compareResult.IsValid)
            {
                throw new InvalidOperationException("Schema comparison failed to complete.");
            }

            var differences = new List<SchemaDifferenceModel>();

            foreach (var diff in compareResult.Differences)
            {
                var name = diff.Name;
                var objectType = diff.SourceObject?.ObjectType?.Name ?? diff.TargetObject?.ObjectType?.Name ?? "Unknown";
                var differenceType = diff.DifferenceType.ToString();
                var updateAction = diff.UpdateAction.ToString();
                var targetName = diff.TargetObject?.Name?.ToString() ?? string.Empty;
                var sourceName = diff.SourceObject?.Name?.ToString() ?? string.Empty;

                var sourceScript = compareResult.GetDiffEntrySourceScript(diff) ?? string.Empty;
                var targetScript = compareResult.GetDiffEntryTargetScript(diff) ?? string.Empty;

                differences.Add(new SchemaDifferenceModel
                {
                    Name = name,
                    ObjectType = objectType,
                    DifferenceType = differenceType,
                    UpdateAction = updateAction,
                    SourceScript = sourceScript,
                    TargetScript = targetScript,
                    TargetObjectName = targetName,
                    SourceObjectName = sourceName,
                });
            }

            string deploymentScript;
            if (compareResult.IsEqual)
            {
                deploymentScript = "-- No differences found";
            }
            else if (!databaseIsSource)
            {
                var databaseName = new SqlConnectionStringBuilder(connectionString).InitialCatalog;
                var scriptResult = compareResult.GenerateScript(databaseName);

                if (!scriptResult.Success)
                {
                    if (scriptResult.Exception != null)
                    {
                        throw scriptResult.Exception;
                    }

                    throw new InvalidOperationException($"Script generation failed: {scriptResult.Message}");
                }

                deploymentScript = scriptResult.Script;
            }
            else
            {
                deploymentScript = "-- Deployment script not available when database is source";
            }

            return new VisualCompareResult
            {
                Differences = differences,
                DeploymentScript = deploymentScript,
            };
        }
    }
}