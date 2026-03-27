using System.Diagnostics;
using System.Net;
using System.Xml.Linq;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Shell.Interop;

namespace SqlProjectsPowerTools
{
    internal static class UpdateChecker
    {
        private static readonly XNamespace AtomNamespace = "http://www.w3.org/2005/Atom";

        public static async Task CheckForUpdatesAsync(string extensionId, string currentVersion, string extensionName)
        {
            try
            {
                var options = await ToolOptions.GetLiveInstanceAsync();
                if (!options.CheckForUpdates)
                {
                    return;
                }

                var feedUrl = $"https://www.vsixgallery.com/feed/extension/{extensionId}";
                string feedContent;

                using (var webClient = new WebClient())
                {
                    feedContent = await webClient.DownloadStringTaskAsync(feedUrl);
                }

                var latestVersion = ParseVersionFromFeed(feedContent);
                if (latestVersion != null && IsNewerVersion(latestVersion, currentVersion))
                {
                    await ShowUpdateNotificationAsync(extensionId, extensionName, latestVersion);
                }
            }
            catch (Exception ex)
            {
                ex.Log();
            }
        }

        private static string ParseVersionFromFeed(string feedContent)
        {
            var doc = XDocument.Parse(feedContent);
            var firstEntry = doc.Root?.Element(AtomNamespace + "entry");

            if (firstEntry == null)
            {
                return null;
            }

            // Try to get version from the link href (e.g. /extension/{id}/{version})
            var linkHref = firstEntry.Element(AtomNamespace + "link")?.Attribute("href")?.Value;
            if (linkHref != null)
            {
                var segments = linkHref.TrimEnd('/').Split('/');
                if (segments.Length > 0)
                {
                    var lastSegment = segments[segments.Length - 1];
                    if (Version.TryParse(lastSegment, out _))
                    {
                        return lastSegment;
                    }
                }
            }

            // Fallback: try to get version from the title ("Extension Name X.Y.Z")
            var title = firstEntry.Element(AtomNamespace + "title")?.Value;
            if (title != null)
            {
                var parts = title.Trim().Split(' ');
                if (parts.Length > 0)
                {
                    var lastPart = parts[parts.Length - 1];
                    if (Version.TryParse(lastPart, out _))
                    {
                        return lastPart;
                    }
                }
            }

            return null;
        }

        private static bool IsNewerVersion(string latestVersion, string currentVersion)
        {
            if (Version.TryParse(latestVersion, out var latest) &&
                Version.TryParse(currentVersion, out var current))
            {
                return latest > current;
            }

            return false;
        }

        private static async Task ShowUpdateNotificationAsync(string extensionId, string extensionName, string newVersion)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var downloadUrl = $"https://www.vsixgallery.com/extension/{extensionId}";

            var model = new InfoBarModel(
                $"{extensionName} {newVersion} is available.",
                new IVsInfoBarActionItem[] { new InfoBarHyperlink("Download") },
                KnownMonikers.StatusInformation);

            var infoBar = await VS.InfoBar.CreateAsync(model);
            if (infoBar != null)
            {
                infoBar.ActionItemClicked += (sender, e) =>
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo(downloadUrl) { UseShellExecute = true });
                    }
                    catch (Exception ex)
                    {
                        ex.Log();
                    }
                    finally
                    {
                        if (sender is InfoBar bar)
                        {
                            bar.Close();
                        }
                    }
                };

                await infoBar.TryShowInfoBarUIAsync();
            }
        }
    }
}
