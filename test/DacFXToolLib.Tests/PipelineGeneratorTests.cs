using System.IO.Compression;
using System.Text.RegularExpressions;
using DacFXToolLib.Pipelines;

namespace DacFXToolLib.Tests;

public sealed class PipelineGeneratorTests : IDisposable
{
    private readonly string root;
    private readonly string projectFile;
    private readonly string dacpacPath;

    public PipelineGeneratorTests()
    {
        root = Path.Combine(Path.GetTempPath(), "PipelineGeneratorTests_" + Guid.NewGuid().ToString("N"));
        var projectFolder = Path.Combine(root, "src", "MyDatabase");
        Directory.CreateDirectory(projectFolder);
        projectFile = Path.Combine(projectFolder, "MyDatabase.sqlproj");
        File.WriteAllText(projectFile, "<Project Sdk=\"Microsoft.Build.Sql/1.0.0\" />");

        dacpacPath = Path.Combine(root, "MyDatabase.dacpac");
        using var archive = ZipFile.Open(dacpacPath, ZipArchiveMode.Create);
        var entry = archive.CreateEntry("model.xml");
        using var writer = new StreamWriter(entry.Open());
        writer.Write("""
            <?xml version="1.0" encoding="utf-8"?>
            <DataSchemaModel FileFormatVersion="1.2" xmlns="http://schemas.microsoft.com/sqlserver/dac/Serialization/2012/02">
              <Header>
                <CustomData Category="AnsiNulls"><Metadata Name="AnsiNulls" Value="True" /></CustomData>
                <CustomData Category="SqlCmdVariables" Type="SqlCmdVariable">
                  <Metadata Name="Name" Value="Environment" />
                  <Metadata Name="Value" Value="" />
                </CustomData>
                <CustomData Category="SqlCmdVariables" Type="SqlCmdVariable">
                  <Metadata Name="Name" Value="StorageAccount" />
                  <Metadata Name="Value" Value="devstorage" />
                </CustomData>
              </Header>
              <Model>
                <Element Type="SqlTable" Name="[dbo].[T]" />
              </Model>
            </DataSchemaModel>
            """);
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void ReadSqlCmdVariables_ReadsNamesAndDefaults()
    {
        var variables = PipelineGenerator.ReadSqlCmdVariables(dacpacPath);

        Assert.Equal(2, variables.Count);
        Assert.Equal("Environment", variables[0].Key);
        Assert.Equal(string.Empty, variables[0].Value);
        Assert.Equal("StorageAccount", variables[1].Key);
        Assert.Equal("devstorage", variables[1].Value);
    }

    [Fact]
    public void ReadSqlCmdVariables_MissingFile_ReturnsEmpty() => Assert.Empty(PipelineGenerator.ReadSqlCmdVariables(Path.Combine(root, "missing.dacpac")));

    [Theory]
    [InlineData("prod", true)]
    [InlineData("Production", true)]
    [InlineData("staging", true)]
    [InlineData("stg", true)]
    [InlineData("preprod", true)]
    [InlineData("dev", false)]
    [InlineData("test", false)]
    [InlineData("", false)]
    public void PlanBeforeDeploy_OnlyForStagingAndProduction(string name, bool expected) => Assert.Equal(expected, PipelineEnvironment.Create(name).PlanBeforeDeploy);

    [Fact]
    public void Render_PublishProfilesDisabledByDefault()
    {
        Assert.False(new PipelineOptions().UsePublishProfiles);

        var options = CreateOptions();
        options.UsePublishProfiles = false;

        var files = new PipelineGenerator(options).Render();

        Assert.Equal(5, files.Count);
        Assert.DoesNotContain(files, f => f.Path.EndsWith(".publish.xml", StringComparison.Ordinal));

        var pipeline = GetFile(files, "azure-pipelines.yml");
        Assert.Contains("publishProfile: ''", pipeline, StringComparison.Ordinal);
        Assert.DoesNotContain("MyDatabase.prod.publish.xml", pipeline, StringComparison.Ordinal);
        Assert.DoesNotContain("*.publish.xml", pipeline, StringComparison.Ordinal);

        var steps = GetFile(files, "sqlpackage-steps.yml");
        Assert.Contains("if ($env:PUBLISH_PROFILE) {", steps, StringComparison.Ordinal);
        Assert.Contains("$arguments += \"/Profile:$publishProfile\"", steps, StringComparison.Ordinal);

        var setup = GetFile(files, "PIPELINE-SETUP.md");
        Assert.DoesNotContain(".publish.xml", setup, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_PublishProfilesEnabled_ReferencesProfiles()
    {
        var files = new PipelineGenerator(CreateOptions()).Render();
        var pipeline = GetFile(files, "azure-pipelines.yml");

        Assert.Contains(files, f => f.Path.EndsWith(".publish.xml", StringComparison.Ordinal));
        Assert.Contains("publishProfile: 'MyDatabase.prod.publish.xml'", pipeline, StringComparison.Ordinal);
        Assert.Contains("*.publish.xml", pipeline, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_ValidationUsesSqlServer2025Container()
    {
        var options = CreateOptions();
        options.IncludeContainerValidation = true;

        var pipeline = GetFile(new PipelineGenerator(options).Render(), "azure-pipelines.yml");

        Assert.Contains("mcr.microsoft.com/mssql/server:2025-latest", pipeline, StringComparison.Ordinal);
        Assert.DoesNotContain("2022-latest", pipeline, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_ProducesAllFilesWithoutPlaceholders()
    {
        var files = new PipelineGenerator(CreateOptions()).Render();

        Assert.Equal(9, files.Count);
        Assert.Contains(files, f => f.Path == Path.Combine(root, "azure-pipelines.yml"));
        Assert.Contains(files, f => f.Path.EndsWith(Path.Combine("pipelines", "templates", "deploy-database.yml"), StringComparison.Ordinal));
        Assert.Contains(files, f => f.Path.EndsWith(Path.Combine("pipelines", "templates", "sqlpackage-steps.yml"), StringComparison.Ordinal));
        Assert.Contains(files, f => f.Path.EndsWith(Path.Combine("pipelines", "setup", "grant-pipeline-access.sql"), StringComparison.Ordinal));
        Assert.Contains(files, f => f.Path.EndsWith(Path.Combine("pipelines", "PIPELINE-SETUP.md"), StringComparison.Ordinal));
        Assert.Contains(files, f => f.Path == Path.Combine(root, "src", "MyDatabase", "MyDatabase.prod.publish.xml") && !f.CanOverwrite);

        foreach (var file in files)
        {
            Assert.DoesNotMatch(new Regex(@"(?<!\$)\{\{[A-Za-z]+\}\}"), file.Content);
        }
    }

    [Fact]
    public void Render_PassesNamesToPowerShellThroughEnvironmentVariables()
    {
        var options = CreateOptions();
        options.DacpacName = "Sales Database's";

        var steps = GetFile(new PipelineGenerator(options).Render(), "sqlpackage-steps.yml");

        Assert.Contains("$dacpac = Join-Path $dacpacFolder ($env:DACPAC_NAME + '.dacpac')", steps, StringComparison.Ordinal);
        Assert.Contains("$publishProfile = Join-Path $dacpacFolder $env:PUBLISH_PROFILE", steps, StringComparison.Ordinal);
        Assert.Contains("DACPAC_NAME: ${{ parameters.dacpacName }}", steps, StringComparison.Ordinal);
        Assert.Contains("PUBLISH_PROFILE: ${{ parameters.publishProfile }}", steps, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_PlanStageOnlyForStagingAndProduction()
    {
        var pipeline = GetFile(new PipelineGenerator(CreateOptions()).Render(), "azure-pipelines.yml");

        Assert.Contains("stageName: dev\n", pipeline, StringComparison.Ordinal);
        Assert.Matches(new Regex(@"stageName: dev\n(.*\n){4}.*planBeforeDeploy: false"), pipeline);
        Assert.Matches(new Regex(@"stageName: test\n(.*\n){4}.*planBeforeDeploy: false"), pipeline);
        Assert.Matches(new Regex(@"stageName: staging\n(.*\n){4}.*planBeforeDeploy: true"), pipeline);
        Assert.Matches(new Regex(@"stageName: prod\n(.*\n){4}.*planBeforeDeploy: true"), pipeline);
        Assert.Contains("- Deploy_staging", pipeline, StringComparison.Ordinal);
        Assert.Contains("projectFile: 'src/MyDatabase/MyDatabase.sqlproj'", pipeline, StringComparison.Ordinal);
        Assert.Contains("dotnet build", pipeline, StringComparison.Ordinal);
        Assert.Contains("-p:RunSqlCodeAnalysis=True", pipeline, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_DoesNotIncludeSqlCmdDefaults()
    {
        var files = new PipelineGenerator(CreateOptions()).Render();
        var generatedContent = string.Join("\n", files.Select(file => file.Content));

        Assert.Contains("`StorageAccount` - SQLCMD variable", generatedContent, StringComparison.Ordinal);
        Assert.DoesNotContain("devstorage", generatedContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_PrefixesDigitLeadingStageName()
    {
        var options = CreateOptions();
        options.Environments = new List<PipelineEnvironment>
        {
            PipelineEnvironment.Create("1prod"),
        };

        var pipeline = GetFile(new PipelineGenerator(options).Render(), "azure-pipelines.yml");

        Assert.Contains("stageName: _1prod\n", pipeline, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_QuotesIndicatorLeadingDefaultBranch()
    {
        var options = CreateOptions();
        options.DefaultBranch = "#release";

        var pipeline = GetFile(new PipelineGenerator(options).Render(), "azure-pipelines.yml");

        Assert.Equal(2, pipeline.Split("      - '#release'\n").Length - 1);
    }

    [Fact]
    public void Render_AlwaysInstallsLatestSqlPackage()
    {
        var steps = GetFile(new PipelineGenerator(CreateOptions()).Render(), "sqlpackage-steps.yml");

        Assert.Contains("dotnet tool install microsoft.sqlpackage --tool-path $toolPath\n", steps, StringComparison.Ordinal);
        Assert.DoesNotContain("--version", steps, StringComparison.Ordinal);
        Assert.Contains("dotnet tool list --tool-path $toolPath", steps, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_WorkloadIdentityAndTemporaryFirewallRule()
    {
        var files = new PipelineGenerator(CreateOptions()).Render();
        var steps = GetFile(files, "sqlpackage-steps.yml");

        Assert.Contains("AzureCLI@2", steps, StringComparison.Ordinal);
        Assert.Contains("az account get-access-token --resource https://database.windows.net/", steps, StringComparison.Ordinal);
        Assert.Contains("/AccessToken:$token", steps, StringComparison.Ordinal);
        Assert.Contains("az sql server firewall-rule create", steps, StringComparison.Ordinal);
        Assert.Contains("az sql server firewall-rule delete", steps, StringComparison.Ordinal);
        Assert.Contains("condition: always()", steps, StringComparison.Ordinal);
        Assert.Contains("SQLCMD_Environment: $(Environment)", steps, StringComparison.Ordinal);
        Assert.Contains("/Variables:StorageAccount=${env:SQLCMD_StorageAccount}", steps, StringComparison.Ordinal);

        var grant = GetFile(files, "grant-pipeline-access.sql");
        Assert.Contains("CREATE USER [my-app] FROM EXTERNAL PROVIDER;", grant, StringComparison.Ordinal);
        Assert.Contains("-- CREATE USER [my-app] WITH SID = 0x..., TYPE = E;", grant, StringComparison.Ordinal);
        Assert.Contains("SELECT CONVERT(varbinary(16), CAST('<application-client-id>' AS uniqueidentifier));", grant, StringComparison.Ordinal);
        Assert.DoesNotContain("db_owner TO", grant, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_ManagedInstanceWithSqlAuthentication_HasNoFirewallSteps()
    {
        var options = CreateOptions();
        options.TargetPlatform = PipelineTargetPlatform.AzureSqlManagedInstance;
        options.Authentication = PipelineAuthentication.SqlAuthentication;

        var files = new PipelineGenerator(options).Render();
        var steps = GetFile(files, "sqlpackage-steps.yml");

        Assert.DoesNotContain("firewall-rule", steps, StringComparison.Ordinal);
        Assert.DoesNotContain("AzureCLI@2", steps, StringComparison.Ordinal);
        Assert.Contains("SQL_PASSWORD: $(SqlPassword)", steps, StringComparison.Ordinal);
        Assert.Contains("serviceConnection: ''", GetFile(files, "azure-pipelines.yml"), StringComparison.Ordinal);
    }

    [Fact]
    public void Render_ClassicProjectBuildsOnWindowsAndValidationStage()
    {
        var options = CreateOptions();
        options.ProjectStyle = PipelineProjectStyle.Classic;
        options.IncludeContainerValidation = true;

        var pipeline = GetFile(new PipelineGenerator(options).Render(), "azure-pipelines.yml");

        Assert.Contains("VSBuild@1", pipeline, StringComparison.Ordinal);
        Assert.Contains("vmImage: windows-latest", pipeline, StringComparison.Ordinal);
        Assert.Contains("- stage: Validate", pipeline, StringComparison.Ordinal);
        Assert.Contains("/Variables:Environment=validation", pipeline, StringComparison.Ordinal);
        Assert.DoesNotContain("/Variables:StorageAccount=validation", pipeline, StringComparison.Ordinal);
        Assert.Contains("      - Validate\n", pipeline, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(PipelineProjectStyle.Classic, true)]
    [InlineData(PipelineProjectStyle.Classic, false)]
    [InlineData(PipelineProjectStyle.MicrosoftBuildSql, true)]
    [InlineData(PipelineProjectStyle.MicrosoftBuildSql, false)]
    [InlineData(PipelineProjectStyle.MsBuildSdkSqlProj, true)]
    [InlineData(PipelineProjectStyle.MsBuildSdkSqlProj, false)]
    public void Render_ExplicitlySetsCodeAnalysis(PipelineProjectStyle projectStyle, bool includeCodeAnalysis)
    {
        var options = CreateOptions();
        options.ProjectStyle = projectStyle;
        options.IncludeCodeAnalysis = includeCodeAnalysis;

        var pipeline = GetFile(new PipelineGenerator(options).Render(), "azure-pipelines.yml");

        Assert.Contains($"RunSqlCodeAnalysis={includeCodeAnalysis}", pipeline, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_ReusesExistingPublishProfile()
    {
        File.WriteAllText(Path.Combine(root, "src", "MyDatabase", "MyDatabase.Prod.publish.xml"), "<Project />");

        var files = new PipelineGenerator(CreateOptions()).Render();

        Assert.DoesNotContain(files, f => f.Path.EndsWith("MyDatabase.prod.publish.xml", StringComparison.Ordinal));
        Assert.Contains("publishProfile: 'MyDatabase.Prod.publish.xml'", GetFile(files, "azure-pipelines.yml"), StringComparison.Ordinal);
    }

    [Fact]
    public void Render_ReusesProfileUsingOriginalEnvironmentName()
    {
        var options = CreateOptions();
        options.Environments = new List<PipelineEnvironment>
        {
            PipelineEnvironment.Create("prod-east"),
        };
        File.WriteAllText(Path.Combine(root, "src", "MyDatabase", "MyDatabase.prod-east.publish.xml"), "<Project />");

        var files = new PipelineGenerator(options).Render();
        var pipeline = GetFile(files, "azure-pipelines.yml");

        Assert.DoesNotContain(files, f => f.Path.EndsWith("MyDatabase.prod_east.publish.xml", StringComparison.Ordinal));
        Assert.Contains("publishProfile: 'MyDatabase.prod-east.publish.xml'", pipeline, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_ClaimsExistingProfileForMostSpecificEnvironment()
    {
        var options = CreateOptions();
        options.Environments = new List<PipelineEnvironment>
        {
            PipelineEnvironment.Create("prod"),
            PipelineEnvironment.Create("west_prod"),
        };
        File.WriteAllText(Path.Combine(root, "src", "MyDatabase", "MyDatabase.west_prod.publish.xml"), "<Project />");

        var files = new PipelineGenerator(options).Render();
        var pipeline = GetFile(files, "azure-pipelines.yml");

        Assert.Contains(files, f => f.Path == Path.Combine(root, "src", "MyDatabase", "MyDatabase.prod.publish.xml") && !f.CanOverwrite);
        Assert.Contains("publishProfile: 'MyDatabase.west_prod.publish.xml'", pipeline, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_ShellQuotesSetupCommandArguments()
    {
        var options = CreateOptions();
        options.DacpacName = "Sales Database's";
        options.DefaultBranch = "#release";

        var setup = GetFile(new PipelineGenerator(options).Render(), "PIPELINE-SETUP.md");

        Assert.Contains("'DatabaseName=Sales Database'\"'\"'s'", setup, StringComparison.Ordinal);
        Assert.Contains("--branch '#release'", setup, StringComparison.Ordinal);
        Assert.Contains("--name 'sql-dev'", setup, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_DoesNotOverwriteUnlessRequested()
    {
        var options = CreateOptions();
        var pipelinePath = Path.Combine(root, "azure-pipelines.yml");
        var profilePath = Path.Combine(root, "src", "MyDatabase", "MyDatabase.dev.publish.xml");

        var written = new PipelineGenerator(options).Generate();
        Assert.Equal(9, written.Count);

        File.WriteAllText(pipelinePath, "custom");
        File.WriteAllText(profilePath, "custom profile");

        Assert.Empty(new PipelineGenerator(options).Generate());
        Assert.Equal("custom", File.ReadAllText(pipelinePath));

        options.Overwrite = true;
        new PipelineGenerator(options).Generate();
        Assert.NotEqual("custom", File.ReadAllText(pipelinePath));
        Assert.Equal("custom profile", File.ReadAllText(profilePath));
    }

    [Fact]
    public void Options_RoundTrip()
    {
        var options = CreateOptions();
        options.AgentPool = PipelineAgentPool.SelfHosted;

        var read = PipelineOptionsExtensions.TryReadJson(options.Write());

        Assert.NotNull(read);
        Assert.Equal(PipelineAgentPool.SelfHosted, read.AgentPool);
        Assert.Equal(4, read.Environments.Count);
        Assert.True(read.Environments[3].PlanBeforeDeploy);
        Assert.Null(read.ProjectFilePath);
    }

    [Fact]
    public void Options_TryRead_ThrowsForMalformedExistingFile()
    {
        var optionsPath = Path.Combine(root, PipelineOptions.OptionsFileName);
        File.WriteAllText(optionsPath, "{");

        Assert.Throws<InvalidDataException>(() => PipelineOptionsExtensions.TryRead(optionsPath));
    }

    private static string GetFile(IReadOnlyList<PipelineFile> files, string name) => files.Single(f => Path.GetFileName(f.Path) == name).Content;

    private PipelineOptions CreateOptions() => new()
    {
        ProjectFilePath = projectFile,
        DacpacPath = dacpacPath,
        OutputRoot = root,
        DeploymentIdentityName = "my-app",
        UsePublishProfiles = true,
        Environments = new List<PipelineEnvironment>
            {
                PipelineEnvironment.Create("dev"),
                PipelineEnvironment.Create("test"),
                PipelineEnvironment.Create("staging"),
                PipelineEnvironment.Create("prod"),
            },
    };
}
