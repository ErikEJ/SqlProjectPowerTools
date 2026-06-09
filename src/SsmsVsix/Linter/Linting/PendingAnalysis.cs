using System.Threading;

namespace SqlProjectsPowerTools.Linting
{
    internal sealed class PendingAnalysis(CancellationTokenSource cancellationTokenSource, int snapshotVersion)
    {
        public CancellationTokenSource CancellationTokenSource { get; } = cancellationTokenSource;

        public int SnapshotVersion { get; } = snapshotVersion;
    }
}
