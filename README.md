[marketplace]: <https://marketplace.visualstudio.com/items?itemName=ErikEJ.SQLProjectPowerTools>
[vsixgallery]: <https://www.vsixgallery.com/extension/SqlProjectsPowerTools.0e226f35-6d47-4156-88df-f9d40db5e2d1>
[repo]:<https://github.com/ErikEJ/SqlProjectPowerTools>

# SQL Database Project Power Tools

[![Build](https://github.com/ErikEJ/SqlProjectPowerTools/actions/workflows/vsix.yml/badge.svg)](https://github.com/ErikEJ/SqlProjectPowerTools/actions/workflows/vsix.yml)
![GitHub Sponsors](https://img.shields.io/github/sponsors/ErikEJ)

Download this extension from the [Visual Studio Marketplace][marketplace]
or get the [CI build][vsixgallery]

----------------------------------------

Visual Studio Tools to enhance the SQL Database Project developer experience.

[Getting Started Guide](docs/getting-started.md) | [Overview blog post: SQL Project Power Tools for Visual Studio](https://erikej.github.io/dotnet/dacfx/sqlserver/visualstudio/2025/09/30/sqlproj-power-tools-visualstudio.html)

## Features

The tool contains the following features.

- **Templates** - for use with `New Project` and for adding new items
- **Import database** - import the schema and database settings from an existing database
- **Schema compare** - compare your database project with a live database and get a script to update the database or your project
- **Analyze** - report with static code analysis result of your database project
- **Create Mermaid E/R diagram** - create an Entity/Relationship diagram of selected tables from your database project
- **.dacpac Solution explorer node** - view the contents of a dacpac file in Solution Explorer
- **Script Table Data** - generate insert statements for table data in your database project, based on [generate-sql-merge](https://github.com/dnlnln/generate-sql-merge)
- **Add new pre- and post-deployment scripts** - easily add new pre- and post-deployment scripts to your database project
- **Scaffold Data API Builder (preview)** - generate a Data API Builder configuration file based on your database project, for use with [Data API Builder](https://learn.microsoft.com/azure/data-api-builder/)

### Power Pack

I have also published [SQL Project Power Pack](https://marketplace.visualstudio.com/items?itemName=ErikEJ.SqlProjectPowerPack) which adds additional features.

- [T-SQL Analyzer](https://marketplace.visualstudio.com/items?itemName=ErikEJ.TSqlAnalyzer) - live code analysis of your T-SQL object creation code for design, naming and performance issues using more than 140 rules.
- [SQL Formatter](https://marketplace.visualstudio.com/items?itemName=MadsKristensen.SqlFormatter) - formats T-SQL code to a consistent and readable layout with .editorconfig support.

## Advanced topics

Have a look at our extensive [user guide](https://github.com/rr-wfm/MSBuild.Sdk.SqlProj/blob/master/README.md) for more information on topics like:

- reference user and system databases
- set database and build properties
- use pre- and post-deployment scripts
- pack and publish dacpacs
- use sqlcmd variables
- static code analysis customization
- and much more

## How can I help?

If you enjoy using the extension, please give it a ★★★★★ rating on the [Visual Studio Marketplace][marketplace].

Should you encounter bugs or have feature requests, head over to the [GitHub repo][repo] to open an issue if one doesn't already exist.

Another way to help out is to [sponsor me on GitHub](https://github.com/sponsors/ErikEJ).

If you would like to contribute code, please fork the [GitHub repo][repo] and submit a pull request.
