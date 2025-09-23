using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Microsoft.EntityFrameworkCore.Scaffolding;
using Microsoft.Extensions.DependencyInjection;
using RevEng.Core.Abstractions.Model;
using DacFXToolLib.Dab;
using DacFXToolLib.Common;
using DacFXToolLib;
using DacFXToolLib.DacpacReport;
using DacFXToolLib.Diagram;


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
                        SchemaInfo[] schemas = null;
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
                        var procedureModelFactory = provider.GetRequiredService<IProcedureModelFactory>();
                        var functionModelFactory = provider.GetRequiredService<IFunctionModelFactory>();
                        var databaseModelFactory = provider.GetRequiredService<IDatabaseModelFactory>();
                        var builder = new TableListBuilder(reverseEngineerCommandOptions, procedureModelFactory, functionModelFactory, databaseModelFactory, schemas);

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