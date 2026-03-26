using Microsoft.VisualStudio.Shell.TableManager;

namespace SqlProjectsPowerTools.ErrorList
{

    /// <summary>
    /// Manages subscription to the table data sink.
    /// </summary>
    internal sealed class SinkManager(MarkdownLintTableDataSource source, ITableDataSink sink) : IDisposable
    {
        public ITableDataSink Sink { get; } = sink;

        public void Dispose()
        {
            source.RemoveSinkManager(this);
        }
    }
}
