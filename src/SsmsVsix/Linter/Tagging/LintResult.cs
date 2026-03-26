using Microsoft.VisualStudio.Text;
using SqlProjectsPowerTools.Linting;

namespace SqlProjectsPowerTools.Tagging
{
    /// <summary>
    /// Represents a lint result with tracking span support.
    /// </summary>
    public class LintResult
    {
        private readonly ITrackingSpan _trackingSpan;

        public string RuleId { get; }

        public string Message { get; }

        public string DocumentationUrl { get; }

        public Linting.DiagnosticSeverity Severity { get; }

        public int Start { get; }

        public LintResult(SqlAnalyzerDiagnosticInfo violation, ITextSnapshot snapshot)
        {
            RuleId = violation.ErrorCode;
            Message = violation.Message;
            DocumentationUrl = violation.HelpLink?.ToString();
            Severity = DiagnosticSeverity.Warning;

            // Calculate span from line/column
            ITextSnapshotLine line = snapshot.GetLineFromLineNumber(Math.Min(violation.Range.StartLine, snapshot.LineCount - 1));
            var startIndex = line.Start.Position + Math.Min(violation.Range.StartColumn, line.Length);
            var endIndex = line.Start.Position + Math.Min(violation.Range.EndColumn, line.Length);

            if (endIndex <= startIndex)
            {
                endIndex = Math.Min(startIndex + 1, line.End.Position);
            }

            var span = new Span(startIndex, Math.Max(1, endIndex - startIndex));
            Start = span.Start;
            _trackingSpan = snapshot.CreateTrackingSpan(span, SpanTrackingMode.EdgeExclusive);
        }

        public SnapshotSpan? GetTranslatedSpan(ITextSnapshot snapshot)
        {
            try
            {
                return _trackingSpan.GetSpan(snapshot);
            }
            catch
            {
                return null;
            }
        }
    }
}
