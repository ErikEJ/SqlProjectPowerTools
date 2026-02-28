using System.Collections.Generic;
using System.Globalization;
using System.Text;
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
                deploymentScript = scriptResult.Success ? scriptResult.Script : "-- Script generation failed: " + scriptResult.Message;
            }
            else
            {
                deploymentScript = "-- Deployment script not available when database is source";
            }

            return new VisualCompareResult
            {
                Differences = differences.ToArray(),
                DeploymentScript = deploymentScript,
            };
        }

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
