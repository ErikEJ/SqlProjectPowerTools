using Microsoft.VisualStudio.Shell.Interop;

namespace MarkdownLintVS.ErrorList
{

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
