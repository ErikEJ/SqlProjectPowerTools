namespace DacFXToolLib.Pipelines
{
    public enum PipelineProjectStyle
    {
        Classic = 0,
        MicrosoftBuildSql = 1,
        MsBuildSdkSqlProj = 2,
    }

    public enum PipelineTargetPlatform
    {
        AzureSqlDatabase = 0,
        AzureSqlManagedInstance = 1,
        SqlServer = 2,
    }

    public enum PipelineAgentPool
    {
        MicrosoftHosted = 0,
        ManagedDevOpsPool = 1,
        SelfHosted = 2,
    }

    public enum PipelineAuthentication
    {
        WorkloadIdentityFederation = 0,
        SqlAuthentication = 1,
        WindowsAuthentication = 2,
    }

    public enum PipelineFirewallStrategy
    {
        TemporaryRule = 0,
        None = 1,
        PrivateNetwork = 2,
    }
}
