using System.Collections.Generic;

namespace MarkdownLintVS.Linting
{
    /// <summary>
    /// Cached analysis result for a text buffer.
    /// </summary>
    internal class CachedAnalysisResult(int snapshotVersion, IReadOnlyList<SqlAnalyzerDiagnosticInfo> violations)
    {
        public int SnapshotVersion { get; } = snapshotVersion;

        public IReadOnlyList<SqlAnalyzerDiagnosticInfo> Violations { get; } = violations;
    }
}
