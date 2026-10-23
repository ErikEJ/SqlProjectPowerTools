using System.Collections.Generic;
using DacFXToolLib.Common;

namespace SqlProjectsPowerTools;

public interface IPickTablesDialog : IDialog<PickTablesDialogResult>
{
    public IPickTablesDialog AddTables(IEnumerable<TableModel> tables, IEnumerable<Schema> customReplacers);

    public IPickTablesDialog PreselectTables(IEnumerable<SerializationTableModel> tables);
}