using Microsoft.VisualStudio.Text;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading;

namespace MarkdownLintVS.Linting
{

    /// <summary>
    /// Event args for analysis completion.
    /// </summary>
    public class AnalysisUpdatedEventArgs(
        ITextBuffer buffer,
        ITextSnapshot snapshot,
        IReadOnlyList<SqlAnalyzerDiagnosticInfo> violations,
        string filePath) : EventArgs
    {
        public ITextBuffer Buffer { get; } = buffer;

        public ITextSnapshot Snapshot { get; } = snapshot;

        public IReadOnlyList<SqlAnalyzerDiagnosticInfo> Violations { get; } = violations;

        public string FilePath { get; } = filePath;
    }
}
