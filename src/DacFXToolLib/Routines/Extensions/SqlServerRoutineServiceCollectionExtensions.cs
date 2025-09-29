using ErikEJ.EntityFrameworkCore.SqlServer.Scaffolding;
using Microsoft.EntityFrameworkCore.Design.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RevEng.Core.Abstractions.Model;

namespace DacFXToolLib.Routines.Extensions
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "EF1001:Internal EF Core API usage.", Justification = "Design tool")]
    public static class SqlServerRoutineServiceCollectionExtensions
    {
        public static IServiceCollection AddSqlServerDacpacStoredProcedureDesignTimeServices(
            this IServiceCollection services,
            SqlServerDacpacDatabaseModelFactoryOptions factoryOptions,
            IOperationReporter? reporter = null)
        {
            if (reporter == null)
            {
                reporter = new OperationReporter(handler: null);
            }

            return services
                .AddSingleton<IClrTypeMapper, SqlServerClrTypeMapper>()
                .AddSingleton<IProcedureModelFactory, SqlServerDacpacStoredProcedureModelFactory>(
                    provider => new SqlServerDacpacStoredProcedureModelFactory(factoryOptions))
                .AddLogging(b => b.SetMinimumLevel(LogLevel.Debug).AddProvider(new OperationLoggerProvider(reporter)));
        }

        public static IServiceCollection AddSqlServerDacpacFunctionDesignTimeServices(
            this IServiceCollection services,
            SqlServerDacpacDatabaseModelFactoryOptions factoryOptions,
            IOperationReporter? reporter = null)
        {
            if (reporter == null)
            {
                reporter = new OperationReporter(handler: null);
            }

            return services
                .AddSingleton<IFunctionModelFactory, SqlServerDacpacFunctionModelFactory>(
                    provider => new SqlServerDacpacFunctionModelFactory(factoryOptions))
                .AddLogging(b => b.SetMinimumLevel(LogLevel.Debug).AddProvider(new OperationLoggerProvider(reporter)));
        }
    }
}