using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using Microsoft.Internal.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Imaging.Interop;
using Microsoft.VisualStudio.Language.Intellisense;

namespace SqlProjectsPowerTools.TreeViewer
{
    [DebuggerDisplay("{Text}")]
    internal class DacpacItemNode :
        IAttachedCollectionSource,
        ITreeDisplayItemWithImages,
        IPrioritizedComparable,
        IBrowsablePattern,
        IInteractionPatternProvider,

        // IContextMenuPattern,
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
        private bool reloadRequested;
        private int loadGeneration;
        private bool showDacpacIcon;
        private CancellationTokenSource loadCancellationTokenSource;

        public DacpacItemNode(IAttachedCollectionSource source, string outputPath, string dacpacPath, string tooltipContent = null)
        {
            SourceItem = source;
            Rebuild(outputPath, dacpacPath, tooltipContent);
        }

        public void Rebuild(string outputPath, string dacpacPath, string tooltipContent = null)
        {
            string newText = !string.IsNullOrEmpty(dacpacPath) && File.Exists(dacpacPath)
                ? Path.GetFileName(dacpacPath)
                : Path.GetFileName(outputPath) ?? ".dacpac content";
            bool newIsCut = false;
            FileSystemInfo newInfo;
            bool newHasItems;

            lock (loadLock)
            {
                isLoaded = false;
                loadGeneration++;

                if (isLoading)
                {
                    reloadRequested = true;
                    loadCancellationTokenSource?.Cancel();
                }
            }

            if (Directory.Exists(outputPath))
            {
                newInfo = new DirectoryInfo(outputPath);
                newHasItems = CheckHasItemsQuick(newInfo as DirectoryInfo);
            }
            else if (File.Exists(outputPath))
            {
                newInfo = new FileInfo(outputPath);
                newHasItems = false;
            }
            else
            {
                newInfo = null;
                newIsCut = true;
                newHasItems = false;
            }

            string oldText = Text;
            bool oldIsCut = IsCut;
            bool oldHasItems = HasItems;

            Info = newInfo;
            Text = newText;
            IsCut = newIsCut;
            HasItems = newHasItems;
            showDacpacIcon = !string.IsNullOrEmpty(dacpacPath) &&
                !string.Equals(dacpacPath, "root", StringComparison.OrdinalIgnoreCase) &&
                dacpacPath.EndsWith(".dacpac", StringComparison.OrdinalIgnoreCase);

            if (!string.Equals(oldText, Text, StringComparison.Ordinal))
            {
                RaisePropertyChanged(nameof(Text));
            }

            if (oldIsCut != IsCut)
            {
                RaisePropertyChanged(nameof(IsCut));
            }

            if (oldHasItems != HasItems)
            {
                RaisePropertyChanged(nameof(HasItems));
            }

            if (!string.IsNullOrEmpty(dacpacPath) || tooltipContent != null)
            {
                object oldTooltip = ToolTipContent;
                ToolTipContent = tooltipContent ?? SetTooltip(dacpacPath);

                if (!Equals(oldTooltip, ToolTipContent))
                {
                    RaisePropertyChanged(nameof(ToolTipContent));
                }
            }
        }

        private static bool CheckHasItemsQuick(DirectoryInfo directory)
        {
            if (directory != null)
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
            CancellationToken cancellationToken;
            int loadGen;
            bool restartLoad = false;

            lock (loadLock)
            {
                if (isLoading || isLoaded || IsDisposed)
                {
                    return;
                }

                isLoading = true;
                loadGen = this.loadGeneration;
                loadCancellationTokenSource?.Cancel();
                loadCancellationTokenSource = new CancellationTokenSource();
                cancellationToken = loadCancellationTokenSource.Token;
            }

            try
            {
                // Do the heavy work off the UI thread
                List<DacpacItemNode> childNodes = await Task.Run(() => LoadChildrenOffUIThread(cancellationToken), cancellationToken);

                if (cancellationToken.IsCancellationRequested || IsDisposed || !IsCurrentLoad(loadGen))
                {
                    DisposeChildren(newChildren: childNodes);
                    return;
                }

                // Switch back to UI thread to update the collection
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

                if (cancellationToken.IsCancellationRequested || IsDisposed || !IsCurrentLoad(loadGen))
                {
                    DisposeChildren(newChildren: childNodes);
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

                    if (reloadRequested && !IsDisposed)
                    {
                        reloadRequested = false;
                        restartLoad = true;
                    }
                }

                if (restartLoad)
                {
                    _ = LoadChildrenAsync();
                }
            }
        }

        private bool IsCurrentLoad(int loadGeneration)
        {
            lock (loadLock)
            {
                return loadGeneration == this.loadGeneration;
            }
        }

        private static void DisposeChildren(IEnumerable<DacpacItemNode> newChildren)
        {
            foreach (DacpacItemNode child in newChildren)
            {
                child.Dispose();
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

                        if (IsInternalMetadataFile(item))
                        {
                            continue;
                        }

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

        private static bool IsInternalMetadataFile(FileSystemInfo item)
        {
            return item is FileInfo && string.Equals(item.Name, ".dacpacstamp", StringComparison.OrdinalIgnoreCase);
        }

        private void UpdateChildrenOnUIThread(List<DacpacItemNode> newChildren)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            children ??= [];

            if (AreChildrenEquivalent(children, newChildren))
            {
                foreach (DacpacItemNode item in newChildren)
                {
                    item.Dispose();
                }

                isLoaded = true;

                bool hadItems = HasItems;
                HasItems = children.Any();
                if (hadItems != HasItems)
                {
                    RaisePropertyChanged(nameof(HasItems));
                }

                return;
            }

            // Dispose old children
            foreach (DacpacItemNode child in children)
            {
                child.Dispose();
            }

            children.BeginBulkOperation();
            children.Clear();
            children.AddRange(newChildren);
            children.EndBulkOperation();

            isLoaded = true;
            HasItems = children.Any();

            RaisePropertyChanged(nameof(Items));
            RaisePropertyChanged(nameof(HasItems));
        }

        private static bool AreChildrenEquivalent(IList<DacpacItemNode> existingChildren, IList<DacpacItemNode> newChildren)
        {
            if (existingChildren.Count != newChildren.Count)
            {
                return false;
            }

            for (int i = 0; i < existingChildren.Count; i++)
            {
                string existingPath = existingChildren[i].Info?.FullName;
                string newPath = newChildren[i].Info?.FullName;

                if (!string.Equals(existingPath, newPath, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return true;
        }

        public string Text { get; set; }

        public string ToolTipText => null;

        public string StateToolTipText => null;

        public object ToolTipContent { get; set; }

        public FontWeight FontWeight => FontWeights.Normal;

        public System.Windows.FontStyle FontStyle => FontStyles.Normal;

        public ImageMoniker IconMoniker => showDacpacIcon ? KnownMonikers.DatabaseApplication : Info.GetIcon(false);

        public ImageMoniker ExpandedIconMoniker => showDacpacIcon ? KnownMonikers.DatabaseApplication : Info.GetIcon(true);

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
                loadGeneration++;

                if (isLoading)
                {
                    reloadRequested = true;
                }

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
                loadGeneration++;

                if (isLoading)
                {
                    reloadRequested = true;
                }

                loadCancellationTokenSource?.Cancel();
            }

            // Await the loading to complete
            await LoadChildrenAsync();
        }

        public void CancelLoad()
        {
            lock (loadLock)
            {
                loadGeneration++;
            }

            loadCancellationTokenSource?.Cancel();
        }

        public int CompareTo(object obj)
        {
            if (obj is not DacpacItemNode node)
            {
                return obj is ITreeDisplayItem item ? StringComparer.Compare(Text, item.Text) : 0;
            }

            var thisIsDirectory = Info is DirectoryInfo;
            var otherIsDirectory = node.Info is DirectoryInfo;

            if (thisIsDirectory != otherIsDirectory)
            {
                return thisIsDirectory ? -1 : 1;
            }

            return StringComparer.Compare(Text, node.Text);
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
                return $"Last updated: {Info.LastWriteTime}\r\nDACPAC file: {Path.GetFileName(dacpacFile)}";
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
