global using System;
global using Community.VisualStudio.Toolkit;
#pragma warning disable SA1210 // Using directives should be ordered alphabetically by namespace
global using Microsoft.VisualStudio.Shell;
#pragma warning disable SA1209 // Using alias directives should be placed after other using directives
global using Task = System.Threading.Tasks.Task;
#pragma warning restore SA1209 // Using alias directives should be placed after other using directives

using System.Runtime.InteropServices;
using System.Threading;
using GalaSoft.MvvmLight.Messaging;
using Microsoft.Extensions.DependencyInjection;

#pragma warning restore SA1210 // Using directives should be ordered alphabetically by namespace

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

        private IServiceProvider extensionServices;

        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await this.RegisterCommandsAsync();

            PackageManager.Package = this;

            extensionServices = CreateServiceProvider();

            typeof(Microsoft.Xaml.Behaviors.Behavior).ToString();
            typeof(DropDownButtonLib.Controls.DropDownButton).ToString();
        }

        internal TView GetView<TView>()
            where TView : IView
        {
            return extensionServices.GetService<TView>();
        }

        private IServiceProvider CreateServiceProvider()
        {
            var services = new ServiceCollection();

            // Register views
            services.AddTransient<IPickTablesDialog, PickTablesDialog>()
                .AddTransient<IPickServerDatabaseDialog, PickServerDatabaseDialog>();

            // Register view models
            services.AddTransient<IPickServerDatabaseViewModel, PickServerDatabaseViewModel>()
                    .AddTransient<IPickTablesViewModel, PickTablesViewModel>()
                    .AddSingleton<Func<ISchemaInformationViewModel>>(() => new SchemaInformationViewModel())
                    .AddSingleton<Func<ITableInformationViewModel>>(provider => () => new TableInformationViewModel(provider.GetService<IMessenger>()))
                    .AddSingleton<Func<IColumnInformationViewModel>>(provider => () => new ColumnInformationViewModel(provider.GetService<IMessenger>()))
                    .AddSingleton<Func<IColumnChildrenViewModel>>(provider => () => new ColumnChildrenViewModel(provider.GetService<IMessenger>()))
                    .AddTransient<IObjectTreeViewModel, ObjectTreeViewModel>();

            // Register BLL
            var messenger = new Messenger();
            messenger.Register<ShowMessageBoxMessage>(this, HandleShowMessageBoxMessage);

            services.AddSingleton<IMessenger>(messenger);

            //// Register DAL
            services.AddTransient<IVisualStudioAccess, VisualStudioAccess>();

            var provider = services.BuildServiceProvider();
            return provider;
        }

        private static void HandleShowMessageBoxMessage(ShowMessageBoxMessage msg)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            VSHelper.ShowMessage(msg.Content);
        }
    }
}