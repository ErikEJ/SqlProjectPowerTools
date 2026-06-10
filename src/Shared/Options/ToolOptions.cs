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

        [Category("General")]
        [DisplayName(@"Disable live code analysis")]
        [Description("Disable live static SQL code analysis")]
        [DefaultValue(false)]
        public bool DisableCodeAnalysis { get; set; }

        [Category("General")]
        [DisplayName(@"Check for updates")]
        [Description("Check for updates to the extension on startup")]
        [DefaultValue(true)]
        public bool CheckForUpdates { get; set; } = true;

        [Category("General")]
        [DisplayName(@"Publish programmability objects on save (preview)")]
        [Description("Automatically publish supported CREATE statements when .sql files are saved")]
        [DefaultValue(false)]
        public bool PublishProgrammabilityObjectsOnSave { get; set; }
    }
}