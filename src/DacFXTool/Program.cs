using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using DacFXToolLib;
using DacFXToolLib.Common;
using DacFXToolLib.Dab;
using Microsoft.EntityFrameworkCore.Scaffolding;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SqlServer.Dac;
using RevEng.Core.Abstractions.Model;

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
                    if ((args.Length == 3 || args.Length == 4)
                        && int.TryParse(args[1], out int dbTypeInt)
                        && bool.TryParse(args[0], out bool mergeDacpacs))
                    {
                        SchemaInfo[]? schemas = null;
                        if (args.Length == 4)
                        {
                            schemas = args[3].Split(',').Select(s => new SchemaInfo { Name = s }).ToArray();
                        }

                        var reverseEngineerCommandOptions = new ReverseEngineerCommandOptions
                        {
                            ConnectionString = args[2],
                            DatabaseType = (DatabaseType)dbTypeInt,
                            MergeDacpacs = mergeDacpacs,
                        };

                        var provider = new ServiceCollection().AddEfpt(reverseEngineerCommandOptions, new List<string>(), new List<string>(), new List<string>()).BuildServiceProvider();
                        var procedureModelFactory = provider.GetService<IProcedureModelFactory>();
                        var functionModelFactory = provider.GetService<IFunctionModelFactory>();
                        var databaseModelFactory = provider.GetRequiredService<IDatabaseModelFactory>();
                        var builder = new TableListBuilder(reverseEngineerCommandOptions, procedureModelFactory, functionModelFactory, databaseModelFactory, schemas ?? []);

                        var buildResult = builder.GetTableModels();

                        buildResult.AddRange(builder.GetProcedures());

                        buildResult.AddRange(builder.GetFunctions());

                        await Console.Out.WriteLineAsync("Result:");
                        await Console.Out.WriteLineAsync(buildResult.Write());

                        return 0;
                    }

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

                    // compare true "<DACPAC_PATH>" "connectionString"
                    if (args.Length == 4
                        && (args[0] == "compare")
                        && bool.TryParse(args[1], out bool databaseIsSource))
                    {
                        if (!new FileInfo(args[2]).Exists)
                        {
                            await Console.Out.WriteLineAsync("Error:");
                            await Console.Out.WriteLineAsync($"DACPAC file '{args[2]}' not found");
                            return 1;
                        }

                        var script = DacPackageComparer.Compare(args[2], args[3], databaseIsSource);

                        var path = Path.Join(Path.GetTempPath(), "SqlProjDiff.sql");

                        if (string.IsNullOrEmpty(script))
                        {
                            script = "-- No differences found";
                        }

                        await File.WriteAllTextAsync(path, script, Encoding.UTF8);

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