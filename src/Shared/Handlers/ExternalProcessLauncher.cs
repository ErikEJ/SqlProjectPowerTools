using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace SqlProjectsPowerTools
{
    public static class ExternalProcessLauncher
    {
        public static async Task<(int ExitCode, string StandardOutput, string StandardError)> RunProcessWithExitCodeAsync(ProcessStartInfo startInfo)
        {
            startInfo.UseShellExecute = false;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            startInfo.CreateNoWindow = true;
            startInfo.StandardOutputEncoding = Encoding.UTF8;
            startInfo.StandardErrorEncoding = Encoding.UTF8;

            var standardOutput = new StringBuilder();
            using (var process = Process.Start(startInfo))
            {
                if (process == null)
                {
                    return (-1, string.Empty, $"Failed to start process '{startInfo.FileName}'.");
                }

                var standardOutputTask = process.StandardOutput.ReadToEndAsync();
                var standardErrorTask = process.StandardError.ReadToEndAsync();
                await Task.WhenAll(standardOutputTask, standardErrorTask);
                process.WaitForExit();
                standardOutput.Append(standardOutputTask.Result);
                var error = standardErrorTask.Result;

                return (process.ExitCode, standardOutput.ToString(), error);
            }
        }

        public static async Task<string> RunProcessAsync(ProcessStartInfo startInfo)
        {
            var (_, standardOutput, error) = await RunProcessWithExitCodeAsync(startInfo);
            var result = standardOutput;
            if (string.IsNullOrEmpty(result) && !string.IsNullOrEmpty(error))
            {
                result = "Error:" + Environment.NewLine + error;
            }

            return result;
        }
    }
}
