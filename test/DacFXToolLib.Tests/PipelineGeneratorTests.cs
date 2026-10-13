using System.IO.Compression;
using System.Text.RegularExpressions;
using DacFXToolLib.Pipelines;

namespace DacFXToolLib.Tests
{
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
                      <Metadata Name="Environment" Value="" />
                      <Metadata Name="StorageAccount" Value="devstorage" />
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
        public void ReadSqlCmdVariables_MissingFile_ReturnsEmpty()
        {
            Assert.Empty(PipelineGenerator.ReadSqlCmdVariables(Path.Combine(root, "missing.dacpac")));
        }

        [Theory]
        [InlineData("prod", true)]
        [InlineData("Production", true)]
        [InlineData("staging", true)]
        [InlineData("stg", true)]
        [InlineData("preprod", true)]
        [InlineData("dev", false)]
        [InlineData("test", false)]
        [InlineData("", false)]
        public void PlanBeforeDeploy_OnlyForStagingAndProduction(string name, bool expected)
        {
            Assert.Equal(expected, PipelineEnvironment.Create(name).PlanBeforeDeploy);
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

        [Fact]
        public void Render_ReusesExistingPublishProfile()
        {
            File.WriteAllText(Path.Combine(root, "src", "MyDatabase", "MyDatabase.Prod.publish.xml"), "<Project />");

            var files = new PipelineGenerator(CreateOptions()).Render();

            Assert.DoesNotContain(files, f => f.Path.EndsWith("MyDatabase.prod.publish.xml", StringComparison.Ordinal));
            Assert.Contains("publishProfile: 'MyDatabase.Prod.publish.xml'", GetFile(files, "azure-pipelines.yml"), StringComparison.Ordinal);
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

        private static string GetFile(IReadOnlyList<PipelineFile> files, string name)
        {
            return files.Single(f => Path.GetFileName(f.Path) == name).Content;
        }

        private PipelineOptions CreateOptions()
        {
            return new PipelineOptions
            {
                ProjectFilePath = projectFile,
                DacpacPath = dacpacPath,
                OutputRoot = root,
                DeploymentIdentityName = "my-app",
                Environments = new List<PipelineEnvironment>
                {
                    PipelineEnvironment.Create("dev"),
                    PipelineEnvironment.Create("test"),
                    PipelineEnvironment.Create("staging"),
                    PipelineEnvironment.Create("prod"),
                },
            };
        }
    }
}
