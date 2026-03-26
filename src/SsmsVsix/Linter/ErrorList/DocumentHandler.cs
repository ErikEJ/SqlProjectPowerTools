using MarkdownLintVS.Linting;
using Microsoft.VisualStudio.Text.Editor;

namespace MarkdownLintVS.ErrorList
{

    /// <summary>
    /// Handles document events for a specific text view.
    /// Listens to shared analysis cache for results.
    /// Note: Debouncing is handled by MarkdownAnalysisCache, not here.
    /// </summary>
    internal sealed class DocumentHandler : IDisposable
    {
        private readonly ITextView _textView;
        private readonly MarkdownLintTableDataSource _tableDataSource;
        private readonly MarkdownAnalysisCache _analysisCache;
        private readonly string _filePath;
        private bool _disposed;

        public DocumentHandler(
            ITextView textView,
            MarkdownLintTableDataSource tableDataSource,
            MarkdownAnalysisCache analysisCache,
            string filePath)
        {
            _textView = textView;
            _tableDataSource = tableDataSource;
            _analysisCache = analysisCache;
            _filePath = filePath;

            // Only listen for analysis results — the tagger owns triggering analysis
            // (on buffer changes, option saves, and initial file open).
            _analysisCache.AnalysisUpdated += OnAnalysisUpdated;
        }

        private void OnAnalysisUpdated(object sender, AnalysisUpdatedEventArgs e)
        {
            if (e.Buffer != _textView.TextBuffer)
            {
                return;
            }

            // Update error list with new results
            _tableDataSource?.UpdateErrors(e.FilePath, e.Violations);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _analysisCache.AnalysisUpdated -= OnAnalysisUpdated;
                _tableDataSource?.ClearErrors(_filePath);
            }
        }
    }
}
