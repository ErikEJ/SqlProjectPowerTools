---
layout: post
title:  "Instantly deploy T-SQL programmability changes on save with SQL Project Power Tools"
image: https://raw.githubusercontent.com/ErikEJ/SQLProjectPowerTools/main/img/menu.png
date:   2026-05-29 12:00:00 +0100
categories: dotnet dacfx sqlserver visualstudio
---

In this blog post I will describe a new feature in [SQL Project Power Tools](https://marketplace.visualstudio.com/items?itemName=ErikEJ.SqlProjectPowerTools): **Publish programmability objects on save**. This feature lets you instantly deploy stored procedures, views, functions, and triggers to your development database the moment you save the corresponding `.sql` file in Visual Studio — no full DACpac deployment required.

## The problem: slow feedback loop for programmability objects

SQL Database Projects are a great way to manage your database schema in source control. The normal development workflow is:

1. Edit your `.sql` files in Visual Studio
2. Build the project to produce a `.dacpac` file
3. Deploy the `.dacpac` to your development database (via dacpac publish or schema compare)
4. Test your changes

This workflow works well for schema objects like **tables** and **indexes**, where DACpac deployment is essential — it handles complex operations like renaming columns, adding constraints, and managing data migrations safely. You really do need the full deployment pipeline for those.

However, for **programmability objects** — stored procedures, views, functions, and triggers — the situation is different. These objects can be replaced atomically using SQL Server's `CREATE OR ALTER` statement, which has been supported since SQL Server 2016. There is no need to build and deploy a full `.dacpac` just to update a stored procedure body.

Unfortunately, SQL Database Projects (and the underlying DACfx tooling) do not allow you to selectively deploy a single file. Every change still requires building the full `.dacpac` and running a deployment, even if you only changed a single stored procedure. This has been raised as a [feature request with the DacFx team](https://github.com/microsoft/DacFx/issues/753), but in the meantime SQL Project Power Tools provides a practical workaround.

## The solution: Publish programmability objects on save

The new **Publish programmability objects on save** feature (previously referred to internally as "auto publish") detects when you save a `.sql` file that contains a supported programmability object and immediately executes a `CREATE OR ALTER` version of that script against your development database. The status bar shows a confirmation when the publish completes, or an error message if something goes wrong.

> **Important distinction:** This feature only works for *programmability objects* — stored procedures, views, functions, and triggers. Schema objects such as tables, indexes, and constraints are deliberately excluded. Changing a table definition has data implications that require the full DACpac deployment pipeline to handle safely.

## How it works

When a `.sql` file is saved in Visual Studio:

1. The extension checks whether the file belongs to a SQL Database Project.
2. It looks for an `.env` file in the project directory containing an `AutoPublish` connection string.
3. If found, it parses the script using the T-SQL parser and verifies it contains only supported statements: `CREATE PROCEDURE`, `CREATE VIEW`, `CREATE FUNCTION`, or `CREATE TRIGGER` (and `SET` options).
4. Any `CREATE` statement that is not already `CREATE OR ALTER` is automatically rewritten to use `CREATE OR ALTER`.
5. The rewritten script is executed against the database specified in the connection string.
6. The Visual Studio status bar shows `Auto publish completed: <filename>` on success, or `Auto publish failed: <filename>` on failure.

Files that contain table definitions, `ALTER TABLE`, `INSERT`, `DROP`, or any other statements that are not programmability object definitions are silently skipped — the feature does nothing for those files.

## Setting up the feature

### Step 1: Enable the option

The feature is off by default. To turn it on, open **Tools > Options > SQL Server Tools > SQL Project Power Tools** in Visual Studio and check the **Enable auto publish on save** option.

### Step 2: Add the connection string

Create a file named `.env` in the root of your SQL Database Project directory (the same folder as your `.sqlproj` or `project.json` file). Add a line in the following format:

```
AutoPublish=Server=localhost;Database=MyDevDatabase;Integrated Security=true;TrustServerCertificate=true
```

You can use any valid SQL Server connection string. The key must be `AutoPublish` (case-insensitive).

> **Security tip:** The `.env` file contains a connection string, so make sure to add it to `.gitignore` to avoid committing credentials to source control.

### Step 3: Save a programmability object

Open a `.sql` file in your project that contains a `CREATE PROCEDURE`, `CREATE VIEW`, `CREATE FUNCTION`, or `CREATE TRIGGER` statement, make a change, and save. The extension will automatically rewrite the statement to `CREATE OR ALTER` and execute it against the development database. Watch the status bar at the bottom of Visual Studio for the completion message.

## Example

Suppose you have a stored procedure in your project at `dbo/Stored Procedures/GetCustomerById.sql`:

```sql
CREATE PROCEDURE [dbo].[GetCustomerById]
    @Id INT
AS
BEGIN
    SELECT Id, Name, Email
    FROM dbo.Customers
    WHERE Id = @Id;
END
```

When you save this file with the feature enabled and a valid `.env` file present, the extension will execute the following against your development database:

```sql
CREATE OR ALTER PROCEDURE [dbo].[GetCustomerById]
    @Id INT
AS
BEGIN
    SELECT Id, Name, Email
    FROM dbo.Customers
    WHERE Id = @Id;
END
```

The stored procedure is updated immediately, without a full build and deploy cycle.

## Other features in SQL Project Power Tools

SQL Project Power Tools includes many other features to improve the SQL Database Project developer experience:

- **Templates** — Project and item templates for creating new SDK-style SQL Database Projects and common object types from File > New > Project and Add > New Item.
- **Import database** — Import the full schema of an existing database into your project, with configurable file layout (flat, by object type, by schema, or by schema and object type).
- **Schema compare** — Visually compare your database project with a live database and apply changes in either direction.
- **Analyze** — Run static code analysis on your project and view a detailed report of issues.
- **Manage code analysis rules** — Enable or disable individual static analysis rules and set their severity directly from a visual dialog, for SDK-style projects.
- **Create Mermaid E/R diagram** — Generate an Entity-Relationship diagram of selected tables for documentation.
- **.dacpac Solution Explorer node** — Browse the contents of a built `.dacpac` file directly from Solution Explorer.
- **Script Table Data** — Generate `INSERT` statements for table data to use as seed data in post-deployment scripts, based on [generate-sql-merge](https://github.com/dnlnln/generate-sql-merge).
- **Add pre- and post-deployment scripts** — Quickly add new pre- and post-deployment scripts to your project from the context menu.
- **Scaffold SQL MCP Server (preview)** — Generate a [SQL MCP Server](https://github.com/ErikEJ/SqlProjectPowerTools/blob/main/docs/dab-mcp-readme.md) configuration file based on your database project schema.

A [getting started guide](https://github.com/ErikEJ/SqlProjectPowerTools/blob/main/docs/getting-started.md) covers all of these features in more detail.

## Getting the extension

Install **SQL Project Power Tools** from the [Visual Studio Marketplace](https://marketplace.visualstudio.com/items?itemName=ErikEJ.SqlProjectPowerTools), or search for it directly in Visual Studio via **Extensions > Manage Extensions**.

The source code is open source and available on [GitHub](https://github.com/ErikEJ/SqlProjectPowerTools). If you find the extension useful, a ★★★★★ rating on the Marketplace is always appreciated. Bug reports and feature requests are welcome on the [GitHub issue tracker](https://github.com/ErikEJ/SqlProjectPowerTools/issues).
