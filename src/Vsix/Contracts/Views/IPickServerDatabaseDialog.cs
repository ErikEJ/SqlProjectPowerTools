using System.Collections.Generic;
using DacFXToolLib.Common;

namespace SqlProjectsPowerTools
{
    public interface IPickServerDatabaseDialog : IDialog<(DatabaseConnectionModel Connection, bool FilterSchemas, SchemaInfo[] Schemas, string UiHint)>
    {
        void PublishConnections(IEnumerable<DatabaseConnectionModel> connections);

        void PublishDefinitions(IEnumerable<DatabaseConnectionModel> definitions);

        void PublishSchemas(IEnumerable<SchemaInfo> schemas);


        void PublishUiHint(string uiHint);

        (DatabaseConnectionModel Connection, bool FilterSchemas, SchemaInfo[] Schemas, string UiHint) GetResults();
    }
}