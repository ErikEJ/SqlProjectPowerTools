using System;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;

namespace ExtensionPack
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [Guid(ExtensionPackPackage.PackageGuidString)]
    public sealed class ExtensionPackPackage : AsyncPackage
    {
        public const string PackageGuidString = "a8e23dd0-b09e-4edb-9ab5-17add7e721d4";
    }
}