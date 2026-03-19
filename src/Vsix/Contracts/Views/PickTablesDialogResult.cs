using DacFXToolLib.Common;

namespace SqlProjectsPowerTools
{
    public class PickTablesDialogResult
    {
        public SerializationTableModel[] Objects { get; set; }

        public Schema[] CustomReplacers { get; set; }
    }
}