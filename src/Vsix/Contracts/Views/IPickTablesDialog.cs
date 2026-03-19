using System.Collections.Generic;
using DacFXToolLib.Common;

namespace SqlProjectsPowerTools
{
    public interface IPickTablesDialog : IDialog<PickTablesDialogResult>
    {
        IPickTablesDialog AddTables(IEnumerable<TableModel> tables, IEnumerable<Schema> customReplacers);

        IPickTablesDialog PreselectTables(IEnumerable<SerializationTableModel> tables);
    }
}