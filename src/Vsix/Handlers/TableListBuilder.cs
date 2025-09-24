using DacFXToolLib.Common;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SqlProjectsPowerTools
{
    public class TableListBuilder
    {
        private readonly string connectionString;
        private readonly DatabaseType databaseType;
        private readonly SchemaInfo[] schemas;

        public TableListBuilder(string connectionString, DatabaseType databaseType, SchemaInfo[] schemas)
        {
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new ArgumentNullException(nameof(connectionString));
            }

            this.connectionString = connectionString;
            this.databaseType = databaseType;
            this.schemas = schemas;
        }

        public async Task<List<TableModel>> GetTableDefinitionsAsync()
        {
            var launcher = new ProcessLauncher();
            // TODO: pass in the mergeDacpacs option from options - AdvancedOptions.Instance.MergeDacpacs

            return await launcher.GetTablesAsync(connectionString, databaseType, schemas, false);
        }
    }
}