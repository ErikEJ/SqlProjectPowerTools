using System.Collections.Generic;
using DacFXToolLib.Common;

namespace SqlProjectsPowerTools
{
    public interface IPickServerDatabaseDialog : IDialog<(DatabaseConnectionModel Connection, int CodeGenerationMode, SchemaInfo[] Schemas, string UiHint, bool GetDatabaseOptions)>
    {
        void PublishConnections(IEnumerable<DatabaseConnectionModel> connections);

        void PublishDefinitions(IEnumerable<DatabaseConnectionModel> definitions);

        void PublishSchemas(IEnumerable<SchemaInfo> schemas);

        void PublishFileGenerationMode(IList<CodeGenerationItem> methods);

        void PublishUiHint(string uiHint);

        (DatabaseConnectionModel Connection, int CodeGenerationMode, SchemaInfo[] Schemas, string UiHint, bool GetDatabaseOptions) GetResults();
    }
}