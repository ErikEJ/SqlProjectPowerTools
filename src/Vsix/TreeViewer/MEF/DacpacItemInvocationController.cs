using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Internal.VisualStudio.PlatformUI;

namespace SqlProjectsPowerTools.TreeViewer
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
                    ObserveTask(OpenItemAsync(item.Info.FullName, preview));
                }
                else
                {
                    ObserveTask(item.RefreshAsync());
                }
            }
#pragma warning restore S3267 // Loops should be simplified with "LINQ" expressions

            return true;
        }

        private static async Task OpenItemAsync(string filePath, bool preview)
        {
            if (preview)
            {
                await VS.Documents.OpenInPreviewTabAsync(filePath);
                return;
            }

            await VS.Documents.OpenAsync(filePath);
        }

        private static void ObserveTask(Task task)
        {
#pragma warning disable VSTHRD110 // Observe result of async calls
#pragma warning disable VSTHRD105 // Avoid method overloads that assume TaskScheduler.Current
            task.ContinueWith(
                t =>
            {
                if (t.Exception?.InnerException != null)
                {
                    t.Exception.InnerException.Log();
                    return;
                }

                t.Exception?.Log();
            },
                TaskContinuationOptions.OnlyOnFaulted);
#pragma warning restore VSTHRD105 // Avoid method overloads that assume TaskScheduler.Current
#pragma warning restore VSTHRD110 // Observe result of async calls
        }
    }
}