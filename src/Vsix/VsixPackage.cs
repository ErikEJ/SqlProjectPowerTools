global using Community.VisualStudio.Toolkit;
global using Microsoft.VisualStudio.Shell;
global using System;
global using Task = System.Threading.Tasks.Task;
using System.Runtime.InteropServices;
using System.Threading;

namespace SqlProjectsPowerTools
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration(Vsix.Name, Vsix.Description, Vsix.Version)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [Guid(PackageGuids.VsixString)]
    [ProvideAutoLoad(UIContextGuid, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideUIContextRule(
        UIContextGuid,
        name: "Auto load based on rules",
        expression: "SdkProject | SqlprojProject ",
        termNames: ["SdkProject", "SqlprojProject"],
        termValues: [$"ActiveProjectCapability:{SdkProjCapability}", "ActiveProjectBuildProperty:DSP=.*"])]

    public sealed class VsixPackage : ToolkitPackage
    {
        public const string UIContextGuid = "E098D400-A841-4C88-9B7C-267EFA15A5E4";
        public const string SdkProjCapability = "MSBuild.Sdk.SqlProj.BuildTSqlScript";

        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await this.RegisterCommandsAsync();
        }
    }
}