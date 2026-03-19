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
                (key) => // key not found - create new
                {
                    return new CancellationTokenSource();
                },
                (key, existingToken) => // key found - cancel task and recreate
                {
                    existingToken.Cancel();
                    existingToken.Dispose();
                    return new CancellationTokenSource();
                });

            // schedule execution after pause
            _ = Task.Delay(milliseconds, token.Token).ContinueWith(
                task =>
                {
                    if (task.IsCanceled)
                    {
                        CleanupToken(uniqueKey, token);
                        return;
                    }

                    try
                    {
                        action(); // run
                    }
                    finally
                    {
                        CleanupToken(uniqueKey, token);
                    }
                },
                token.Token,
                TaskContinuationOptions.None,
                TaskScheduler.Default); // Explicitly specify TaskScheduler.Default
        }

        private static void CleanupToken(string uniqueKey, CancellationTokenSource token)
        {
            if (Tokens.TryGetValue(uniqueKey, out CancellationTokenSource currentToken) && ReferenceEquals(currentToken, token))
            {
                Tokens.TryRemove(uniqueKey, out _);
            }

            token.Dispose();
        }
    }
}