using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace SqlProjectsPowerTools.TreeViewer
{
    public static class Debouncer
    {
        private static readonly ConcurrentDictionary<string, CancellationTokenSource> Tokens = new();

        public static void Debounce(string uniqueKey, Action action, int milliseconds)
        {
            CancellationTokenSource token = Tokens.AddOrUpdate(
                uniqueKey,
                (key) =>
                {
                    return new CancellationTokenSource();
                },
                (key, existingToken) =>
                {
                    existingToken.Cancel();
                    return new CancellationTokenSource();
                });

            _ = Task.Delay(milliseconds, token.Token).ContinueWith(
                task =>
                {
                    if (!task.IsCanceled)
                    {
                        action();
                        if (Tokens.TryRemove(uniqueKey, out CancellationTokenSource cts))
                        {
                            cts.Dispose();
                        }
                    }
                },
                token.Token,
                TaskContinuationOptions.None,
                TaskScheduler.Default);
        }
    }
}