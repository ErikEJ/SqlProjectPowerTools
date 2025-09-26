using System.ComponentModel;

namespace SqlProjectsPowerTools
{
    public class ToolOptions : BaseOptionModel<ToolOptions>
    {
        [Category("General")]
        [DisplayName(@"Merge .dacpac files")]
        [Description("Merge dependent .dacpac files")]
        [DefaultValue(false)]
        public bool MergeDacpacs { get; set; }
    }
}