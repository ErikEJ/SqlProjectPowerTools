# SQL Database Projects Power Tools

Visual Studio Tools to enhance the SQL Database Projects developer experience.

## Features

The following features are planned. Each feature will apply to both classic .sqlproj and MsBuild.Sdk.SqlProj projects, unless noted.

### Preview 1

- Import database (for MsBuild.Sdk.SqlProj only)
- Analyze (html output of analysis of your database project)
- Create Mermaid E/R diagram of selected tables from your database project
- Unpack dacpac 
- Include the MsBuild.Sdk.SqlProj templates - for `New Project` and for adding new items

### Future

- Data API Builder scaffold (from EF Core Power Tools)
- Schema compare (for MsBuild.Sdk.SqlProj)

## Related projects

The plan is to release this as a Visual Studio extension, similar to EF Core Power Tools.

In addition, the plan is to publish an extension pack for SQL Database Projects Power Tools, which will include:

- SQL Database Projects Power Tools from ErikEJ
- T-SQL Analyzer from ErikEJ
- SQL Formatter from Mads Kristensen
