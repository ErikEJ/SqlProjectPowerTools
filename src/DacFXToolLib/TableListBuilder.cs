using DacFXToolLib.Common;
using Microsoft.EntityFrameworkCore.Scaffolding;
using Microsoft.EntityFrameworkCore.Scaffolding.Metadata;
using RevEng.Core.Abstractions;
using RevEng.Core.Abstractions.Model;
using System.Collections.Generic;

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
            var tableLookup = new Dictionary<string, Model.SimpleTable>(StringComparer.OrdinalIgnoreCase);
            var columnLookup = new Dictionary<Model.SimpleTable, Dictionary<string, Model.SimpleColumn>>();

            foreach (var dbTable in dbModel.Tables)
            {
                var schema = dbTable.Schema ?? "dbo";

                var simpleTable = new Model.SimpleTable
                {
                    Name = dbTable.Name,
                    Schema = schema,
                };

                var simpleColumnsByName = new Dictionary<string, Model.SimpleColumn>(StringComparer.OrdinalIgnoreCase);

                foreach (var col in dbTable.Columns)
                {
                    var simpleCol = new Model.SimpleColumn
                    {
                        Name = col.Name,
                        StoreType = col.StoreType,
                        IsNullable = col.IsNullable,
                    };

                    simpleTable.Columns.Add(simpleCol);
                    simpleColumnsByName[col.Name] = simpleCol;
                }

                if (dbTable.PrimaryKey != null)
                {
                    var pk = new Model.SimplePrimaryKey { Name = dbTable.PrimaryKey.Name };

                    if (simpleColumnsByName.Count > 0)
                    {
                        foreach (var col in dbTable.PrimaryKey.Columns)
                        {
                            if (simpleColumnsByName.TryGetValue(col.Name, out var simpleCol))
                            {
                                pk.Columns.Add(simpleCol);
                            }
                        }
                    }

                    simpleTable.PrimaryKey = pk;
                }

                tables.Add(simpleTable);

                var tableKey = schema + "." + dbTable.Name;
                tableLookup[tableKey] = simpleTable;
                columnLookup[simpleTable] = simpleColumnsByName;
            }

            foreach (var dbTable in dbModel.Tables)
            {
                var schema = dbTable.Schema ?? "dbo";
                var tableKey = schema + "." + dbTable.Name;

                if (!tableLookup.TryGetValue(tableKey, out var simpleTable))
                {
                    continue;
                }

                columnLookup.TryGetValue(simpleTable, out var simpleColumnsByName);

                foreach (var fk in dbTable.ForeignKeys)
                {
                    var principalSchema = fk.PrincipalTable.Schema ?? "dbo";
                    var principalKey = principalSchema + "." + fk.PrincipalTable.Name;

                    if (!tableLookup.TryGetValue(principalKey, out var principalTable))
                    {
                        continue;
                    }

                    columnLookup.TryGetValue(principalTable, out var principalColumnsByName);

                    var simpleFk = new Model.SimpleForeignKey
                    {
                        Name = fk.Name,
                        PrincipalTable = principalTable,
                    };

                    if (simpleColumnsByName != null)
                    {
                        foreach (var col in fk.Columns)
                        {
                            if (simpleColumnsByName.TryGetValue(col.Name, out var simpleCol))
                            {
                                simpleFk.Columns.Add(simpleCol);
                            }
                        }
                    }

                    if (principalColumnsByName != null)
                    {
                        foreach (var col in fk.PrincipalColumns)
                        {
                            if (principalColumnsByName.TryGetValue(col.Name, out var simpleCol))
                            {
                                simpleFk.PrincipalColumns.Add(simpleCol);
                            }
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