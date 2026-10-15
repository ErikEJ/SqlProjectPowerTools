using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using DacFXToolLib.Pipelines;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.CommandWpf;

namespace SqlProjectsPowerTools
{
    public class PipelineWizardViewModel : ViewModelBase
    {
        private int targetPlatformIndex;
        private int agentPoolIndex;
        private int authenticationIndex;
        private int firewallStrategyIndex;
        private string agentPoolName = string.Empty;
        private string serviceConnectionName = string.Empty;
        private string deploymentIdentityName = string.Empty;
        private string environmentsText = string.Empty;
        private string defaultBranch = "main";
        private bool includeCodeAnalysis;
        private bool includeContainerValidation;
        private bool usePublishProfiles;

        public PipelineWizardViewModel()
        {
            OkCommand = new RelayCommand(
                OkExecuted,
                () => Environments.Count > 0
                    && !IsAzureSqlWindowsAuthentication
                    && !IsMicrosoftHostedWindowsAuthentication
                    && !IsMicrosoftHostedPrivateNetwork);
            CancelCommand = new RelayCommand(CancelExecuted);
        }

        public event EventHandler<CloseRequestedEventArgs> CloseRequested;

        public ICommand OkCommand { get; }

        public ICommand CancelCommand { get; }

        // The order of these lists matches the enum values
        public IList<string> TargetPlatforms { get; } = new List<string> { "Azure SQL Database", "Azure SQL Managed Instance", "SQL Server" };

        public IList<string> AgentPools { get; } = new List<string> { "Microsoft-hosted agents", "Managed DevOps Pool", "Self-hosted agents" };

        public IList<string> Authentications { get; } = new List<string> { "Passwordless (workload identity federation)", "SQL authentication (secret variable)", "Windows authentication" };

        public IList<string> FirewallStrategies { get; } = new List<string> { "Temporary firewall rule for the agent IP", "None", "Private network (private endpoint)" };

        public int TargetPlatformIndex
        {
            get => targetPlatformIndex;
            set => SetAndRefresh(ref targetPlatformIndex, value);
        }

        public int AgentPoolIndex
        {
            get => agentPoolIndex;
            set
            {
                SetAndRefresh(ref agentPoolIndex, value);
                RaisePropertyChanged(nameof(IsAgentPoolNameEnabled));
            }
        }

        public int AuthenticationIndex
        {
            get => authenticationIndex;
            set
            {
                SetAndRefresh(ref authenticationIndex, value);
                RaisePropertyChanged(nameof(IsDeploymentIdentityEnabled));
            }
        }

        public int FirewallStrategyIndex
        {
            get => firewallStrategyIndex;
            set => SetAndRefresh(ref firewallStrategyIndex, value);
        }

        public bool IsAgentPoolNameEnabled => AgentPoolIndex != (int)PipelineAgentPool.MicrosoftHosted;

        public bool IsDeploymentIdentityEnabled => AuthenticationIndex == (int)PipelineAuthentication.WorkloadIdentityFederation;

        public string AgentPoolName
        {
            get => agentPoolName;
            set => Set(ref agentPoolName, value);
        }

        public string ServiceConnectionName
        {
            get => serviceConnectionName;
            set => Set(ref serviceConnectionName, value);
        }

        public string DeploymentIdentityName
        {
            get => deploymentIdentityName;
            set => Set(ref deploymentIdentityName, value);
        }

        public string DefaultBranch
        {
            get => defaultBranch;
            set => Set(ref defaultBranch, value);
        }

        public string EnvironmentsText
        {
            get => environmentsText;
            set
            {
                if (Set(ref environmentsText, value))
                {
                    RaisePropertyChanged(nameof(PlanSummary));
                }
            }
        }

        public bool IncludeCodeAnalysis
        {
            get => includeCodeAnalysis;
            set => Set(ref includeCodeAnalysis, value);
        }

        public bool IncludeContainerValidation
        {
            get => includeContainerValidation;
            set => Set(ref includeContainerValidation, value);
        }

        public bool UsePublishProfiles
        {
            get => usePublishProfiles;
            set => Set(ref usePublishProfiles, value);
        }

        public IList<string> Environments => (EnvironmentsText ?? string.Empty)
            .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(e => e.Trim())
            .Where(e => e.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        public string PlanSummary
        {
            get
            {
                var environments = Environments;
                if (environments.Count == 0)
                {
                    return "Enter at least one environment.";
                }

                var planned = environments.Where(PipelineEnvironment.IsStagingOrProduction).ToList();

                return planned.Count == 0
                    ? "No staging or production environment: all environments deploy directly without a plan stage."
                    : $"Plan stage (script + deploy report) and approval before deploying to: {string.Join(", ", planned)}. Other environments deploy directly.";
            }
        }

        public string Warning
        {
            get
            {
                var warnings = new List<string>();

                if (IsAzureSqlWindowsAuthentication)
                {
                    warnings.Add("Windows authentication is not supported for Azure SQL Database. Choose SQL authentication or passwordless (workload identity federation).");
                }

                if (AgentPoolIndex == (int)PipelineAgentPool.MicrosoftHosted
                    && AuthenticationIndex == (int)PipelineAuthentication.WindowsAuthentication)
                {
                    warnings.Add("Windows authentication requires a self-hosted agent running as a domain account.");
                }

                if (AgentPoolIndex == (int)PipelineAgentPool.MicrosoftHosted
                    && FirewallStrategyIndex == (int)PipelineFirewallStrategy.PrivateNetwork)
                {
                    warnings.Add("Microsoft-hosted agents cannot reach a private endpoint - use a Managed DevOps Pool or self-hosted agents.");
                }

                if (TargetPlatformIndex != (int)PipelineTargetPlatform.AzureSqlDatabase
                    && FirewallStrategyIndex == (int)PipelineFirewallStrategy.TemporaryRule)
                {
                    warnings.Add("Temporary firewall rules are only generated for Azure SQL Database.");
                }

                if (TargetPlatformIndex == (int)PipelineTargetPlatform.SqlServer
                    && AuthenticationIndex == (int)PipelineAuthentication.WorkloadIdentityFederation)
                {
                    warnings.Add("Passwordless access with workload identity federation requires Microsoft Entra authentication on the SQL Server.");
                }

                return string.Join(Environment.NewLine, warnings);
            }
        }

        public bool HasWarning => !string.IsNullOrEmpty(Warning);

        private bool IsAzureSqlWindowsAuthentication =>
            TargetPlatformIndex == (int)PipelineTargetPlatform.AzureSqlDatabase
            && AuthenticationIndex == (int)PipelineAuthentication.WindowsAuthentication;

        private bool IsMicrosoftHostedWindowsAuthentication =>
            AgentPoolIndex == (int)PipelineAgentPool.MicrosoftHosted
            && AuthenticationIndex == (int)PipelineAuthentication.WindowsAuthentication;

        private bool IsMicrosoftHostedPrivateNetwork =>
            AgentPoolIndex == (int)PipelineAgentPool.MicrosoftHosted
            && FirewallStrategyIndex == (int)PipelineFirewallStrategy.PrivateNetwork;

        public void Load(PipelineOptions options)
        {
            if (options == null)
            {
                return;
            }

            TargetPlatformIndex = (int)options.TargetPlatform;
            AgentPoolIndex = (int)options.AgentPool;
            AuthenticationIndex = (int)options.Authentication;
            FirewallStrategyIndex = (int)options.FirewallStrategy;
            AgentPoolName = options.AgentPoolName ?? string.Empty;
            ServiceConnectionName = options.ServiceConnectionName ?? string.Empty;
            DeploymentIdentityName = options.DeploymentIdentityName ?? string.Empty;
            DefaultBranch = string.IsNullOrWhiteSpace(options.DefaultBranch) ? "main" : options.DefaultBranch;
            IncludeCodeAnalysis = options.IncludeCodeAnalysis;
            IncludeContainerValidation = options.IncludeContainerValidation;
            UsePublishProfiles = options.UsePublishProfiles;
            EnvironmentsText = options.Environments == null || options.Environments.Count == 0
                ? "dev, test, prod"
                : string.Join(", ", options.Environments.Select(e => e.Name));
        }

        public void ApplyTo(PipelineOptions options)
        {
            if (options == null)
            {
                return;
            }

            options.TargetPlatform = (PipelineTargetPlatform)TargetPlatformIndex;
            options.AgentPool = (PipelineAgentPool)AgentPoolIndex;
            options.Authentication = (PipelineAuthentication)AuthenticationIndex;
            options.FirewallStrategy = (PipelineFirewallStrategy)FirewallStrategyIndex;
            options.AgentPoolName = AgentPoolName?.Trim();
            options.ServiceConnectionName = ServiceConnectionName?.Trim();
            options.DeploymentIdentityName = DeploymentIdentityName?.Trim();
            options.DefaultBranch = string.IsNullOrWhiteSpace(DefaultBranch) ? "main" : DefaultBranch.Trim();
            options.IncludeCodeAnalysis = IncludeCodeAnalysis;
            options.IncludeContainerValidation = IncludeContainerValidation;
            options.UsePublishProfiles = UsePublishProfiles;

            // Plan stages are only used for staging and production type environments
            options.Environments = Environments.Select(PipelineEnvironment.Create).ToList();
        }

        private void SetAndRefresh(ref int field, int value, [CallerMemberName] string propertyName = null)
        {
            if (Set(propertyName, ref field, value))
            {
                RaisePropertyChanged(nameof(Warning));
                RaisePropertyChanged(nameof(HasWarning));
                ((RelayCommand)OkCommand).RaiseCanExecuteChanged();
            }
        }

        private void OkExecuted()
        {
            CloseRequested?.Invoke(this, new CloseRequestedEventArgs(true));
        }

        private void CancelExecuted()
        {
            CloseRequested?.Invoke(this, new CloseRequestedEventArgs(false));
        }
    }
}
