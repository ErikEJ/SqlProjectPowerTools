using System.ComponentModel.Composition;
using MarkdownLintVS.Linting;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace MarkdownLintVS.ErrorList
{
    /// <summary>
    /// Listens for document changes and updates the error list.
    /// Uses shared MarkdownAnalysisCache to avoid duplicate parsing.
    /// </summary>
    [Export(typeof(ITextViewCreationListener))]
    [ContentType("SQL")]
    [ContentType("SQL Server Tools")]
    [TextViewRole(PredefinedTextViewRoles.Document)]
    public class MarkdownDocumentListener : ITextViewCreationListener
    {
        [Import]
        internal MarkdownLintTableDataSource TableDataSource { get; set; }

        [Import]
        internal MarkdownAnalysisCache AnalysisCache { get; set; }

        public void TextViewCreated(ITextView textView)
        {
            var filePath = GetFilePath(textView);
            var handler = new DocumentHandler(textView, TableDataSource, AnalysisCache, filePath);
            textView.Closed += (s, e) => handler.Dispose();
        }

        private static string GetFilePath(ITextView textView)
        {
            if (textView.TextBuffer.Properties.TryGetProperty(typeof(ITextDocument), out ITextDocument document))
            {
                return document.FilePath;
            }

            return null;
        }
    }
}
