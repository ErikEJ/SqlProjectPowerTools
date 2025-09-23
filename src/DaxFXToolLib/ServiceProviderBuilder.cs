using DacFXToolLib.Common;
using DacFXToolLib.Routines.Extensions;
using ErikEJ.EntityFrameworkCore.SqlServer.Scaffolding;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Design.Internal;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Scaffolding;
using Microsoft.EntityFrameworkCore.SqlServer.Design.Internal;
using Microsoft.EntityFrameworkCore.SqlServer.Scaffolding.Internal;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace DacFXToolLib
{
    public static class ServiceProviderBuilder
    {
        public static IServiceCollection AddEfpt(this IServiceCollection serviceCollection, ReverseEngineerCommandOptions options, List<string> errors, List<string> warnings, List<string> info)
        {
            ArgumentNullException.ThrowIfNull(options);
            ArgumentNullException.ThrowIfNull(serviceCollection);
            ArgumentNullException.ThrowIfNull(warnings);
            ArgumentNullException.ThrowIfNull(errors);
            ArgumentNullException.ThrowIfNull(info);

            var reporter = new OperationReporter(
                new OperationReportHandler(
                    errors.Add,
                    warnings.Add,
                    info.Add,
                    info.Add));

            serviceCollection
                .AddEntityFrameworkDesignTimeServices()
                .AddSingleton<IOperationReporter, OperationReporter>(provider =>
                    reporter);

            // Add database provider services
            switch (options.DatabaseType)
            {
                case DatabaseType.SQLServerDacpac:
                    AddSqlServerProviderServices(serviceCollection, options); break;


                default:
                    throw new ArgumentOutOfRangeException(nameof(options), $"unsupported database type: {options.DatabaseType}");
            }


            return serviceCollection;
        }

        private static void AddSqlServerProviderServices(IServiceCollection serviceCollection, ReverseEngineerCommandOptions options)
        {
            var provider = new SqlServerDesignTimeServices();
            provider.ConfigureDesignTimeServices(serviceCollection);

#if CORE100
            serviceCollection.AddSingleton(new SqlServerSingletonOptions());
#endif

            if (options.DatabaseType == DatabaseType.SQLServerDacpac)
            {
                var excludedIndexes = options.Tables?.Select(t => new { t.Name, t.ExcludedIndexes });

                serviceCollection.AddSingleton<IDatabaseModelFactory, SqlServerDacpacDatabaseModelFactory>(
                   serviceProvider => new SqlServerDacpacDatabaseModelFactory(
                       new SqlServerDacpacDatabaseModelFactoryOptions
                       {
                           MergeDacpacs = options.MergeDacpacs,
                           ExcludedIndexes = excludedIndexes?.ToDictionary(t => t.Name, t => t.ExcludedIndexes),
                       },
                       serviceProvider.GetService<IRelationalTypeMappingSource>()));

                serviceCollection.AddSqlServerDacpacStoredProcedureDesignTimeServices(new SqlServerDacpacDatabaseModelFactoryOptions
                {
                    MergeDacpacs = options.MergeDacpacs,
                });

                serviceCollection.AddSqlServerDacpacFunctionDesignTimeServices(new SqlServerDacpacDatabaseModelFactoryOptions
                {
                    MergeDacpacs = options.MergeDacpacs,
                });
            }
        }
    }
}