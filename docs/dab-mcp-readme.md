# Data API Builder (DAB) SQL MCP Server - Getting Started

This guide helps you set up and use Data API Builder's Model Context Protocol (MCP) server with SQL databases in Visual Studio.

## What is DAB SQL MCP?

Data API Builder (DAB) SQL MCP server enables AI-powered assistants like GitHub Copilot to interact with your SQL databases through a standardized interface. This allows you to:

- Query database schema and metadata
- Get table and column information
- Execute safe read-only queries
- Generate database-aware code and SQL statements

## Quick Start with SQL Database Project Power Tools

SQL Database Project Power Tools makes it easy to scaffold DAB configuration files directly from your database project:

### 1. Generate a DAB Configuration File

Right-click on your SQL database project in Solution Explorer and select **SQL Project Power Tools > Scaffold Data API Builder**:

1. Choose the tables you want to expose
2. A `dab-config.json` file and `dab-build.cmd` script will be generated in your project
3. The generated script includes commands to set up and run DAB with MCP support

### 2. Configure Your Connection String

The generated `dab-config.json` file uses an environment variable for the connection string:

```json
{
  "data-source": {
    "database-type": "mssql",
    "connection-string": "@env('dab-connection-string')"
  }
}
```

Create a `.env` file in your project directory with the following content:

```bash
dab-connection-string=Server=localhost;Database=MyDatabase;Integrated Security=true;
```

**Important:** Add `.env` to your `.gitignore` to avoid committing credentials to source control.

### 3. Install, Configure and Run DAB

The scaffolded `dab-build.cmd` script includes all necessary commands. Run it to:

1. Install the DAB CLI tool globally:
   ```bash
   dotnet tool install -g Microsoft.DataApiBuilder --prerelease
   ```

2. Initialize and configure DAB with your database entities

3. Start the Data API Builder server:
   ```bash
   dab start
   ```

By default, DAB will:

- Host REST API at: `http://localhost:5000/api`
- Host GraphQL endpoint at: `http://localhost:5000/graphql`
- Enable MCP endpoint at: `http://localhost:5000/mcp` (if configured)

### 4. Install DAB MCP Server in Visual Studio

Once DAB is running, configure Visual Studio to use it as an MCP server:

📦 **[Install DAB MCP Server](https://vs-open.link/mcp-install?%7B%22name%22%3A%22sql-mcp-server%22%2C%22type%22%3A%22http%22%2C%22url%22%3A%22http%3A//localhost%3A5000/mcp%22%7D)**

This will configure Visual Studio to use the Data API Builder MCP server for database interactions with AI assistants like GitHub Copilot.

Alternatively, you can manually add the MCP server configuration in Visual Studio settings `.mcp.json` file:

```json
{
    "sql-mcp-server": {
      "type": "http",
      "url": "http://localhost:5000/mcp"
    }
}
```

## Complete Workflow with SQL Database Project Power Tools

This integrated workflow makes database development seamless:

1. **Import your database** if you haven't already
2. **Scaffold DAB configuration** from the context menu
3. **Customize the generated config** to fit your needs
4. **Deploy your database** using the project
5. **Run DAB** with the generated configuration
6. **Configure MCP** in Visual Studio using the install link above

This workflow integrates database development, schema versioning (via SQL database projects), API generation, and AI-powered interactions seamlessly.

## DAB Configuration Example

Here's a minimal `dab-config.json` with MCP enabled:

```json
{
  "$schema": "https://github.com/Azure/data-api-builder/releases/download/v1.7.86/dab.draft.schema.json",
  "data-source": {
    "database-type": "mssql",
    "connection-string": "@env('dab-connection-string')"
  },
  "runtime": {
    "rest": {
      "enabled": true,
      "path": "/api"
    },
    "graphql": {
      "enabled": true,
      "path": "/graphql"
    },
    "mcp": {
      "enabled": true,
      "path": "/mcp"
    },
    "host": {
      "mode": "development"
    }
  },
  "entities": {
    "YourTable": {
      "source": {
        "object": "dbo.YourTable",
        "type": "table"
      },
      "permissions": [
        {
          "role": "anonymous",
          "actions": ["read"]
        }
      ]
    }
  }
}
```

**Note:** When using SQL Database Project Power Tools scaffolding, most of this configuration is generated automatically for you.

## Key Features

### REST API

Access your data via RESTful endpoints:

```text
GET http://localhost:5000/api/YourTable
GET http://localhost:5000/api/YourTable/id/123
```

### GraphQL API

Query data using GraphQL:

```graphql
query {
  yourTable {
    items {
      id
      name
    }
  }
}
```

### MCP Integration

Enable AI assistants to understand and query your database schema through the Model Context Protocol.

## Learn More

- [SQL Database Project Power Tools Documentation](getting-started.md)
- [Data API Builder Documentation](https://learn.microsoft.com/azure/data-api-builder/)
- [DAB MCP Quickstart VS Code](https://learn.microsoft.com/azure/data-api-builder/mcp/quickstart-visual-studio-code)
- [DAB GitHub Repository](https://github.com/Azure/data-api-builder)

## Troubleshooting

### Connection Issues

- Verify your connection string is correct
- Ensure the database server is accessible
- Check firewall settings

### DAB CLI Not Found

- Ensure .NET SDK is installed
- Add .NET tools path to your PATH environment variable
- Try reinstalling: `dotnet tool uninstall -g Microsoft.DataApiBuilder && dotnet tool install -g Microsoft.DataApiBuilder --prerelease`

### MCP Server Not Responding

- Verify MCP is enabled in `dab-config.json`
- Check that DAB is running (`dab start`)
- Review DAB logs for errors
- Ensure the `.env` file exists with your connection string

## Support

For issues related to:

- **SQL Database Project Power Tools**: [GitHub Issues](https://github.com/ErikEJ/SqlProjectPowerTools/issues)
- **Data API Builder**: [DAB GitHub Issues](https://github.com/Azure/data-api-builder/issues)
- **Visual Studio MCP**: [Visual Studio Feedback](https://developercommunity.visualstudio.com/)
