using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.VisualStudio.Imaging;

namespace SqlProjectsPowerTools
{
    public class SchemaCompareToolWindow : BaseToolWindow<SchemaCompareToolWindow>
    {
        public override string GetTitle(int toolWindowId) => "Visual Schema Compare";

        public override Type PaneType => typeof(Pane);

        public override Task<FrameworkElement> CreateAsync(int toolWindowId, CancellationToken cancellationToken)
        {
            return Task.FromResult<FrameworkElement>(new SchemaCompareWindowControl());
        }

        [Guid("a7e3c028-5f2a-4b8e-9c1d-3f6a8b2e4d09")]
        internal class Pane : ToolkitToolWindowPane
        {
            public Pane()
            {
                BitmapImageMoniker = KnownMonikers.CompareSchemas;
            }
        }
    }
}
