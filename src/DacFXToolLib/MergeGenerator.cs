using System.Data;
using System.Text;
using Microsoft.Data.SqlClient;

namespace DacFXToolLib
{
    public static class MergeGenerator
    {
        public static string Generate(string projectPath, string connectionString, string tableName, string schema)
        {
            var script = ReadScript();

            using var connection = new SqlConnection(connectionString);
            connection.Open();
#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities
            using var command = new SqlCommand(script, connection);
#pragma warning restore CA2100 // Review SQL queries for security vulnerabilities
            command.ExecuteNonQuery();

            // Sample: EXEC [#sp_generate_merge] @schema = 'dbo', @table_name = 'Album', @results_to_text = 1, @include_use_db = 0
            using var mergeCommand = new SqlCommand("[#sp_generate_merge]", connection);
            mergeCommand.CommandType = CommandType.StoredProcedure;
            mergeCommand.Parameters.AddWithValue("@table_name", tableName);
            mergeCommand.Parameters.AddWithValue("@schema", schema);
            mergeCommand.Parameters.AddWithValue("@results_to_text", 1);
            mergeCommand.Parameters.AddWithValue("@include_use_db", 0);
            var result = (string?)mergeCommand.ExecuteScalar();

            connection.Close();

            if (result == null)
            {
                throw new InvalidOperationException("Merge script generation failed.");
            }

            var preamble = $@"-- Add the following ItemGroup to your project file to include the merge script in your project:

-- <ItemGroup>
--   <PostDeploy Include=""Post-Deployment/postdeploy.sql"" />
-- </ItemGroup>

--EXEC [#sp_generate_merge] @schema = '{schema}', @table_name = '{tableName}', @results_to_text = 1, @include_use_db = 0
";
            result = preamble + result;

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
