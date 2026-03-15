using System.IO;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Imaging.Interop;
using Microsoft.VisualStudio.Shell.Interop;

namespace SqlProjectsPowerTools.TreeViewer
{
    internal static class IconMapper
    {
        private static IVsImageService2 imageService;

        private static IVsImageService2 GetImageService()
        {
            return imageService ??= VS.GetRequiredService<SVsImageService, IVsImageService2>();
        }

        public static ImageMoniker GetIcon(this FileSystemInfo info, bool isOpen)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (info == null)
            {
                return KnownMonikers.DatabaseApplication;
            }

            if (info is FileInfo file)
            {
                if (file.Extension.Equals(".dacpac", StringComparison.OrdinalIgnoreCase))
                {
                    return KnownMonikers.Extension;
                }

                ImageMoniker moniker = GetImageService().GetImageMonikerForFile(file.FullName);

                if (moniker.Id < 0)
                {
                    moniker = KnownMonikers.DatabaseApplication;
                }

                return moniker;
            }

            return info.FullName.EndsWith(".dacpac", StringComparison.OrdinalIgnoreCase)
                ? KnownMonikers.DatabaseApplication
                : isOpen ? KnownMonikers.FolderOpened : KnownMonikers.FolderClosed;
        }
    }
}
