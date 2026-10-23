using System.Collections.Generic;
using DacFXToolLib.Common;

namespace SqlProjectsPowerTools;

public interface IPickServerDatabaseDialog : IDialog<(DatabaseConnectionModel Connection, int CodeGenerationMode, SchemaInfo[] Schemas, string UiHint, bool GetDatabaseOptions)>
{
    public void PublishConnections(IEnumerable<DatabaseConnectionModel> connections);

    public void PublishDefinitions(IEnumerable<DatabaseConnectionModel> definitions);

    public void PublishSchemas(IEnumerable<SchemaInfo> schemas);

    public void PublishFileGenerationMode(IList<CodeGenerationItem> methods);

    public void PublishUiHint(string uiHint);

    public (DatabaseConnectionModel Connection, int CodeGenerationMode, SchemaInfo[] Schemas, string UiHint, bool GetDatabaseOptions) GetResults();
}