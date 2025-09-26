using System.Collections.Generic;
using System.Threading.Tasks;
using DacFXToolLib.Common;

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

            return await launcher.GetTablesAsync(connectionString, databaseType, schemas, ToolOptions.Instance.MergeDacpacs);
        }
    }
}