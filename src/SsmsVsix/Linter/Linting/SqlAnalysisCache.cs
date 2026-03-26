using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading;
using Microsoft.VisualStudio.Text;

namespace SqlProjectsPowerTools.Linting
{
    /// <summary>
    /// Provides shared analysis caching for T-SQL documents. Both the tagger and error list use this to avoid
    /// duplicate parsing.
    /// </summary>
    [Export(typeof(SqlAnalysisCache))]
    public class SqlAnalysisCache
    {
        private static readonly object _propertyKey = typeof(SqlAnalysisCache);
        private static readonly object _pendingAnalysisKey = typeof(SqlAnalysisCache).FullName + ".PendingAnalysis";

        /// <summary>
        /// Delay in milliseconds before analyzing after the last keystroke.
        /// </summary>
        private const int _debounceDelayMs = 300;

        /// <summary>
        /// Event raised when analysis results are updated for a buffer.
        /// </summary>
        public event EventHandler<AnalysisUpdatedEventArgs> AnalysisUpdated;

        /// <summary>
        /// Triggers analysis without debounce delay on a background thread.
        /// Use this for initial file open or when options change. The snapshot and text are
        /// captured on the calling thread, then analysis runs off the UI thread and notifies
        /// listeners via AnalysisUpdated.
        /// </summary>
        public void AnalyzeImmediate(ITextBuffer buffer, string filePath, string sqlVersion, string rules, string projectName)
        {
            ITextSnapshot snapshot = buffer.CurrentSnapshot;
            if (HasPendingAnalysisForSnapshot(buffer, snapshot.Version.VersionNumber))
            {
                return;
            }

            // Cancel any pending debounced analysis
            CancelPendingAnalysis(buffer);

            var text = snapshot.GetText();

            // Run analysis on a background thread without debounce delay
            var pendingAnalysis = new PendingAnalysis(new CancellationTokenSource(), snapshot.Version.VersionNumber);
            buffer.Properties[_pendingAnalysisKey] = pendingAnalysis;
            PerformAnalysisNowAsync(buffer, filePath, sqlVersion, rules, projectName, pendingAnalysis.CancellationTokenSource.Token, snapshot, text).FireAndForget();
        }

        /// <summary>
        /// Triggers debounced analysis on a background thread. Waits for a pause in typing before analyzing to reduce
        /// CPU usage. Use this when the buffer content changes during editing.
        /// </summary>
        public void InvalidateAndAnalyze(ITextBuffer buffer, string filePath, string sqlVersion, string rules, string projectName)
        {
            // Cancel any pending analysis for this buffer
            CancelPendingAnalysis(buffer);

            var cts = new CancellationTokenSource();
            ITextSnapshot snapshot = buffer.CurrentSnapshot;
            buffer.Properties[_pendingAnalysisKey] = new PendingAnalysis(cts, snapshot.Version.VersionNumber);
            var text = snapshot.GetText();

            // Pass the token, not the CTS, to avoid accessing disposed CTS
            PerformAnalysisAsync(buffer, filePath, sqlVersion, rules, projectName, cts.Token, snapshot, text).FireAndForget();
        }

        private async Task PerformAnalysisAsync(ITextBuffer buffer, string filePath, string sqlVersion, string rules, string projectName, CancellationToken cancellationToken, ITextSnapshot snapshot, string text)
        {
            try
            {
                await Task.Delay(_debounceDelayMs, cancellationToken);

                if (!cancellationToken.IsCancellationRequested)
                {
                    PerformAnalysis(buffer, snapshot, text, filePath, sqlVersion, rules, projectName, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when user types again before delay expires
            }
            catch (ObjectDisposedException)
            {
                // CancellationTokenSource was disposed - this is fine, just stop
            }
        }

        private async Task PerformAnalysisNowAsync(ITextBuffer buffer, string filePath, string sqlVersion, string rules, string projectName, CancellationToken cancellationToken, ITextSnapshot snapshot, string text)
        {
            try
            {
                // Yield to background thread immediately (no debounce delay)
                await Task.Run(() => PerformAnalysis(buffer, snapshot, text, filePath, sqlVersion, rules, projectName, cancellationToken), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Analysis was cancelled
            }
            catch (ObjectDisposedException)
            {
                // CancellationTokenSource was disposed
            }
            finally
            {
                ClearPendingAnalysisIfSnapshotMatches(buffer, snapshot.Version.VersionNumber);
            }
        }

        /// <summary>
        /// Performs the actual analysis and updates the cache.
        /// </summary>
        private void PerformAnalysis(ITextBuffer buffer, ITextSnapshot snapshot, string text, string filePath, string sqlVersion, string rules, string projectName, CancellationToken cancellationToken = default)
        {
            try
            {
                // Return empty violations if linting is disabled
                var violations = new List<SqlAnalyzerDiagnosticInfo>();

                ThreadHelper.JoinableTaskFactory.Run(async () =>
                {
                    violations = await AnalyzerUtilities.Instance.AnalyzeAsync(text, rules, sqlVersion, cancellationToken);
                });

                var result = new CachedAnalysisResult(snapshot.Version.VersionNumber, violations);

                buffer.Properties[_propertyKey] = result;

                AnalysisUpdated?.Invoke(this, new AnalysisUpdatedEventArgs(buffer, snapshot, violations, filePath, projectName));
            }
            catch (OperationCanceledException)
            {
                // Analysis was cancelled — don't update cache or notify listeners
            }
            catch (Exception ex)
            {
                ex.Log("Shared .sql script analysis failed");
            }
        }

        /// <summary>
        /// Cancels any pending debounced analysis for the buffer.
        /// </summary>
        private void CancelPendingAnalysis(ITextBuffer buffer)
        {
            if (buffer.Properties.TryGetProperty(_pendingAnalysisKey, out PendingAnalysis pendingAnalysis))
            {
                _ = buffer.Properties.RemoveProperty(_pendingAnalysisKey);

                // Cancel first, then dispose - order matters for race condition safety
                // The token is passed by value to the async method, so accessing IsCancellationRequested
                // after Cancel() is safe, but we should not dispose until after Task.Delay returns
                try
                {
                    pendingAnalysis.CancellationTokenSource.Cancel();
                }
                finally
                {
                    // Dispose is safe here because Task.Delay will throw OperationCanceledException
                    // before accessing the CTS again, and we catch ObjectDisposedException as a fallback
                    pendingAnalysis.CancellationTokenSource.Dispose();
                }
            }
        }

        private static bool HasPendingAnalysisForSnapshot(ITextBuffer buffer, int snapshotVersion)
        {
            return buffer.Properties.TryGetProperty(_pendingAnalysisKey, out PendingAnalysis pendingAnalysis)
                && pendingAnalysis.SnapshotVersion == snapshotVersion;
        }

        private static void ClearPendingAnalysisIfSnapshotMatches(ITextBuffer buffer, int snapshotVersion)
        {
            if (buffer.Properties.TryGetProperty(_pendingAnalysisKey, out PendingAnalysis pendingAnalysis)
                && pendingAnalysis.SnapshotVersion == snapshotVersion)
            {
                _ = buffer.Properties.RemoveProperty(_pendingAnalysisKey);
                pendingAnalysis.CancellationTokenSource.Dispose();
            }
        }
    }
}
