using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
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

        public string SourceTypeIdentifier => StandardTableDataSources.ErrorTableDataSource;

        public string Identifier => "SqlProjects";

        public string DisplayName => Vsix.Name;

        [ImportingConstructor]
        public MarkdownLintTableDataSource([Import] ITableManagerProvider tableManagerProvider)
        {
            _instance = this;

            ITableManager tableManager = tableManagerProvider.GetTableManager(StandardTables.ErrorsTable);
            tableManager.AddSource(
                this,
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
            }

            return manager;
        }

        public void UpdateErrors(string filePath, string projectName, IEnumerable<Linting.SqlAnalyzerDiagnosticInfo> violations)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return;
            }

            violations ??= [];

            var errors = violations.Select(v => new MarkdownLintError(v, filePath, projectName)).ToList();

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
}
