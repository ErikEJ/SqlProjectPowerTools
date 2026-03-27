using System.Data;
using System.Text;
using Microsoft.Data.SqlClient;

namespace DacFXToolLib
{
    public static class MergeGenerator
    {
        public static string Generate(string projectPath, string connectionString, string tableName, string schema)
        {
            ArgumentNullException.ThrowIfNull(projectPath);
            ArgumentNullException.ThrowIfNull(connectionString);
            ArgumentNullException.ThrowIfNull(tableName);
            ArgumentNullException.ThrowIfNull(schema);

            var script = ReadScript();

            const string parameterOverrides = @"SET @table_name = @p_table_name
SET @schema = @p_schema
SET @results_to_text = 1
SET @include_use_db = 0";

            script = script.Replace("-- {PARAMETER_OVERRIDES}", parameterOverrides, StringComparison.Ordinal);

            using var connection = new SqlConnection(connectionString);
            connection.Open();
#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities
            using var mergeCommand = new SqlCommand(script, connection);
#pragma warning restore CA2100 // Review SQL queries for security vulnerabilities
            mergeCommand.Parameters.Add(new SqlParameter("@p_table_name", SqlDbType.NVarChar, 776) { Value = tableName });
            mergeCommand.Parameters.Add(new SqlParameter("@p_schema", SqlDbType.NVarChar, 64) { Value = schema });
            var result = (string?)mergeCommand.ExecuteScalar();

            connection.Close();

            if (result == null)
            {
                throw new InvalidOperationException("Merge script generation failed.");
            }

            var safeSchema = schema.Replace("'", "''", StringComparison.Ordinal).ReplaceLineEndings(string.Empty);
            var safeTableName = tableName.Replace("'", "''", StringComparison.Ordinal).ReplaceLineEndings(string.Empty);
            var preamble = $@"-- @schema = '{safeSchema}', @table_name = '{safeTableName}', @results_to_text = 1, @include_use_db = 0
";
            result = preamble + result;

            result = result.ReplaceLineEndings();

            return WriteResult(projectPath, tableName, result);
        }

        private static string WriteResult(string projectPath, string tableName, string result)
        {
            var projectDirectory = Path.GetDirectoryName(projectPath);

            if (!Directory.Exists(Path.Join(projectDirectory, "Post-Deployment")))
            {
                Directory.CreateDirectory(Path.Join(projectDirectory, "Post-Deployment"));
            }

            var outputPath = Path.Combine(Path.GetDirectoryName(projectPath)!, "Post-Deployment", $"{tableName}_merge.sql");

            if (File.Exists(outputPath))
            {
                throw new InvalidOperationException($"The file {outputPath} already exists.");
            }

            File.WriteAllText(outputPath, result.ToString(), Encoding.UTF8);

            return outputPath;
        }

        private static string ReadScript()
        {
            var resourceName = "DacFXToolLib.sp_generate_merge.sql";
            using var stream = System.Reflection.Assembly.GetAssembly(typeof(MergeGenerator))!.GetManifestResourceStream(resourceName);
            using var reader = new StreamReader(stream!);
            var sql = reader.ReadToEnd();

            if (string.IsNullOrWhiteSpace(sql))
            {
                throw new InvalidOperationException("Could not load the embedded SQL resource.");
            }

            return sql;
        }
    }
}