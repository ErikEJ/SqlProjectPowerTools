using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell.TableControl;
using Microsoft.VisualStudio.Shell.TableManager;
using SqlProjectsPowerTools;

namespace MarkdownLintVS.ErrorList
{
    /// <summary>
    /// Table data source for the Error List window.
    /// </summary>
    [Export(typeof(MarkdownLintTableDataSource))]
    public class MarkdownLintTableDataSource : ITableDataSource
    {
        private static MarkdownLintTableDataSource _instance;

        public static MarkdownLintTableDataSource Instance => _instance;

        private readonly List<SinkManager> _managers = [];
        private readonly Dictionary<string, TableEntriesSnapshot> _snapshots =
            new(StringComparer.OrdinalIgnoreCase);

        // Separate storage for folder lint results (keyed by "FolderLint:" prefix)
        private const string _folderLintPrefix = "FolderLint:";
        private TableEntriesSnapshot _folderLintSnapshot;
        private readonly List<MarkdownLintError> _folderLintErrors = [];

        public string SourceTypeIdentifier => StandardTableDataSources.ErrorTableDataSource;

        public string Identifier => "MarkdownLint";

        public string DisplayName => Vsix.Name;

        /// <summary>
        /// Ensures the data source singleton is initialized.
        /// Call this from commands that need the data source before any document is opened.
        /// </summary>
        public static async System.Threading.Tasks.Task<MarkdownLintTableDataSource> EnsureInitializedAsync()
        {
            if (_instance != null)
            {
                return _instance;
            }

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            // Get the component model to trigger MEF composition
            IComponentModel componentModel = await VS.GetServiceAsync<SComponentModel, IComponentModel>();
            if (componentModel != null)
            {
                // This will trigger MEF to create the instance
                return componentModel.GetService<MarkdownLintTableDataSource>();
            }

            return null;
        }

        [ImportingConstructor]
        public MarkdownLintTableDataSource([Import] ITableManagerProvider tableManagerProvider)
        {
            _instance = this;

            ITableManager tableManager = tableManagerProvider.GetTableManager(StandardTables.ErrorsTable);
            tableManager.AddSource(this,
                StandardTableColumnDefinitions.Column,
                StandardTableColumnDefinitions.DocumentName,
                StandardTableColumnDefinitions.ErrorCode,
                StandardTableColumnDefinitions.ErrorSeverity,
                StandardTableColumnDefinitions.Line,
                StandardTableColumnDefinitions.Text,
                StandardTableColumnDefinitions.ProjectName);
        }

        public IDisposable Subscribe(ITableDataSink sink)
        {
            var manager = new SinkManager(this, sink);

            lock (_managers)
            {
                _managers.Add(manager);
            }

            // Send existing snapshots to new sink
            lock (_snapshots)
            {
                foreach (TableEntriesSnapshot snapshot in _snapshots.Values)
                {
                    sink.AddSnapshot(snapshot);
                }

                if (_folderLintSnapshot != null)
                {
                    sink.AddSnapshot(_folderLintSnapshot);
                }
            }

            return manager;
        }

        public void UpdateErrors(string filePath, IEnumerable<Linting.SqlAnalyzerDiagnosticInfo> violations)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return;
            }

            violations ??= [];

            var errors = violations.Select(v => new MarkdownLintError(v, filePath)).ToList();

            lock (_snapshots)
            {
                if (_snapshots.TryGetValue(filePath, out TableEntriesSnapshot oldSnapshot))
                {
                    _snapshots.Remove(filePath);
                    NotifySinks(sink => sink.RemoveSnapshot(oldSnapshot));
                }

                if (errors.Count > 0)
                {
                    var snapshot = new TableEntriesSnapshot(filePath, errors);
                    _snapshots[filePath] = snapshot;
                    NotifySinks(sink => sink.AddSnapshot(snapshot));
                }

                // Remove any folder lint errors for this file to avoid duplicates
                RemoveFolderLintErrorsForFile(filePath);
            }
        }

        public void ClearErrors(string filePath)
        {
            if (filePath == null)
            {
                return;
            }

            lock (_snapshots)
            {
                if (_snapshots.TryGetValue(filePath, out TableEntriesSnapshot snapshot))
                {
                    _snapshots.Remove(filePath);
                    NotifySinks(sink => sink.RemoveSnapshot(snapshot));
                }
            }
        }

        public void ClearAllErrors()
        {
            lock (_snapshots)
            {
                foreach (TableEntriesSnapshot snapshot in _snapshots.Values)
                {
                    NotifySinks(sink => sink.RemoveSnapshot(snapshot));
                }

                _snapshots.Clear();

                if (_folderLintSnapshot != null)
                {
                    NotifySinks(sink => sink.RemoveSnapshot(_folderLintSnapshot));
                    _folderLintSnapshot = null;
                }

                _folderLintErrors.Clear();
            }
        }

        /// <summary>
        /// Clears all folder lint errors (from Lint Folder command).
        /// </summary>
        public void ClearFolderLintErrors()
        {
            lock (_snapshots)
            {
                if (_folderLintSnapshot != null)
                {
                    NotifySinks(sink => sink.RemoveSnapshot(_folderLintSnapshot));
                    _folderLintSnapshot = null;
                }

                _folderLintErrors.Clear();
            }
        }

        /// <summary>
        /// Removes folder lint errors for a specific file.
        /// Called when a file is opened and linted individually to avoid duplicates.
        /// </summary>
        private void RemoveFolderLintErrorsForFile(string filePath)
        {
            // Must be called within lock(_snapshots)
            if (_folderLintSnapshot == null)
            {
                return;
            }

            var filteredErrors = _folderLintErrors
                .Where(e => !string.Equals(e.FilePath, filePath, StringComparison.OrdinalIgnoreCase))
                .ToList();

            // Only update if we actually removed something
            if (filteredErrors.Count < _folderLintErrors.Count)
            {
                NotifySinks(sink => sink.RemoveSnapshot(_folderLintSnapshot));
                _folderLintErrors.Clear();
                _folderLintErrors.AddRange(filteredErrors);

                if (_folderLintErrors.Count > 0)
                {
                    _folderLintSnapshot = new TableEntriesSnapshot(_folderLintPrefix + "Results", _folderLintErrors);
                    NotifySinks(sink => sink.AddSnapshot(_folderLintSnapshot));
                }
                else
                {
                    _folderLintSnapshot = null;
                }
            }
        }

        private void NotifySinks(Action<ITableDataSink> action)
        {
            lock (_managers)
            {
                foreach (SinkManager manager in _managers)
                {
                    action(manager.Sink);
                }
            }
        }

        internal void RemoveSinkManager(SinkManager manager)
        {
            lock (_managers)
            {
                _managers.Remove(manager);
            }
        }
    }

    /// <summary>
    /// Manages subscription to the table data sink.
    /// </summary>
    internal class SinkManager(MarkdownLintTableDataSource source, ITableDataSink sink) : IDisposable
    {
        public ITableDataSink Sink { get; } = sink;

        public void Dispose()
        {
            source.RemoveSinkManager(this);
        }
    }

    /// <summary>
    /// Snapshot of error entries for a file.
    /// </summary>
    internal class TableEntriesSnapshot(string filePath, List<MarkdownLintError> errors) : ITableEntriesSnapshot
    {
        public string FilePath { get; } = filePath;

        public int VersionNumber { get; } = 1;

        public int Count => errors.Count;

        public IEnumerable<MarkdownLintError> GetErrors() => errors;

        public int IndexOf(int currentIndex, ITableEntriesSnapshot newerSnapshot)
        {
            return currentIndex;
        }

        public bool TryGetValue(int index, string keyName, out object content)
        {
            if (index < 0 || index >= errors.Count)
            {
                content = null;
                return false;
            }

            MarkdownLintError error = errors[index];

            switch (keyName)
            {
                case StandardTableKeyNames.DocumentName:
                    content = error.FilePath;
                    return true;

                case StandardTableKeyNames.Line:
                    content = error.Line;
                    return true;

                case StandardTableKeyNames.Column:
                    content = error.Column;
                    return true;

                case StandardTableKeyNames.Text:
                    content = error.Message;
                    return true;

                case StandardTableKeyNames.ErrorCode:
                    content = error.ErrorCode;
                    return true;

                case StandardTableKeyNames.ErrorSeverity:
                    content = error.Severity;
                    return true;

                case StandardTableKeyNames.ErrorCategory:
                    content = "Markdown";
                    return true;

                case StandardTableKeyNames.BuildTool:
                    content = "MarkdownLint";
                    return true;

                case StandardTableKeyNames.HelpLink:
                    content = error.HelpLink;
                    return true;

                case StandardTableKeyNames.ErrorCodeToolTip:
                    content = error.Description;
                    return true;

                default:
                    content = null;
                    return false;
            }
        }

        public void StartCaching()
        {
        }

        public void StopCaching()
        {
        }

        public void Dispose()
        {
        }
    }

    /// <summary>
    /// Represents an error in the error list.
    /// </summary>
    internal class MarkdownLintError
    {
        public string FilePath { get; }

        public int Line { get; }

        public int Column { get; }

        public string Message { get; }

        public string ErrorCode { get; }

        public string Description { get; }

        public string HelpLink { get; }

        public __VSERRORCATEGORY Severity { get; }

        public MarkdownLintError(Linting.SqlAnalyzerDiagnosticInfo violation, string filePath)
        {
            FilePath = filePath;
            Line = violation.Range.StartLine;
            Column = violation.Range.StartColumn;
            Message = violation.Message;
            ErrorCode = violation.ErrorCode;
            Description = violation.Message;
            HelpLink = violation.HelpLink?.ToString() ?? string.Empty;
            Severity = __VSERRORCATEGORY.EC_WARNING;
        }

        public MarkdownLintError(
            string filePath,
            int line,
            int column,
            string errorCode,
            string message,
            string description,
            string helpLink,
            Linting.DiagnosticSeverity severity)
        {
            FilePath = filePath;
            Line = line;
            Column = column;
            ErrorCode = errorCode;
            Message = message;
            Description = description ?? string.Empty;
            HelpLink = helpLink ?? string.Empty;
            Severity = GetSeverity(severity);
        }

        private static __VSERRORCATEGORY GetSeverity(Linting.DiagnosticSeverity severity)
        {
            return severity switch
            {
                Linting.DiagnosticSeverity.Error => __VSERRORCATEGORY.EC_ERROR,
                Linting.DiagnosticSeverity.Warning => __VSERRORCATEGORY.EC_WARNING,
                _ => __VSERRORCATEGORY.EC_MESSAGE,
            };
        }
    }
}
