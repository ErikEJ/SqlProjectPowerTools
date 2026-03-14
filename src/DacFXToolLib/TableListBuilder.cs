using DacFXToolLib.Common;
using Microsoft.EntityFrameworkCore.Scaffolding;
using Microsoft.EntityFrameworkCore.Scaffolding.Metadata;
using RevEng.Core.Abstractions;
using RevEng.Core.Abstractions.Model;

namespace DacFXToolLib
{
    public class TableListBuilder
    {
        private readonly IProcedureModelFactory? procedureModelFactory;
        private readonly IFunctionModelFactory? functionModelFactory;
        private readonly IDatabaseModelFactory databaseModelFactory;

        private readonly SchemaInfo[] schemas;
        private readonly DatabaseType databaseType;
        private readonly string connectionString;
        private DatabaseModel? databaseModel;

        public TableListBuilder(
            ReverseEngineerCommandOptions options,
            IProcedureModelFactory? procedureModelFactory,
            IFunctionModelFactory? functionModelFactory,
            IDatabaseModelFactory databaseModelFactory,
            SchemaInfo[] schemas)
        {
            ArgumentNullException.ThrowIfNull(options);

            this.procedureModelFactory = procedureModelFactory;
            this.functionModelFactory = functionModelFactory;
            this.databaseModelFactory = databaseModelFactory;
            this.schemas = schemas;
            databaseType = options.DatabaseType;
            connectionString = options.ConnectionString;
        }

        public List<TableModel> GetTableModels()
        {
            var dbModel = databaseModel ?? GetDatabaseModel();

            var databaseTables = dbModel.Tables.OrderBy(t => t.Schema).ThenBy(t => t.Name).ToList();

            var buildResult = new List<TableModel>();
            foreach (var databaseTable in databaseTables)
            {
                var columns = new List<ColumnModel>();

                var primaryKeyColumnNames = databaseTable.PrimaryKey?.Columns.Select(c => c.Name).ToHashSet();
                var foreignKeyColumnNames = databaseTable.ForeignKeys?.SelectMany(c => c.Columns).Select(c => c.Name).ToHashSet();
                columns.AddRange(from column in databaseTable.Columns.Where(c => !string.IsNullOrWhiteSpace(c.Name))
                                 select new ColumnModel(
                                     column.Name,
                                     column.StoreType,
                                     primaryKeyColumnNames?.Contains(column.Name) ?? false,
                                     foreignKeyColumnNames?.Contains(column.Name) ?? false));
                buildResult.Add(new TableModel(databaseTable.Name, databaseTable.Schema, databaseType, databaseTable is DatabaseView ? ObjectType.View : ObjectType.Table, columns));
            }

            return buildResult;
        }

        public string GetMermaidDiagram()
        {
            var dbModel = databaseModel ?? GetDatabaseModel();

            var simpleTables = ConvertToSimpleTables(dbModel);

            var generator = new DatabaseModelToMermaid(simpleTables);

            return generator.CreateMermaid();
        }

        private static List<Model.SimpleTable> ConvertToSimpleTables(DatabaseModel dbModel)
        {
            var tables = new List<Model.SimpleTable>();

            foreach (var dbTable in dbModel.Tables)
            {
                var simpleTable = new Model.SimpleTable
                {
                    Name = dbTable.Name,
                    Schema = dbTable.Schema ?? "dbo",
                };

                foreach (var col in dbTable.Columns)
                {
                    simpleTable.Columns.Add(new Model.SimpleColumn
                    {
                        Name = col.Name,
                        StoreType = col.StoreType,
                        IsNullable = col.IsNullable,
                    });
                }

                if (dbTable.PrimaryKey != null)
                {
                    var pk = new Model.SimplePrimaryKey { Name = dbTable.PrimaryKey.Name };
                    foreach (var col in dbTable.PrimaryKey.Columns)
                    {
                        var simpleCol = simpleTable.Columns.FirstOrDefault(c =>
                            string.Equals(c.Name, col.Name, StringComparison.OrdinalIgnoreCase));
                        if (simpleCol != null)
                        {
                            pk.Columns.Add(simpleCol);
                        }
                    }

                    simpleTable.PrimaryKey = pk;
                }

                tables.Add(simpleTable);
            }

            foreach (var dbTable in dbModel.Tables)
            {
                var simpleTable = tables.FirstOrDefault(t =>
                    string.Equals(t.Name, dbTable.Name, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(t.Schema, dbTable.Schema ?? "dbo", StringComparison.OrdinalIgnoreCase));
                if (simpleTable == null)
                {
                    continue;
                }

                foreach (var fk in dbTable.ForeignKeys)
                {
                    var principalTable = tables.FirstOrDefault(t =>
                        string.Equals(t.Name, fk.PrincipalTable.Name, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(t.Schema, fk.PrincipalTable.Schema ?? "dbo", StringComparison.OrdinalIgnoreCase));
                    if (principalTable == null)
                    {
                        continue;
                    }

                    var simpleFk = new Model.SimpleForeignKey
                    {
                        Name = fk.Name,
                        PrincipalTable = principalTable,
                    };

                    foreach (var col in fk.Columns)
                    {
                        var simpleCol = simpleTable.Columns.FirstOrDefault(c =>
                            string.Equals(c.Name, col.Name, StringComparison.OrdinalIgnoreCase));
                        if (simpleCol != null)
                        {
                            simpleFk.Columns.Add(simpleCol);
                        }
                    }

                    foreach (var col in fk.PrincipalColumns)
                    {
                        var simpleCol = principalTable.Columns.FirstOrDefault(c =>
                            string.Equals(c.Name, col.Name, StringComparison.OrdinalIgnoreCase));
                        if (simpleCol != null)
                        {
                            simpleFk.PrincipalColumns.Add(simpleCol);
                        }
                    }

                    if (simpleFk.PrincipalColumns.Count > 0)
                    {
                        simpleTable.ForeignKeys.Add(simpleFk);
                    }
                }
            }

            return tables;
        }

        public List<TableModel> GetProcedures()
        {
            var result = new List<TableModel>();

            if (databaseType != DatabaseType.SQLServerDacpac)
            {
                return result;
            }

            var procedureModelOptions = new ModuleModelFactoryOptions
            {
                FullModel = false,
                Modules = new List<string>(),
            };

            var procedureModel = procedureModelFactory!.Create(connectionString, procedureModelOptions);

            foreach (var procedure in procedureModel.Routines)
            {
                result.Add(new TableModel(procedure.Name, procedure.Schema, databaseType, ObjectType.Procedure, null));
            }

            return result.OrderBy(c => c.DisplayName).ToList();
        }

        public List<TableModel> GetFunctions()
        {
            var result = new List<TableModel>();

            if (databaseType != DatabaseType.SQLServerDacpac)
            {
                return result;
            }

            var functionModelOptions = new ModuleModelFactoryOptions
            {
                FullModel = false,
                Modules = new List<string>(),
            };

            var functionModel = functionModelFactory!.Create(connectionString, functionModelOptions);

            foreach (var function in functionModel.Routines)
            {
                result.Add(new TableModel(function.Name, function.Schema, databaseType, ObjectType.ScalarFunction, null));
            }

            return result.OrderBy(c => c.DisplayName).ToList();
        }

        private DatabaseModel GetDatabaseModel()
        {
            var dbModelOptions = new DatabaseModelFactoryOptions(schemas: schemas?.Select(s => s.Name));

            databaseModel = this.databaseModelFactory!.Create(connectionString, dbModelOptions);
            return databaseModel;
        }
    }
}