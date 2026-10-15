using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace DacFXToolLib.Pipelines
{
    /// <summary>
    /// Generates a starter Azure DevOps pipeline for a SQL database project from embedded templates.
    /// </summary>
    public sealed partial class PipelineGenerator
    {
        public const string DocsUrl = "https://github.com/ErikEJ/SqlProjectPowerTools/blob/main/docs/azure-devops-pipelines.md";

        private const string PublishProfileSuffix = ".publish.xml";

        private readonly PipelineOptions options;

        public PipelineGenerator(PipelineOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            if (string.IsNullOrEmpty(options.ProjectFilePath))
            {
                throw new ArgumentException("ProjectFilePath is required", nameof(options));
            }

            this.options = options;
        }

        private string ProjectName => Path.GetFileNameWithoutExtension(options.ProjectFilePath);

        private string ProjectDirectory => Path.GetDirectoryName(Path.GetFullPath(options.ProjectFilePath))!;

        private string OutputRoot => string.IsNullOrEmpty(options.OutputRoot) ? ProjectDirectory : Path.GetFullPath(options.OutputRoot);

        private string DacpacName => string.IsNullOrEmpty(options.DacpacName) ? ProjectName : options.DacpacName;

        private bool UsesTemporaryFirewallRule =>
            options.TargetPlatform == PipelineTargetPlatform.AzureSqlDatabase
            && options.FirewallStrategy == PipelineFirewallStrategy.TemporaryRule;

        private bool UsesServiceConnection =>
            options.Authentication == PipelineAuthentication.WorkloadIdentityFederation
            || UsesTemporaryFirewallRule;

        /// <summary>
        /// Reads the SQLCMD variable names and default values from a .dacpac file.
        /// </summary>
        /// <param name="dacpacPath">Path to the .dacpac file.</param>
        /// <returns>The SQLCMD variables, empty if the file does not exist.</returns>
        public static IReadOnlyList<KeyValuePair<string, string>> ReadSqlCmdVariables(string? dacpacPath)
        {
            var result = new List<KeyValuePair<string, string>>();

            if (string.IsNullOrEmpty(dacpacPath) || !File.Exists(dacpacPath))
            {
                return result;
            }

            using var archive = ZipFile.OpenRead(dacpacPath);
            var entry = archive.GetEntry("model.xml");
            if (entry == null)
            {
                return result;
            }

            using var stream = entry.Open();
            using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });

            var inSqlCmdVariable = false;
            var customDataDepth = -1;
            string? variableName = null;
            var defaultValue = string.Empty;

            while (reader.Read())
            {
                if (reader.NodeType != XmlNodeType.Element && reader.NodeType != XmlNodeType.EndElement)
                {
                    continue;
                }

                // The header with SQLCMD variables precedes the (potentially large) model
                if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "Model")
                {
                    break;
                }

                if (reader.NodeType == XmlNodeType.Element
                    && reader.LocalName == "CustomData"
                    && reader.GetAttribute("Category") == "SqlCmdVariables")
                {
                    inSqlCmdVariable = !reader.IsEmptyElement;
                    customDataDepth = reader.Depth;
                    variableName = null;
                    defaultValue = string.Empty;
                    continue;
                }

                if (inSqlCmdVariable && reader.NodeType == XmlNodeType.EndElement && reader.Depth == customDataDepth)
                {
                    if (!string.IsNullOrEmpty(variableName))
                    {
                        result.Add(new KeyValuePair<string, string>(variableName, defaultValue));
                    }

                    inSqlCmdVariable = false;
                    continue;
                }

                if (inSqlCmdVariable && reader.NodeType == XmlNodeType.Element && reader.LocalName == "Metadata")
                {
                    var metadataName = reader.GetAttribute("Name");
                    if (metadataName == "Name")
                    {
                        variableName = reader.GetAttribute("Value");
                    }
                    else if (metadataName == "Value")
                    {
                        defaultValue = reader.GetAttribute("Value") ?? string.Empty;
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Writes the pipeline files to disk.
        /// </summary>
        /// <returns>The full paths of the files that were written.</returns>
        public IReadOnlyList<string> Generate()
        {
            var written = new List<string>();

            foreach (var file in Render())
            {
                if (File.Exists(file.Path) && (!file.CanOverwrite || !options.Overwrite))
                {
                    continue;
                }

                var directory = Path.GetDirectoryName(file.Path);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(file.Path, file.Content, new UTF8Encoding(false));
                written.Add(file.Path);
            }

            return written;
        }

        /// <summary>
        /// Renders the pipeline files without writing them.
        /// </summary>
        /// <returns>The files to generate.</returns>
        public IReadOnlyList<PipelineFile> Render()
        {
            var sqlCmdVariables = ReadSqlCmdVariables(options.DacpacPath)
                .Where(v => SafeVariableNameRegex().IsMatch(v.Key))
                .ToList();

            var environments = GetEnvironments();

            var files = new List<PipelineFile>
            {
                new(Combine(PipelineOptions.PipelineFileName), RenderPipeline(environments, sqlCmdVariables), true),
                new(Combine(PipelineOptions.DeployTemplateFileName), Fill(LoadTemplate("deploy-database.yml"), new Dictionary<string, string>()), true),
                new(Combine(PipelineOptions.SqlPackageTemplateFileName), RenderSqlPackageSteps(sqlCmdVariables), true),
                new(Combine(PipelineOptions.GrantAccessScriptFileName), RenderGrantScript(), true),
                new(Combine(PipelineOptions.SetupGuideFileName), RenderSetupGuide(environments, sqlCmdVariables), true),
            };

            foreach (var environment in environments.Where(e => e.CreateProfile))
            {
                if (!options.UsePublishProfiles)
                {
                    continue;
                }

                files.Add(new PipelineFile(
                    Path.Combine(ProjectDirectory, environment.PublishProfile),
                    Fill(LoadTemplate("publish-profile.xml"), new Dictionary<string, string>
                    {
                        ["EnvironmentName"] = XmlEscape(environment.Name),
                        ["DatabaseName"] = XmlEscape(DacpacName),
                    }),
                    false));
            }

            return files;
        }

        private static string LoadTemplate(string name)
        {
            var resourceName = "DacFXToolLib.Pipelines.Templates." + name;
            using var stream = typeof(PipelineGenerator).Assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException($"Template '{resourceName}' not found");
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd().Replace("\r\n", "\n", StringComparison.Ordinal);
        }

        private static string Fill(string template, IDictionary<string, string> values)
        {
            var result = template.Replace("{{DocsUrl}}", DocsUrl, StringComparison.Ordinal);

            foreach (var value in values)
            {
                result = result.Replace("{{" + value.Key + "}}", value.Value, StringComparison.Ordinal);
            }

            return result;
        }

        private static string Indent(int spaces, params string[] lines)
        {
            var prefix = new string(' ', spaces);
            return string.Join("\n", lines.Select(l => string.IsNullOrEmpty(l) ? string.Empty : prefix + l));
        }

        private static string XmlEscape(string value)
        {
            return System.Security.SecurityElement.Escape(value) ?? string.Empty;
        }

        private static string YamlQuote(string value)
        {
            return "'" + value.Replace("'", "''", StringComparison.Ordinal) + "'";
        }

        private static string ToStageName(string name)
        {
            var stageName = UnsafeStageCharactersRegex().Replace(name, "_");
            return string.IsNullOrEmpty(stageName) ? "env" : char.IsLetter(stageName[0]) || stageName[0] == '_' ? stageName : "_" + stageName;
        }

        [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]*$")]
        private static partial Regex SafeVariableNameRegex();

        [GeneratedRegex("[^A-Za-z0-9_]")]
        private static partial Regex UnsafeStageCharactersRegex();

        private string Combine(string relativePath)
        {
            return Path.Combine(OutputRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        }

        private List<EnvironmentInfo> GetEnvironments()
        {
            var configured = (options.Environments ?? new List<PipelineEnvironment>())
                .Where(e => e != null && !string.IsNullOrWhiteSpace(e.Name))
                .ToList();

            if (configured.Count == 0)
            {
                configured = new List<PipelineEnvironment>
                {
                    PipelineEnvironment.Create("dev"),
                    PipelineEnvironment.Create("prod"),
                };
            }

            var existingProfiles = Directory.Exists(ProjectDirectory)
                ? Directory.GetFiles(ProjectDirectory, "*" + PublishProfileSuffix).Select(Path.GetFileName).OfType<string>().OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList()
                : new List<string>();

            var normalizedEnvironments = new List<(string Name, string StageName, bool PlanBeforeDeploy)>();
            var usedStageNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var environment in configured)
            {
                var name = environment.Name.Trim();
                var stageName = ToStageName(name);

                if (!usedStageNames.Add(stageName))
                {
                    throw new InvalidOperationException($"Environment '{name}' maps to the duplicate stage name '{stageName}'. Rename it before generating the pipeline.");
                }

                normalizedEnvironments.Add((name, stageName, environment.PlanBeforeDeploy));
            }

            var availableProfiles = new HashSet<string>(existingProfiles, StringComparer.OrdinalIgnoreCase);
            var profileByStageName = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            foreach (var environment in normalizedEnvironments
                .OrderByDescending(e => e.Name.Length)
                .ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase))
            {
                var existingProfile = FindExistingProfile(availableProfiles, environment.Name);
                if (existingProfile != null)
                {
                    availableProfiles.Remove(existingProfile);
                }

                profileByStageName[environment.StageName] = existingProfile;
            }

            var result = new List<EnvironmentInfo>();
            foreach (var environment in normalizedEnvironments)
            {
                var existingProfile = profileByStageName[environment.StageName];
                result.Add(new EnvironmentInfo(
                    environment.Name,
                    environment.StageName,
                    "sql-" + environment.StageName.ToLowerInvariant(),
                    existingProfile ?? $"{ProjectName}.{environment.StageName}{PublishProfileSuffix}",
                    existingProfile == null,
                    environment.PlanBeforeDeploy));
            }

            return result;
        }

        private string? FindExistingProfile(IEnumerable<string> profileFileNames, string environmentName)
        {
            var projectProfileName = $"{ProjectName}.{environmentName}";
            var profiles = profileFileNames.OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList();

            return profiles.FirstOrDefault(p => GetProfileName(p).Equals(projectProfileName, StringComparison.OrdinalIgnoreCase))
                ?? profiles.FirstOrDefault(p => GetProfileName(p).Equals(environmentName, StringComparison.OrdinalIgnoreCase))
                ?? profiles.FirstOrDefault(p =>
                {
                    var name = GetProfileName(p);
                    return name.EndsWith("." + environmentName, StringComparison.OrdinalIgnoreCase)
                        || name.EndsWith("_" + environmentName, StringComparison.OrdinalIgnoreCase)
                        || name.EndsWith("-" + environmentName, StringComparison.OrdinalIgnoreCase);
                });
        }

        private static string GetProfileName(string profileFileName)
        {
            return profileFileName[..^PublishProfileSuffix.Length];
        }

        private static string ShellQuote(string value)
        {
            return "'" + value.Replace("'", "'\"'\"'", StringComparison.Ordinal) + "'";
        }

        private string[] PoolLines(bool windows)
        {
            if (options.AgentPool == PipelineAgentPool.MicrosoftHosted)
            {
                return new[] { "vmImage: " + (windows ? "windows-latest" : "ubuntu-latest") };
            }

            var poolName = string.IsNullOrWhiteSpace(options.AgentPoolName) ? "<agent-pool-name>" : options.AgentPoolName.Trim();
            return new[] { "name: " + YamlQuote(poolName) };
        }

        private string RenderPipeline(List<EnvironmentInfo> environments, List<KeyValuePair<string, string>> sqlCmdVariables)
        {
            var projectPath = Path.GetRelativePath(OutputRoot, Path.GetFullPath(options.ProjectFilePath)).Replace('\\', '/');
            var projectFolder = Path.GetDirectoryName(projectPath)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(projectFolder))
            {
                projectFolder = ".";
            }

            var triggerPaths = projectFolder == "."
                ? string.Empty
                : Indent(2, "paths:", "  include:", "    - " + YamlQuote(projectFolder), "    - azure-pipelines.yml", "    - pipelines") + "\n";

            var buildPool = Indent(10, PoolLines(options.ProjectStyle == PipelineProjectStyle.Classic));

            var deployPool = PoolLines(options.Authentication == PipelineAuthentication.WindowsAuthentication);

            var stages = new StringBuilder();
            var previousStage = options.IncludeContainerValidation ? "Validate" : "Build";

            foreach (var environment in environments)
            {
                stages.AppendLine();
                stages.AppendLine(Indent(
                    2,
                    $"# {environment.Name}: " + (environment.PlanBeforeDeploy
                        ? "plan (script + deploy report) -> approval on the Environment -> deploy"
                        : "deploys directly, no plan stage"),
                    "- template: pipelines/templates/deploy-database.yml",
                    "  parameters:",
                    "    environment: " + YamlQuote(environment.Name),
                    "    stageName: " + environment.StageName,
                    "    dependsOn:",
                    "      - " + previousStage,
                    "    variableGroup: " + YamlQuote(environment.VariableGroup),
                    "    publishProfile: " + YamlQuote(options.UsePublishProfiles ? environment.PublishProfile : string.Empty),
                    "    planBeforeDeploy: " + (environment.PlanBeforeDeploy ? "true" : "false"),
                    "    serviceConnection: " + YamlQuote(UsesServiceConnection ? options.ServiceConnectionName ?? string.Empty : string.Empty),
                    "    dacpacName: " + YamlQuote(DacpacName),
                    "    pool:"));
                stages.AppendLine(Indent(8, deployPool));

                previousStage = "Deploy_" + environment.StageName;
            }

            return Fill(LoadTemplate("azure-pipelines.yml"), new Dictionary<string, string>
            {
                ["ProjectName"] = ProjectName,
                ["OptionsFile"] = PipelineOptions.OptionsFileName,
                ["DefaultBranch"] = YamlQuote(string.IsNullOrWhiteSpace(options.DefaultBranch) ? "main" : options.DefaultBranch.Trim()),
                ["TriggerPaths"] = triggerPaths,
                ["ProjectPath"] = projectPath.Replace("'", "''", StringComparison.Ordinal),
                ["ProjectFolder"] = projectFolder.Replace("'", "''", StringComparison.Ordinal),
                ["CodeAnalysisComment"] = options.IncludeCodeAnalysis ? " (with code analysis)" : string.Empty,
                ["PublishProfilePattern"] = options.UsePublishProfiles ? "                *.publish.xml\n" : string.Empty,
                ["ValidateComment"] = options.IncludeContainerValidation
                    ? "#   Validate - deploy twice to a throwaway SQL Server container to test the\n#              .dacpac and that post-deployment scripts can run repeatedly\n"
                    : string.Empty,
                ["BuildPool"] = buildPool,
                ["BuildSteps"] = RenderBuildSteps(),
                ["PublishProfilePattern"] = options.UsePublishProfiles ? "                *.publish.xml\n" : string.Empty,
                ["ValidateStage"] = options.IncludeContainerValidation ? RenderValidateStage(sqlCmdVariables) : string.Empty,
                ["EnvironmentStages"] = stages.ToString().TrimEnd('\r', '\n').Replace("\r\n", "\n", StringComparison.Ordinal),
            });
        }

        private string RenderBuildSteps()
        {
            var codeAnalysis = options.IncludeCodeAnalysis;

            if (options.ProjectStyle == PipelineProjectStyle.Classic)
            {
                return Indent(
                    10,
                    "# Classic .sqlproj: requires a Windows agent with Visual Studio (SSDT) and MSBuild",
                    codeAnalysis ? "# Code analysis runs as part of the build, manage the rules with 'Manage Code Analysis Rules...'" : string.Empty,
                    "- task: VSBuild@1",
                    $"  displayName: Build {ProjectName}",
                    "  inputs:",
                    "    solution: '$(projectFile)'",
                    "    configuration: '$(buildConfiguration)'",
                    $"    msbuildArgs: '/p:RunSqlCodeAnalysis={codeAnalysis}'")
                    .Replace("\n\n", "\n", StringComparison.Ordinal);
            }

            return Indent(
                10,
                "# SDK-style project: builds with the .NET SDK on Linux, macOS or Windows agents",
                "# The .NET SDK on the agent is used - add a UseDotNet@2 task or global.json to pin a version",
                codeAnalysis ? "# Code analysis runs as part of the build, manage the rules with 'Manage Code Analysis Rules...'" : string.Empty,
                $"- script: dotnet build \"$(projectFile)\" --configuration $(buildConfiguration) -p:RunSqlCodeAnalysis={codeAnalysis}",
                $"  displayName: Build {ProjectName}")
                .Replace("\n\n", "\n", StringComparison.Ordinal);
        }

        private string RenderValidateStage(List<KeyValuePair<string, string>> sqlCmdVariables)
        {
            var variables = string.Concat(sqlCmdVariables
                .Where(v => string.IsNullOrEmpty(v.Value))
                .Select(v => $" \"/Variables:{v.Key}=validation\""));

            var lines = new[]
            {
                string.Empty,
                "# Deploy to a throwaway SQL Server container before touching real environments.",
                "# Deploying twice proves that post-deployment scripts can run repeatedly.",
                $"# Guide: {DocsUrl}#validate-in-a-throwaway-sql-server-container",
                "- stage: Validate",
                "  displayName: Validate in SQL Server container",
                "  dependsOn: Build",
                "  jobs:",
                "    - job: Validate",
                "      pool:",
                "        vmImage: ubuntu-latest",
                "      steps:",
                "        - checkout: none",
                "        - download: current",
                "          artifact: dacpac",
                string.Empty,
                "        - pwsh: |",
                "            $toolPath = Join-Path $env:AGENT_TEMPDIRECTORY 'sqlpackage'",
                "            dotnet tool install microsoft.sqlpackage --tool-path $toolPath",
                "            if ($LASTEXITCODE -ne 0) { throw 'Unable to install SqlPackage' }",
                "            Write-Host \"##vso[task.prependpath]$toolPath\"",
                "            dotnet tool list --tool-path $toolPath",
                "          displayName: Install latest SqlPackage",
                string.Empty,
                "        - bash: |",
                "            set -e",
                "            # Random password for this run only",
                "            pw=\"V$(openssl rand -hex 16)!a\"",
                "            echo \"##vso[task.setvariable variable=ValidationSaPassword;issecret=true]$pw\"",
                "            docker run -d --name sqlvalidation -e ACCEPT_EULA=Y -e \"MSSQL_SA_PASSWORD=$pw\" -p 1433:1433 mcr.microsoft.com/mssql/server:2025-latest",
                "            for i in $(seq 1 60); do",
                "              if docker exec sqlvalidation /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P \"$pw\" -Q \"SELECT 1\" > /dev/null 2>&1; then exit 0; fi",
                "              sleep 2",
                "            done",
                "            echo \"SQL Server container did not start\"",
                "            exit 1",
                "          displayName: Start SQL Server container",
                string.Empty,
                "        - pwsh: |",
                "            $dacpac = Join-Path $env:PIPELINE_WORKSPACE 'dacpac/" + DacpacName.Replace("'", "''", StringComparison.Ordinal) + ".dacpac'",
                "            foreach ($attempt in 1..2) {",
                "              Write-Host \"Deployment $attempt of 2\"",
                "              sqlpackage /Action:Publish \"/SourceFile:$dacpac\" /TargetServerName:localhost /TargetDatabaseName:Validation /TargetUser:sa \"/TargetPassword:$env:SA_PASSWORD\" /TargetTrustServerCertificate:True /p:AllowIncompatiblePlatform=True" + variables,
                "              if ($LASTEXITCODE -ne 0) { throw \"Validation deployment $attempt failed\" }",
                "            }",
                "          displayName: Deploy twice to the container",
                "          env:",
                "            SA_PASSWORD: $(ValidationSaPassword)",
            };

            return Indent(2, lines);
        }

        private string RenderSqlPackageSteps(List<KeyValuePair<string, string>> sqlCmdVariables)
        {
            var requiredVariables = new List<string>();
            var envMappings = new List<string>();
            var authArguments = new List<string>();

            if (UsesTemporaryFirewallRule)
            {
                requiredVariables.Add("SqlResourceGroupName - resource group of the logical server (firewall rule)");
            }

            switch (options.Authentication)
            {
                case PipelineAuthentication.WorkloadIdentityFederation:
                    authArguments.Add("# Passwordless: a Microsoft Entra token for the service connection identity");
                    authArguments.Add($"# Guide: {DocsUrl}#passwordless-access-to-azure-sql");
                    authArguments.Add("$token = az account get-access-token --resource https://database.windows.net/ --query accessToken --output tsv");
                    authArguments.Add("if ($LASTEXITCODE -ne 0 -or -not $token) { throw 'Unable to get an access token for Azure SQL' }");
                    authArguments.Add("Write-Host \"##vso[task.setsecret]$token\"");
                    authArguments.Add("$arguments += \"/AccessToken:$token\"");
                    break;
                case PipelineAuthentication.SqlAuthentication:
                    requiredVariables.Add("SqlUserName     - SQL authentication user");
                    requiredVariables.Add("SqlPassword     - SQL authentication password (mark as secret!)");
                    envMappings.Add("SQL_USER: $(SqlUserName)");
                    envMappings.Add("SQL_PASSWORD: $(SqlPassword)");
                    authArguments.Add("# SQL authentication - consider passwordless access with workload identity federation");
                    authArguments.Add("$arguments += \"/TargetUser:$env:SQL_USER\"");
                    authArguments.Add("$arguments += \"/TargetPassword:$env:SQL_PASSWORD\"");
                    break;
                case PipelineAuthentication.WindowsAuthentication:
                    authArguments.Add("# Windows authentication: the agent service account must have access to the database");
                    break;
            }

            if (options.TargetPlatform == PipelineTargetPlatform.SqlServer)
            {
                authArguments.Add("# Remove when the SQL Server uses a certificate trusted by the agent");
                authArguments.Add("$arguments += '/TargetTrustServerCertificate:True'");
            }

            var sqlCmdArguments = new List<string>();
            if (sqlCmdVariables.Count > 0)
            {
                sqlCmdArguments.Add("# SQLCMD variables - values come from the environment variable group");
            }

            foreach (var variable in sqlCmdVariables)
            {
                requiredVariables.Add($"{variable.Key} - SQLCMD variable");
                envMappings.Add($"SQLCMD_{variable.Key}: $({variable.Key})");
                sqlCmdArguments.Add($"$arguments += \"/Variables:{variable.Key}=${{env:SQLCMD_{variable.Key}}}\"");
            }

            string runStepHeader;
            if (options.Authentication == PipelineAuthentication.WorkloadIdentityFederation)
            {
                runStepHeader = Indent(
                    2,
                    "- task: AzureCLI@2",
                    "  inputs:",
                    "    azureSubscription: ${{ parameters.serviceConnection }}",
                    "    scriptType: pscore",
                    "    scriptLocation: inlineScript",
                    "    inlineScript: |");
            }
            else
            {
                runStepHeader = Indent(2, "- pwsh: |");
            }

            return Fill(LoadTemplate("sqlpackage-steps.yml"), new Dictionary<string, string>
            {
                ["RequiredVariablesComment"] = string.Concat(requiredVariables.Select(v => "#   " + v + "\n")),
                ["FirewallOpen"] = RenderFirewallOpen(),
                ["FirewallClose"] = RenderFirewallClose(),
                ["RunStepHeader"] = runStepHeader,
                ["AuthArguments"] = authArguments.Count > 0 ? Indent(8, authArguments.ToArray()) + "\n" : string.Empty,
                ["SqlCmdArguments"] = sqlCmdArguments.Count > 0 ? Indent(8, sqlCmdArguments.ToArray()) + "\n" : string.Empty,
                ["EnvMappings"] = envMappings.Count > 0 ? Indent(6, envMappings.ToArray()) + "\n" : string.Empty,
            });
        }

        private string RenderFirewallOpen()
        {
            if (UsesTemporaryFirewallRule)
            {
                return "\n" + Indent(
                    2,
                    "# Microsoft-hosted agents get a new IP address on every run: open a temporary",
                    "# firewall rule for this agent only, and ALWAYS remove it again (last step).",
                    "# Requires public network access on the server and permission for the service",
                    "# connection identity to change firewall rules (for example SQL Server Contributor).",
                    $"# Guide: {DocsUrl}#firewall-issues-on-microsoft-hosted-agents",
                    "- task: AzureCLI@2",
                    "  displayName: Open temporary firewall rule for the agent IP",
                    "  inputs:",
                    "    azureSubscription: ${{ parameters.serviceConnection }}",
                    "    scriptType: pscore",
                    "    scriptLocation: inlineScript",
                    "    inlineScript: |",
                    "      $ErrorActionPreference = 'Stop'",
                    "      $ip = Invoke-RestMethod -Uri 'https://api.ipify.org'",
                    "      $server = $env:SQL_SERVER.Split('.')[0]",
                    "      $rule = \"azdo-$env:BUILD_BUILDID-$env:SYSTEM_JOBID\"",
                    "      Write-Host \"Adding firewall rule $rule for $ip on server $server\"",
                    "      az sql server firewall-rule create --resource-group $env:SQL_RESOURCE_GROUP --server $server --name $rule --start-ip-address $ip --end-ip-address $ip --output none",
                    "      if ($LASTEXITCODE -ne 0) { throw 'Unable to create the firewall rule' }",
                    "  env:",
                    "    SQL_SERVER: $(SqlServerName)",
                    "    SQL_RESOURCE_GROUP: $(SqlResourceGroupName)") + "\n";
            }

            if (options.FirewallStrategy == PipelineFirewallStrategy.PrivateNetwork)
            {
                return "\n" + Indent(
                    2,
                    "# Private network: the agent (Managed DevOps Pool or self-hosted agent in a VNet)",
                    "# reaches the database through a private endpoint, no firewall rule is needed.",
                    $"# Guide: {DocsUrl}#firewall-issues-on-microsoft-hosted-agents") + "\n";
            }

            return string.Empty;
        }

        private string RenderFirewallClose()
        {
            if (!UsesTemporaryFirewallRule)
            {
                return string.Empty;
            }

            return "\n" + Indent(
                2,
                "# Runs even when the deployment fails or is cancelled",
                "- task: AzureCLI@2",
                "  displayName: Remove temporary firewall rule",
                "  condition: always()",
                "  inputs:",
                "    azureSubscription: ${{ parameters.serviceConnection }}",
                "    scriptType: pscore",
                "    scriptLocation: inlineScript",
                "    inlineScript: |",
                "      $server = $env:SQL_SERVER.Split('.')[0]",
                "      $rule = \"azdo-$env:BUILD_BUILDID-$env:SYSTEM_JOBID\"",
                "      az sql server firewall-rule delete --resource-group $env:SQL_RESOURCE_GROUP --server $server --name $rule --output none",
                "  env:",
                "    SQL_SERVER: $(SqlServerName)",
                "    SQL_RESOURCE_GROUP: $(SqlResourceGroupName)") + "\n";
        }

        private string RenderGrantScript()
        {
            var userName = options.Authentication switch
            {
                PipelineAuthentication.WorkloadIdentityFederation => string.IsNullOrWhiteSpace(options.DeploymentIdentityName) ? "<deployment-identity-name>" : options.DeploymentIdentityName.Trim(),
                PipelineAuthentication.WindowsAuthentication => @"<DOMAIN\agent-service-account>",
                _ => "pipeline_deployer",
            };

            var escapedUserName = userName.Replace("]", "]]", StringComparison.Ordinal);

            string connectAs;
            string createUser;

            switch (options.Authentication)
            {
                case PipelineAuthentication.WorkloadIdentityFederation:
                    connectAs = "-- Connect as a Microsoft Entra admin of the server.\n"
                        + $"-- '{userName}' is the display name of the app registration or managed identity\n"
                        + $"-- used by the Azure DevOps service connection '{options.ServiceConnectionName}'.";
                    createUser = $"CREATE USER [{escapedUserName}] FROM EXTERNAL PROVIDER;\nGO";
                    break;
                case PipelineAuthentication.WindowsAuthentication:
                    connectAs = "-- Connect as a sysadmin. Replace the account with the agent service account.";
                    createUser = $"USE [master];\nGO\nCREATE LOGIN [{escapedUserName}] FROM WINDOWS;\nGO\nUSE [<target-database-name>];\nGO\nCREATE USER [{escapedUserName}] FOR LOGIN [{escapedUserName}];\nGO";
                    break;
                default:
                    connectAs = "-- Connect as an admin. Store the password as the secret variable 'SqlPassword'\n"
                        + "-- in the variable group - never in source control.";
                    createUser = options.TargetPlatform == PipelineTargetPlatform.SqlServer
                        ? $"USE [master];\nGO\nCREATE LOGIN [{escapedUserName}] WITH PASSWORD = '<strong-password>';\nGO\nUSE [<target-database-name>];\nGO\nCREATE USER [{escapedUserName}] FOR LOGIN [{escapedUserName}];\nGO"
                        : $"-- Contained database user\nCREATE USER [{escapedUserName}] WITH PASSWORD = '<strong-password>';\nGO";
                    break;
            }

            return Fill(LoadTemplate("grant-pipeline-access.sql"), new Dictionary<string, string>
            {
                ["ProjectName"] = ProjectName,
                ["ConnectAs"] = connectAs,
                ["CreateUser"] = createUser,
                ["UserName"] = escapedUserName,
            });
        }

        private string RenderSetupGuide(List<EnvironmentInfo> environments, List<KeyValuePair<string, string>> sqlCmdVariables)
        {
            var profileRows = string.Concat(environments
                .Where(e => options.UsePublishProfiles && e.CreateProfile)
                .Select(e => $"| `{e.PublishProfile}` | Publish profile for {e.Name} (in the project folder) |\n"));

            var environmentRows = string.Concat(environments
                .Select(e => $"| {e.Name} | `{e.VariableGroup}` | {(options.UsePublishProfiles ? $"`{e.PublishProfile}`" : "-")} | {(e.PlanBeforeDeploy ? "Yes" : "No")} |\n"));

            var variables = new List<string>
            {
                "`SqlServerName` - for example `myserver.database.windows.net`",
                "`DatabaseName` - the target user database",
            };

            if (UsesTemporaryFirewallRule)
            {
                variables.Add("`SqlResourceGroupName` - resource group of the logical server");
            }

            if (options.Authentication == PipelineAuthentication.SqlAuthentication)
            {
                variables.Add("`SqlUserName` - SQL authentication user");
                variables.Add("`SqlPassword` - SQL authentication password, **mark it as secret** (or link the group to Azure Key Vault)");
            }

            foreach (var variable in sqlCmdVariables)
            {
                variables.Add($"`{variable.Key}` - SQLCMD variable");
            }

            var authenticationSteps = new List<string>();
            var az = new List<string>
            {
                $"az devops configure --defaults {ShellQuote("organization=https://dev.azure.com/<organization>")} {ShellQuote("project=<project>")}",
            };

            if (UsesServiceConnection)
            {
                authenticationSteps.Add(
                    $"- [ ] **Create the Azure Resource Manager service connection** `{options.ServiceConnectionName}` (Project settings > Service connections > New > Azure Resource Manager) using **workload identity federation** - there is no secret to rotate.");
            }

            if (options.Authentication == PipelineAuthentication.WorkloadIdentityFederation)
            {
                authenticationSteps.Add("- [ ] **Make sure the logical server has a Microsoft Entra admin**, then run `pipelines/setup/grant-pipeline-access.sql` once in each target database to create a database user for the service connection identity. This is the step most people miss.");
            }
            else
            {
                authenticationSteps.Add("- [ ] **Run `pipelines/setup/grant-pipeline-access.sql`** once in each target database to give the deployment account minimal permissions.");
            }

            var firewallSteps = new List<string>();
            if (UsesTemporaryFirewallRule)
            {
                firewallSteps.Add("- [ ] **Allow the service connection identity to manage firewall rules**, for example the *SQL Server Contributor* role on the logical server. Public network access must be enabled on the server. Do not use *Allow Azure services and resources to access this server*.");
                az.Add(string.Empty);
                az.Add("# Allow the service connection identity to add and remove firewall rules");
                az.Add($"az role assignment create --assignee {ShellQuote("<service-connection-app-id>")} --role {ShellQuote("SQL Server Contributor")} --scope {ShellQuote("/subscriptions/<subscription-id>/resourceGroups/<resource-group>/providers/Microsoft.Sql/servers/<server>")}");
            }
            else if (options.FirewallStrategy == PipelineFirewallStrategy.PrivateNetwork)
            {
                firewallSteps.Add($"- [ ] **Make sure the agent pool `{options.AgentPoolName}` can reach the private endpoint** of the database server, and disable public network access on the server.");
            }

            az.Add(string.Empty);
            az.Add("# One variable group per environment");
            foreach (var environment in environments)
            {
                var variableArguments = new List<KeyValuePair<string, string>>
                {
                    new("SqlServerName", "<server>.database.windows.net"),
                    new("DatabaseName", DacpacName),
                };
                if (UsesTemporaryFirewallRule)
                {
                    variableArguments.Add(new KeyValuePair<string, string>("SqlResourceGroupName", "<resource-group>"));
                }

                if (options.Authentication == PipelineAuthentication.SqlAuthentication)
                {
                    variableArguments.Add(new KeyValuePair<string, string>("SqlUserName", "pipeline_deployer"));
                }

                variableArguments.AddRange(sqlCmdVariables.Select(v => new KeyValuePair<string, string>(v.Key, "<value>")));

                var quotedVariableArguments = string.Join(" ", variableArguments.Select(v => ShellQuote($"{v.Key}={v.Value}")));
                az.Add($"az pipelines variable-group create --name {ShellQuote(environment.VariableGroup)} --variables {quotedVariableArguments}");
                if (options.Authentication == PipelineAuthentication.SqlAuthentication)
                {
                    az.Add($"az pipelines variable-group variable create --group-id {ShellQuote($"<{environment.VariableGroup}-id>")} --name {ShellQuote("SqlPassword")} --secret true");
                }
            }

            az.Add(string.Empty);
            az.Add("# Create the pipeline from the YAML file");
            az.Add($"az pipelines create --name {ShellQuote(ProjectName + " database")} --yml-path {ShellQuote("azure-pipelines.yml")} --repository {ShellQuote("<repository>")} --repository-type tfsgit --branch {ShellQuote(options.DefaultBranch ?? "main")} --skip-first-run true");

            return Fill(LoadTemplate("PIPELINE-SETUP.md"), new Dictionary<string, string>
            {
                ["ProjectName"] = ProjectName,
                ["ValidateRow"] = options.IncludeContainerValidation ? "container validation, " : string.Empty,
                ["ProfileRows"] = profileRows,
                ["EnvironmentRows"] = environmentRows,
                ["EnvironmentList"] = string.Join(", ", environments.Select(e => $"`{e.Name}`")) + " (they are also created automatically on the first run, if you have permission)",
                ["AuthenticationSteps"] = string.Join("\n", authenticationSteps),
                ["VariableList"] = string.Join("\n", variables.Select(v => "  - " + v)),
                ["FirewallSteps"] = string.Join("\n", firewallSteps),
                ["ServiceConnectionAuthorize"] = UsesServiceConnection ? " and the service connection" : string.Empty,
                ["AzCommands"] = string.Join("\n", az),
            });
        }

        private sealed record EnvironmentInfo(
            string Name,
            string StageName,
            string VariableGroup,
            string PublishProfile,
            bool CreateProfile,
            bool PlanBeforeDeploy);
    }
}
