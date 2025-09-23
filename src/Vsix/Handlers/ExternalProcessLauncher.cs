using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace SqlProjectsPowerTools
{
    public class ExternalProcessLauncher
    {
        private readonly Project project;

        public ExternalProcessLauncher(Project project)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            this.project = project;
        }

        public static List<Tuple<string, string>> BuildModelResult(string modelInfo)
        {
            var result = new List<Tuple<string, string>>();

            var contexts = modelInfo.Split(new[] { "DbContext:" + Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var context in contexts)
            {
                if (context.StartsWith("info:", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (context.StartsWith("dbug:", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (context.StartsWith("warn:", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (context.IndexOf("DebugView:", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                var parts = context.Split(new[] { "DebugView:" + Environment.NewLine }, StringSplitOptions.None);
                result.Add(new Tuple<string, string>(parts[0].Trim(), parts.Length > 1 ? parts[1].Trim() : string.Empty));
            }

            return result;
        }

        public static async Task<string> RunProcessAsync(ProcessStartInfo startInfo)
        {
            startInfo.UseShellExecute = false;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            startInfo.CreateNoWindow = true;
            startInfo.StandardOutputEncoding = Encoding.UTF8;
            startInfo.StandardErrorEncoding = Encoding.UTF8;

            var standardOutput = new StringBuilder();
            var error = string.Empty;
            using (var process = Process.Start(startInfo))
            {
                while (process != null && !process.HasExited)
                {
                    standardOutput.Append(await process.StandardOutput.ReadToEndAsync());
                }

                if (process != null)
                {
                    standardOutput.Append(await process.StandardOutput.ReadToEndAsync());
                }

                if (process != null)
                {
                    error = await process.StandardError.ReadToEndAsync();
                }
            }

            var result = standardOutput.ToString();
            if (string.IsNullOrEmpty(result) && !string.IsNullOrEmpty(error))
            {
                result = "Error:" + Environment.NewLine + error;
            }

            return result;
        }
    }
}