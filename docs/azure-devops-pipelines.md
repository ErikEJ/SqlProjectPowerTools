# Deploying SQL Database Projects with Azure DevOps

This guide covers the hard parts of deploying a SQL database project from an Azure DevOps pipeline, and how SQL Project Power Tools can create a starter pipeline for you.

Each topic follows the same structure: **the problem**, **why it is hard**, **the recommended pattern** and **what the generated pipeline does for you**. The generated YAML has comments that link back to these sections.

- [Add a starter pipeline to your project](#add-a-starter-pipeline-to-your-project)
- [Passwordless access to Azure SQL](#passwordless-access-to-azure-sql)
- [Firewall issues on Microsoft-hosted agents](#firewall-issues-on-microsoft-hosted-agents)
- [Stages that script the intended changes](#stages-that-script-the-intended-changes)
- [Using the latest SqlPackage](#using-the-latest-sqlpackage)
- [Publish profiles and SQLCMD variables](#publish-profiles-and-sqlcmd-variables)
- [Classic and SDK-style projects need different agents](#classic-and-sdk-style-projects-need-different-agents)
- [Validate in a throwaway SQL Server container](#validate-in-a-throwaway-sql-server-container)
- [Other things to get right](#other-things-to-get-right)

## Add a starter pipeline to your project

1. Right-click your SQL database project in Solution Explorer
2. Select **SQL Project Power Tools > Add Azure DevOps Pipeline...**
3. Pick the target, agent pool, authentication, firewall strategy, environments, whether to use publish profiles, and optional stages
4. Select **OK**

The tool reads these facts from your project, so you don't have to enter them:

- The project style (classic, `Microsoft.Build.Sql` or `MSBuild.Sdk.SqlProj`), which decides the agent image and build step
- The project path relative to the root of your git repository
- The `.dacpac` name and the target platform
- The SQLCMD variables defined in the project (the project is built and the variables are read from the `.dacpac`)
- Existing `*.publish.xml` publish profiles, and whether code analysis is enabled

These files are created, relative to the root of your git repository:

| File | Purpose |
| --- | --- |
| `azure-pipelines.yml` | The pipeline: build, optional validation, then one stage per environment |
| `pipelines/templates/deploy-database.yml` | Reusable plan/deploy stage template for one environment |
| `pipelines/templates/sqlpackage-steps.yml` | Installs the latest SqlPackage and runs `Script`, `DeployReport` or `Publish` |
| `pipelines/setup/grant-pipeline-access.sql` | One-time script that creates the pipeline identity as a database user |
| `pipelines/PIPELINE-SETUP.md` | Checklist of what to set up in Azure and Azure DevOps, with matching `az` commands |
| `<Project>.<environment>.publish.xml` | One publish profile per environment, added to the project if missing (only when publish profiles are enabled) |

Your choices are saved in `pipeline-options.json` next to the project file, so you can run the command again to regenerate the pipeline. If generated files already exist, you are asked whether to overwrite them. Publish profiles are never overwritten.

Start with `pipelines/PIPELINE-SETUP.md`: the pipeline will not run until the service connection, environments and variable groups exist.

## Passwordless access to Azure SQL

**The problem:** the pipeline needs to sign in to the database, and a SQL login password stored in a variable is a secret that must be rotated, can leak in logs and is often shared between environments.

**Why it is hard:** passwordless access involves three separate pieces that must all line up: an identity in Microsoft Entra ID, a service connection in Azure DevOps that can sign in as that identity, and a user for that identity inside each database.

**Recommended pattern:**

- Create an **Azure Resource Manager service connection** that uses **workload identity federation**. Azure DevOps exchanges a short-lived token with Entra ID on every run, so there is no client secret to store or rotate.
- Deploy from an `AzureCLI@2` step that uses the service connection. Inside the step, get an access token for `https://database.windows.net/` with `az account get-access-token`, and pass it to SqlPackage with `/AccessToken:`. Alternatively, use `Authentication=Active Directory Default` in the connection string, which picks up the Azure CLI sign-in.
- **The step people miss:** the identity must exist as a user in every target database. A Microsoft Entra admin of the server runs `CREATE USER [<identity>] FROM EXTERNAL PROVIDER` once per database, and grants roles to that user.
- **If the admin cannot query Microsoft Entra ID** (for example, the identity lacks Microsoft Graph permission), `FROM EXTERNAL PROVIDER` fails. On Azure SQL Database and SQL database in Microsoft Fabric, create the user from the application (client) ID instead: `CREATE USER [<identity>] WITH SID = <sid>, TYPE = E`, where `<sid>` is the client ID as a `binary(16)` value (`SELECT CONVERT(varbinary(16), CAST('<application-client-id>' AS uniqueidentifier));`). Use `TYPE = E` for a user, service principal or managed identity, and `TYPE = X` for a group.
- Grant the smallest set of roles that works. `db_owner` is not required for most deployments: `db_ddladmin` lets SqlPackage change the schema, and `db_datareader` plus `db_datawriter` cover post-deployment scripts that read and write data. Add `db_owner` only if your project manages permissions, users or other database-level settings.

**What the generator does:**

- With **Passwordless (workload identity federation)** selected, every SqlPackage call runs inside `AzureCLI@2` with an Entra access token. The token is marked as a secret so it is masked in logs.
- `pipelines/setup/grant-pipeline-access.sql` contains the `CREATE USER ... FROM EXTERNAL PROVIDER` statement (with a commented `CREATE USER ... WITH SID = ..., TYPE = E` alternative for when Microsoft Entra ID cannot be queried) and role statements for the identity name you enter.
- `pipelines/PIPELINE-SETUP.md` explains how to create the service connection and find the identity name.

SQL authentication (user name and password in a secret variable in a variable group) and Windows authentication (self-hosted agent running as a domain account) are also available.

## Firewall issues on Microsoft-hosted agents

**The problem:** a Microsoft-hosted agent gets a new IP address on every run, so the Azure SQL server firewall blocks it.

**Why it is hard:** the agent's IP address is not known in advance, and the address ranges for Azure DevOps are large and change over time.

**The options, from weakest to strongest:**

1. **Allow Azure services and resources to access this server** (the `0.0.0.0` rule). Avoid this: it allows connections from any Azure tenant, not only yours.
2. **A temporary firewall rule.** Detect the agent's public IP address, add a firewall rule for it with `az sql server firewall-rule create`, and always remove the rule in a final step that runs even if the deployment fails. The `SqlAzureDacpacDeployment@1` task can also add the agent IP automatically.
3. **A private network.** Use a [Managed DevOps Pool](https://learn.microsoft.com/azure/devops/managed-devops-pools/overview) or self-hosted agents in a virtual network, reach the database through a private endpoint, and disable public network access on the server.

Options 1 and 2 need public network access to be enabled on the server. Option 3 removes the problem completely and is the best choice for production.

**What the generator does:**

- With **Temporary firewall rule** (Azure SQL Database only), each job that connects to the database adds a rule named `azdo-<build id>-<job id>` for the agent's IP address, and removes it in a step with `condition: always()`.
- The service connection identity needs permission to change the firewall, for example the **SQL Server Contributor** role on the server. `PIPELINE-SETUP.md` includes the `az role assignment create` command.
- With **Private network** or **None**, no firewall steps are added.

## Stages that script the intended changes

**The problem:** a reviewer approves a production deployment without knowing what SQL will run.

**Why it is hard:** the changes depend on the current state of the target database, so they can only be computed by comparing the `.dacpac` with the live database just before the deployment.

**Recommended pattern:**

1. **Build:** compile the project, run code analysis, and publish the `.dacpac` (and any publish profiles) as a pipeline artifact.
2. **Plan:** run SqlPackage `Script` and `DeployReport` against the target. Publish the `.sql` script and `.xml` report as an artifact, and show a readable summary on the run page.
3. **Approve:** deploy to an Azure DevOps **Environment** with an approval check, so reviewers read the plan before approving.
4. **Deploy:** a deployment job that uses the same `.dacpac` (and the publish profile, if enabled).

The plan stage only runs for **staging and production type environments**: environments with a name that contains `prod`, `stag`, `stg` or `live`. Development and test environments deploy directly to keep the feedback loop short.

**Drift between plan and deploy:** the database can change between the plan and the approval. There are two ways to deal with this:

- **Option A:** run the approved script with `sqlcmd` (go-sqlcmd supports Entra authentication). What was approved is exactly what runs, but you lose SqlPackage's safety checks at deploy time.
- **Option B (generated by default):** create the `DeployReport` again at deploy time and fail the deployment if it differs from the approved report. SqlPackage then publishes with all its safety checks.

**What the generator does:**

- For each staging or production environment, a `Plan` job publishes a `plan-<environment>` artifact with the script, the deploy report and a summary, then a deployment job targets the Azure DevOps environment where you configure the approval check.
- The deployment job compares a new deploy report with the approved one and fails on drift. If it fails, run the pipeline again to create a new plan.
- Stages run in order, for example `dev` > `test` > `prod`, and are skipped for pull request builds.

## Using the latest SqlPackage

**The problem:** the SqlPackage that is pre-installed on hosted images, or bundled with the Azure DevOps tasks, often lags behind the latest release. You miss bug fixes, performance improvements and support for new Azure SQL features.

**Recommended pattern:** install the latest version of the [`microsoft.sqlpackage`](https://www.nuget.org/packages/microsoft.sqlpackage) .NET tool at the start of every job that runs SqlPackage, and print the version to the log so every run shows which version was used.

**What the generator does:** `pipelines/templates/sqlpackage-steps.yml` installs the latest `microsoft.sqlpackage` into the agent's temporary directory with `dotnet tool install`, adds it to the `PATH` and logs the installed version. No SqlPackage version is pinned, so every run uses the latest release.

If you need a reproducible version instead, pin it with `--version` on the `dotnet tool install` command.

## Publish profiles and SQLCMD variables

- Publish profiles are **optional and off by default**. Enable **Use a publish profile per environment** in the wizard to generate (and reference) one publish profile per environment (`<Project>.<environment>.publish.xml`) for deployment options such as `BlockOnPossibleDataLoss` and `DropObjectsNotInSource`. Keep server names, database names and secrets out of the profiles. When disabled, SqlPackage runs with its default options and no profile files are created.
- Map SQLCMD variables to pipeline variables. The generated pipeline reads the variables from a variable group per environment, `sql-<environment>`, so each environment can have its own values. Mark secret values as secret, or link the variable group to Azure Key Vault.
- When publish profiles are enabled, the generator reuses existing publish profiles whose name ends with the environment name, and creates missing ones.

## Classic and SDK-style projects need different agents

- **SDK-style projects** (`Microsoft.Build.Sql` and `MSBuild.Sdk.SqlProj`) build with `dotnet build` on any agent, including the faster `ubuntu-latest` image.
- **Classic `.sqlproj` projects** need MSBuild with the SQL Server Data Tools build targets, so they build with `VSBuild@1` on a `windows-latest` agent.

The generator picks the right image and build step for your project. Consider converting a classic project to SDK-style to build on Linux.

## Validate in a throwaway SQL Server container

Optionally, deploy the `.dacpac` to an empty SQL Server container before touching a real environment. This catches deployment errors, and post-deployment scripts that fail or are not safe to run more than once. The generated `Validate` stage starts a SQL Server 2025 container on an Ubuntu agent and publishes the `.dacpac` twice.

This works best for projects that don't depend on Azure-only features.

## Other things to get right

- **Post-deployment scripts must be safe to run repeatedly.** They run on every deployment. The MERGE scripts created by **Script Table Data** already are.
- **There is no automatic rollback.** SqlPackage does not undo a deployment. Rely on point-in-time restore or backups, and fix forward.
- **Choose `DropObjectsNotInSource` and `BlockOnPossibleDataLoss` on purpose** for each environment. When publish profiles are enabled, keep the choice in the publish profile.
- **Version the `.dacpac`** and record the version in the deployment logs, so you know what was deployed where.
- **GitHub Actions** is not supported by the generator yet.
