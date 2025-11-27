using Microsoft.SqlServer.Dac;

namespace DacFXToolLib
{
    public static class DacPackageScripter
    {
        public static string Script(string dacpacPath, string outputPath)
        {
            // Ensure output directory exists
            Directory.CreateDirectory(outputPath);

            var options = new DacDeployOptions
            {
                CreateNewDatabase = false,
            };

            var databaseName = Path.GetFileNameWithoutExtension(dacpacPath);

            var scriptFileName = $"{databaseName}_Create.sql";

            var scriptFile = Path.Combine(outputPath, scriptFileName);

            using var file = File.Create(scriptFile);

            using (var package = DacPackage.Load(dacpacPath))
            {
                DacServices.GenerateCreateScript(file, package, databaseName, options);
            }

            return scriptFile;
        }
    }
}