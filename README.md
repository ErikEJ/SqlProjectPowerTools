# SQL Database Project Power Tools

Visual Studio Tools to enhance the SQL Database Projects developer experience.

[Overview blog post](https://erikej.github.io/dotnet/dacfx/sqlserver/visualstudio/2025/09/30/sqlproj-power-tools-visualstudio.html)

## Features

The tool contains the following features. Each feature applies to both classic .sqlproj and MsBuild.Sdk.SqlProj projects, unless noted.

- **Import database** - for MsBuild.Sdk.SqlProj only
- **Analyze** - html output of analysis of your database project
- **Create Mermaid E/R diagram** - diagram selected tables from your database project
- **Unpack dacpac** - extract a .dacpac into a single .sql file
- **Include the MsBuild.Sdk.SqlProj templates** - for `New Project` and for adding new items

### How to install

Download the latest version of the Visual Studio extension from [Visual Studio MarketPlace](https://marketplace.visualstudio.com/items?itemName=ErikEJ.SqlProjectPowerTools)

Or simply install from the Extensions dialog in Visual Studio.

I have also published [SQL Project Power Pack](https://marketplace.visualstudio.com/items?itemName=ErikEJ.SqlProjectPowerPack) which adds T-SQL Analyzer and SQL Formatter.

### Daily build

You can download the daily build from [Open VSIX Gallery](https://www.vsixgallery.com/extension/SqlProjectsPowerTools.0e226f35-6d47-4156-88df-f9d40db5e2d1)

### Future (under consideration)

- **Data API Builder scaffold** - build a Data API Builder config file from your database project
- **Schema compare** - compare your database project to a live database or dacpac
