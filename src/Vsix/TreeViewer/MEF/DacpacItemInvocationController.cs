using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Internal.VisualStudio.PlatformUI;

namespace SqlProjectsPowerTools.TreeViewer.MEF
{
    internal class DacpacItemInvocationController : IInvocationController
    {
        // Singleton instance to avoid creating new instances for each node
        public static readonly DacpacItemInvocationController Instance = new();

        private DacpacItemInvocationController()
        {
            // Private constructor for singleton
        }

        public bool Invoke(IEnumerable<object> items, InputSource inputSource, bool preview)
        {
#pragma warning disable S3267 // Loops should be simplified with "LINQ" expressions
            foreach (DacpacItemNode item in items.OfType<DacpacItemNode>())
            {
                if (item.Info is FileInfo)
                {
                    if (preview)
                    {
                        VS.Documents.OpenInPreviewTabAsync(item.Info.FullName).FireAndForget();
                    }
                    else
                    {
                        VS.Documents.OpenAsync(item.Info.FullName).FireAndForget();
                    }
                }
                else
                {
                    SendKeys.Send("{RIGHT}");
                }
            }
#pragma warning restore S3267 // Loops should be simplified with "LINQ" expressions

            return true;
        }
    }
}