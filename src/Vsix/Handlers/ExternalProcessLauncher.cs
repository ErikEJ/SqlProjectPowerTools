using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace SqlProjectsPowerTools
{
    public static class ExternalProcessLauncher
    {
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