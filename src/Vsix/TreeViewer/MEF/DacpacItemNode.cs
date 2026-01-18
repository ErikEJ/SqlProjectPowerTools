using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using Microsoft.Internal.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Imaging.Interop;
using Microsoft.VisualStudio.Language.Intellisense;

namespace SqlProjectsPowerTools.TreeViewer.MEF
{
    [DebuggerDisplay("{Text}")]
#pragma warning disable S3881 // "IDisposable" should be implemented correctly
    internal class DacpacItemNode :
#pragma warning restore S3881 // "IDisposable" should be implemented correctly
        IAttachedCollectionSource,
        ITreeDisplayItemWithImages,
        IPrioritizedComparable,
        IBrowsablePattern,
        IInteractionPatternProvider,
        IInvocationPattern,
        ISupportDisposalNotification,
        IDisposable,
        IRefreshPattern
    {
        private static readonly StringComparer StringComparer = StringComparer.OrdinalIgnoreCase;

        private readonly object loadLock = new();
        private BulkObservableCollection<DacpacItemNode> children;
        private bool isLoaded;
        private bool isLoading;
        private CancellationTokenSource loadCancellationTokenSource;

        public DacpacItemNode(IAttachedCollectionSource source, string outputPath, string dacpacPath)
        {
            SourceItem = source;
            Rebuild(outputPath, dacpacPath);
        }

        public void Rebuild(string outputPath, string dacpacPath)
        {
            Text = Path.GetFileName(outputPath) ?? ".dacpac content";
            IsCut = false;
            isLoaded = false;
            var hadItems = HasItems;

            if (Directory.Exists(outputPath))
            {
                Info = new DirectoryInfo(outputPath);
                HasItems = CheckHasItemsQuick();
            }
            else if (File.Exists(outputPath))
            {
                Info = new FileInfo(outputPath);
                HasItems = false;
            }
            else
            {
                IsCut = true;
                HasItems = false;
            }

            // Batch property change notifications
            RaisePropertyChanged(nameof(Text));
            RaisePropertyChanged(nameof(IsCut));

            // Only notify HasItems changed if it actually changed
            if (hadItems != HasItems)
            {
                RaisePropertyChanged(nameof(HasItems));
            }

            if (!string.IsNullOrEmpty(dacpacPath))
            {
                ToolTipContent = SetTooltip(dacpacPath);
                RaisePropertyChanged(nameof(ToolTipContent));
            }
        }

        private bool CheckHasItemsQuick()
        {
            if (Info is DirectoryInfo directory)
            {
                try
                {
                    // Quick check - just see if there's at least one item without enumerating all
                    return directory.EnumerateFileSystemInfos().Take(1).Any();
                }
                catch
                {
                    return false;
                }
            }

            return false;
        }

        public FileSystemInfo Info { get; set; }

        public object SourceItem { get; init; }

        public bool HasItems { get; set; }

        public IEnumerable Items
        {
            get
            {
                children ??= [];

                if (!isLoaded && !isLoading && Info is DirectoryInfo)
                {
                    // Start async loading without blocking
                    _ = LoadChildrenAsync();
                }

                return children;
            }
        }

        private async Task LoadChildrenAsync()
        {
            lock (loadLock)
            {
                if (isLoading || isLoaded || IsDisposed)
                {
                    return;
                }

                isLoading = true;
                loadCancellationTokenSource?.Cancel();
                loadCancellationTokenSource = new CancellationTokenSource();
            }

            CancellationToken cancellationToken = loadCancellationTokenSource.Token;

            try
            {
                // Do the heavy work off the UI thread
                List<DacpacItemNode> childNodes = await Task.Run(() => LoadChildrenOffUIThread(cancellationToken), cancellationToken);

                if (cancellationToken.IsCancellationRequested || IsDisposed)
                {
                    return;
                }

                // Switch back to UI thread to update the collection
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

                if (cancellationToken.IsCancellationRequested || IsDisposed)
                {
                    return;
                }

                UpdateChildrenOnUIThread(childNodes);
            }
            catch (OperationCanceledException)
            {
                // Expected when cancelled
            }
            catch (Exception ex)
            {
                // Log error but don't crash
                await ex.LogAsync();
            }
            finally
            {
                lock (loadLock)
                {
                    isLoading = false;
                }
            }
        }

        private List<DacpacItemNode> LoadChildrenOffUIThread(CancellationToken cancellationToken)
        {
            var activeNodes = new List<DacpacItemNode>();

            if (Info is DirectoryInfo directory)
            {
                try
                {
                    foreach (FileSystemInfo item in directory.EnumerateFileSystemInfos())
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        var child = new DacpacItemNode(this, item.FullName, null);
                        activeNodes.Add(child);
                    }

                    // Sort off UI thread
                    activeNodes.Sort((lhs, rhs) =>
                    {
                        // Optimized comparison - check directory first without GetType()
                        var lhsIsDir = lhs.Info is DirectoryInfo;
                        var rhsIsDir = rhs.Info is DirectoryInfo;

                        return lhsIsDir == rhsIsDir
                            ? StringComparer.Compare(lhs.Text, rhs.Text)
                            : lhsIsDir ? -1 : 1;
                    });
                }
                catch (UnauthorizedAccessException)
                {
                    // Directory not accessible
                }
                catch (DirectoryNotFoundException)
                {
                    // Directory was deleted
                }
            }

            return activeNodes;
        }

        private void UpdateChildrenOnUIThread(List<DacpacItemNode> newChildren)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            // Dispose old children
            if (children != null)
            {
                foreach (DacpacItemNode child in children)
                {
                    child.Dispose();
                }
            }

            children ??= [];

            children.BeginBulkOperation();
            children.Clear();
            children.AddRange(newChildren);
            children.EndBulkOperation();

            isLoaded = true;
            HasItems = children.Any();

            RaisePropertyChanged(nameof(Items));
            RaisePropertyChanged(nameof(HasItems));
        }

        public string Text { get; set; }

        public string ToolTipText => null;

        public string StateToolTipText => null;

        public object ToolTipContent { get; set; }

        public FontWeight FontWeight => FontWeights.Normal;

        public System.Windows.FontStyle FontStyle => FontStyles.Normal;

        public ImageMoniker IconMoniker => Info.GetIcon(false);

        public ImageMoniker ExpandedIconMoniker => Info.GetIcon(true);

        public ImageMoniker OverlayIconMoniker => default;

        public ImageMoniker StateIconMoniker => default;

        public int Priority => 0;

        public bool IsCut { get; set; }

        private bool isDisposed;

        public bool IsDisposed
        {
            get
            {
                return isDisposed;
            }

            set
            {
                if (isDisposed != value)
                {
                    isDisposed = value;
                    RaisePropertyChanged(nameof(IsDisposed));
                }
            }
        }

        public bool CanPreview => Info is FileInfo;

        public IInvocationController InvocationController => DacpacItemInvocationController.Instance;

        public event PropertyChangedEventHandler PropertyChanged;

        private void Refresh()
        {
            // Reset loading state and trigger async reload
            lock (loadLock)
            {
                isLoaded = false;
                loadCancellationTokenSource?.Cancel();
            }

            // Trigger async loading
            _ = LoadChildrenAsync();
        }

        public async Task RefreshAsync()
        {
            // Reset loading state
            lock (loadLock)
            {
                isLoaded = false;
                loadCancellationTokenSource?.Cancel();
            }

            // Await the loading to complete
            await LoadChildrenAsync();
        }

        public void CancelLoad()
        {
            loadCancellationTokenSource?.Cancel();
        }

        public int CompareTo(object obj)
        {
            return obj is ITreeDisplayItem item ? StringComparer.Compare(Text, item.Text) : 0;
        }

        private string SetTooltip(string dacpacFile)
        {
            if (Info == null)
            {
                return "Compile to see content of generated .dacpac file";
            }
            else if (!string.IsNullOrEmpty(dacpacFile) && File.Exists(dacpacFile))
            {
                // Avoid expensive file size lookup on UI thread - just show basic info
                // File size could be computed async if needed for tooltip
                return $"Last updated: {Info.LastWriteTime}";
            }

            return $"Last updated: {Info.LastWriteTime}";
        }

        public void Dispose()
        {
            if (IsDisposed)
            {
                return;
            }

            IsDisposed = true;

            // Cancel any ongoing loading
            loadCancellationTokenSource?.Cancel();
            loadCancellationTokenSource?.Dispose();
            loadCancellationTokenSource = null;

            // Dispose children efficiently
            if (children != null)
            {
                // Create local copy to avoid issues during disposal
                DacpacItemNode[] childrenToDispose = [.. children];
                children.Clear();
                children = null;

                foreach (DacpacItemNode item in childrenToDispose)
                {
                    item.Dispose();
                }
            }

            // Clear event handlers to help GC
            PropertyChanged = null;
        }

        public object GetBrowseObject() => this;

        public TPattern GetPattern<TPattern>()
            where TPattern : class
        {
            if (!IsDisposed)
            {
                // Optimized pattern lookup using type comparison instead of HashSet
                Type patternType = typeof(TPattern);

                if (patternType == typeof(ITreeDisplayItem) ||
                    patternType == typeof(IBrowsablePattern) ||
                    patternType == typeof(IInvocationPattern) ||
                    patternType == typeof(ISupportDisposalNotification) ||
                    patternType == typeof(IRefreshPattern))
                {
                    return this as TPattern;
                }
            }
            else
            {
                // If this item has been deleted, it no longer supports any patterns
                // other than ISupportDisposalNotification.
                if (typeof(TPattern) == typeof(ISupportDisposalNotification))
                {
                    return this as TPattern;
                }
            }

            return null;
        }

        public void RaisePropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
