using System.Diagnostics;

namespace SqlProjectsPowerTools
{
    internal static class PackageManager
    {
        private static VsixPackage package;

        public static VsixPackage Package
        {
            get
            {
                Debug.Assert(package != null, "PackageManager.Package: package is null and someone is trying to access it!");
                return package;
            }

            set
            {
                package = value;
            }
        }
    }
}