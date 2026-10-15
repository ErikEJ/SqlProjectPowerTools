using System.Collections.Generic;
using System.Runtime.Serialization;

namespace DacFXToolLib.Pipelines
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "DTO")]
    public class PipelineOptions
    {
        public const string OptionsFileName = "pipeline-options.json";

        public const string PipelineFileName = "azure-pipelines.yml";

        public const string DeployTemplateFileName = "pipelines/templates/deploy-database.yml";

        public const string SqlPackageTemplateFileName = "pipelines/templates/sqlpackage-steps.yml";

        public const string GrantAccessScriptFileName = "pipelines/setup/grant-pipeline-access.sql";

        public const string SetupGuideFileName = "pipelines/PIPELINE-SETUP.md";

        public static IReadOnlyList<string> GeneratedFileNames { get; } = new[]
        {
            PipelineFileName,
            DeployTemplateFileName,
            SqlPackageTemplateFileName,
            GrantAccessScriptFileName,
            SetupGuideFileName,
        };

        /// <summary>
        /// Gets or sets the full path to the .sqlproj file (not persisted).
        /// </summary>
        [IgnoreDataMember]
        public string ProjectFilePath { get; set; }

        /// <summary>
        /// Gets or sets the full path to the built .dacpac file, used to read SQLCMD variables (not persisted).
        /// </summary>
        [IgnoreDataMember]
        public string DacpacPath { get; set; }

        /// <summary>
        /// Gets or sets the root folder where the pipeline files are written, normally the git repository root (not persisted).
        /// </summary>
        [IgnoreDataMember]
        public string OutputRoot { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether existing pipeline files are overwritten (not persisted).
        /// </summary>
        [IgnoreDataMember]
        public bool Overwrite { get; set; }

        public string DacpacName { get; set; }

        public PipelineProjectStyle ProjectStyle { get; set; } = PipelineProjectStyle.MicrosoftBuildSql;

        public string SqlServerVersion { get; set; }

        public PipelineTargetPlatform TargetPlatform { get; set; } = PipelineTargetPlatform.AzureSqlDatabase;

        public PipelineAgentPool AgentPool { get; set; } = PipelineAgentPool.MicrosoftHosted;

        public string AgentPoolName { get; set; }

        public PipelineAuthentication Authentication { get; set; } = PipelineAuthentication.WorkloadIdentityFederation;

        public PipelineFirewallStrategy FirewallStrategy { get; set; } = PipelineFirewallStrategy.TemporaryRule;

        public string ServiceConnectionName { get; set; } = "azure-sql-deployment";

        public string DeploymentIdentityName { get; set; } = "<deployment-identity-name>";

        public List<PipelineEnvironment> Environments { get; set; } = new List<PipelineEnvironment>();

        public bool IncludeCodeAnalysis { get; set; } = true;

        public bool IncludeContainerValidation { get; set; }

        public bool UsePublishProfiles { get; set; }

        public string DefaultBranch { get; set; } = "main";
    }
}
