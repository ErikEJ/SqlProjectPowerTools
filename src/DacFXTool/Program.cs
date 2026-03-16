using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using DacFXToolLib;
using DacFXToolLib.Common;
using DacFXToolLib.Dab;
using Microsoft.SqlServer.Dac;

[assembly: CLSCompliant(true)]
[assembly: SuppressMessage("Reliability", "CA2007:Consider calling ConfigureAwait on the awaited task", Justification = "Reviewed")]

namespace DacFXTool
{
    internal static class Program
    {
        public static async System.Threading.Tasks.Task<int> Main(string[] args)
        {
            try
            {
                Console.OutputEncoding = Encoding.UTF8;

                ArgumentNullException.ThrowIfNull(args);

                if (args.Length > 0)
                {
                    if (args.Length == 2
                        && args[0] == "dacpacreport"
                        && new FileInfo(args[1]).Exists)
                    {
                        var builder = new DacpacReportBuilder(new FileInfo(args[1]));

                        var buildResult = builder.BuildReport();

                        await Console.Out.WriteLineAsync("Result:");
                        await Console.Out.WriteLineAsync(buildResult);

                        return 0;
                    }

                    // getobjects "<dacpac path>"
                    if (args.Length == 2
                        && args[0] == "getobjects")
                    {
                        if (!new FileInfo(args[1]).Exists)
                        {
                            await Console.Out.WriteLineAsync("Error:");
                            await Console.Out.WriteLineAsync($"DACPAC file '{args[1]}' not found");
                            return 1;
                        }

                        var objects = DacpacModelFactory.GetObjects(args[1]);

                        await Console.Out.WriteLineAsync("Result:");
                        await Console.Out.WriteLineAsync(objects.Write());

                        return 0;
                    }

                    // erdiagram <options file> <connection string>
                    if (args.Length == 3
                        && args[0] == "erdiagram"
                        && new FileInfo(args[1]).Exists)
                    {
                        var dabOptions = DataApiBuilderOptionsExtensions.TryRead(args[1]);

                        if (dabOptions == null)
                        {
                            await Console.Out.WriteLineAsync("Error:");
                            await Console.Out.WriteLineAsync("Could not read options");
                            return 1;
                        }

                        dabOptions.ConnectionString = args[2];

                        var builder = new ErDiagramBuilder(dabOptions);

                        var buildResult = builder.GetErDiagramFileName(dabOptions.Optional);

                        await Console.Out.WriteLineAsync("Result:");
                        await Console.Out.WriteLineAsync(buildResult);

                        return 0;
                    }

                    if (args.Length == 3
                        && args[0] == "dabbuilder"
                        && new FileInfo(args[1]).Exists)
                    {
                        var dabOptions = DataApiBuilderOptionsExtensions.TryRead(args[1]);

                        if (dabOptions == null)
                        {
                            await Console.Out.WriteLineAsync("Error:");
                            await Console.Out.WriteLineAsync("Could not read options");
                            return 1;
                        }

                        dabOptions.ConnectionString = args[2];

                        var builder = new DabBuilder(dabOptions);

                        var buildResult = builder.GetDabConfigCmdFile();

                        await Console.Out.WriteLineAsync("Result:");
                        await Console.Out.WriteLineAsync(buildResult);

                        return 0;
                    }

                    // import 5 "path" "connection string"
                    if (args.Length == 4
                        && args[0] == "import")
                    {
                        var importer = new SchemaExtractor(args[3]);

                        var target = int.TryParse(args[1], out int targetType) ? (DacExtractTarget?)targetType : null;

                        if (target == null)
                        {
                            await Console.Out.WriteLineAsync("Error:");
                            await Console.Out.WriteLineAsync($"Invalid target type '{args[1]}'");
                            return 1;
                        }

                        importer.Extract(args[2], target.Value);

                        await Console.Out.WriteLineAsync("Result:");
                        await Console.Out.WriteLineAsync("OK");

                        return 0;
                    }

                    // getdboptions "connection string"
                    if (args.Length == 2
                        && args[0] == "getdboptions")
                    {
                        var importer = new SchemaExtractor(args[1]);

                        var optionsPath = importer.GetDatabaseOptions();

                        await Console.Out.WriteLineAsync("Result:");
                        await Console.Out.WriteLineAsync(optionsPath);

                        return 0;
                    }

                    // unpack "dacpac path" "output path"
                    if (args.Length == 3
                        && args[0] == "unpack")
                    {
                        if (!new FileInfo(args[1]).Exists)
                        {
                            await Console.Out.WriteLineAsync("Error:");
                            await Console.Out.WriteLineAsync($"DACPAC file '{args[1]}' not found");
                            return 1;
                        }

                        DacPackageUnpacker.Unpack(args[1], args[2]);

                        await Console.Out.WriteLineAsync("Result:");
                        await Console.Out.WriteLineAsync("OK");

                        return 0;
                    }

                    // unpack "dacpac path" "output path"
                    if (args.Length == 3
                        && args[0] == "script")
                    {
                        if (!new FileInfo(args[1]).Exists)
                        {
                            await Console.Out.WriteLineAsync("Error:");
                            await Console.Out.WriteLineAsync($"DACPAC file '{args[1]}' not found");
                            return 1;
                        }

                        var result = DacPackageScripter.Script(args[1], args[2]);

                        await Console.Out.WriteLineAsync("Result:");
                        await Console.Out.WriteLineAsync(result);

                        return 0;
                    }

                    // visualcompare "<DACPAC_PATH>" "connectionString" <database_is_source>
                    if (args.Length == 4
                        && args[0] == "visualcompare"
                        && bool.TryParse(args[3], out bool visualDbIsSource))
                    {
                        if (!new FileInfo(args[1]).Exists)
                        {
                            await Console.Out.WriteLineAsync("Error:");
                            await Console.Out.WriteLineAsync($"DACPAC file '{args[1]}' not found");
                            return 1;
                        }

                        var compareResult = DacPackageComparer.CompareVisual(args[1], args[2], visualDbIsSource);

                        var path = Path.Join(Path.GetTempPath(), $"SqlProjVisualCompare_{Guid.NewGuid():N}.json");

                        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(compareResult), Encoding.UTF8);

                        await Console.Out.WriteLineAsync("Result:");
                        await Console.Out.WriteLineAsync(path);

                        return 0;
                    }

                    // merge "projectpath" "connectionString" "tableName" "schema"
                    if (args.Length == 5
                        && (args[0] == "merge"))
                    {
                        var result = MergeGenerator.Generate(args[1], args[2], args[3], args[4]);

                        await Console.Out.WriteLineAsync("Result:");
                        await Console.Out.WriteLineAsync(result);

                        return 0;
                    }
                }
                else
                {
                    await Console.Out.WriteLineAsync("Error:");
                    await Console.Out.WriteLineAsync("Invalid command line");
                    return 1;
                }

                return 0;
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception ex)
            {
                await Console.Out.WriteLineAsync("Error:");
                await Console.Out.WriteLineAsync(ex.Demystify().ToString());
                return 1;
            }
#pragma warning restore CA1031 // Do not catch general exception types
        }
    }
}