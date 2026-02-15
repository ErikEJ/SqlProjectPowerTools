# Data API Builder (DAB) SQL MCP Server - Getting Started

This guide helps you set up and use Data API Builder's Model Context Protocol (MCP) server with SQL databases in Visual Studio.

## What is DAB SQL MCP?

Data API Builder (DAB) SQL MCP server enables AI-powered assistants like GitHub Copilot to interact with your SQL databases through a standardized interface. This allows you to:

- Query database schema and metadata
- Get table and column information
- Execute safe read-only queries
- Generate database-aware code and SQL statements

## Installation

### Install DAB MCP Server in Visual Studio

To install the Data API Builder MCP server in Visual Studio, use the following link:

📦 **[Install DAB MCP Server](https://learn.microsoft.com/en-us/visualstudio/ide/mcp-servers?view=visualstudio&install=%7B%22mcpServers%22%3A%7B%22dab-mcp%22%3A%7B%22command%22%3A%22dab%22%2C%22args%22%3A%5B%22start%22%2C%22--no-https-redirect%22%5D%7D%7D%7D)**

This will configure Visual Studio to use the Data API Builder MCP server for database interactions.

Alternatively, you can manually add the MCP server configuration in Visual Studio settings:

```json
{
  "mcpServers": {
    "dab-mcp": {
      "command": "dab",
      "args": ["start", "--no-https-redirect"]
    }
  }
}
```

### DAB MCP Configuration

Refer to the [DAB MCP Quickstart documentation](https://learn.microsoft.com/en-us/azure/data-api-builder/mcp/quickstart-visual-studio-code#create-your-mcp-server-definition) for detailed configuration options.

## Getting Started with Data API Builder

### 1. Generate a DAB Configuration File

If you're using SQL Database Project Power Tools, you can scaffold a DAB configuration file directly from your database project:

1. Right-click on your SQL database project in Solution Explorer
2. Select **SQL Project Power Tools > Scaffold Data API Builder**
3. Choose the tables you want to expose
4. A `dab-config.json` file will be generated in your project

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

Set the connection string using a `.env` file (recommended approach used by the scaffolded script):

1. Create a `.env` file in your project directory with the following content:

```
dab-connection-string=Server=localhost;Database=MyDatabase;Integrated Security=true;
```

2. **Important:** Add `.env` to your `.gitignore` to avoid committing credentials to source control.

The Data API Builder will automatically load environment variables from the `.env` file when it starts.

### 3. Install Data API Builder CLI

Install the DAB CLI tool globally with the `--prerelease` flag (required for MCP support):

```bash
dotnet tool install -g Microsoft.DataApiBuilder --prerelease
```

Or update if already installed:

```bash
dotnet tool update -g Microsoft.DataApiBuilder --prerelease
```

### 4. Run DAB

Start the Data API Builder server:

```bash
dab start --no-https-redirect
```

The `--no-https-redirect` flag is used to match the Visual Studio MCP server configuration and simplifies local development.

By default, DAB will:
- Host REST API at: `http://localhost:5000/api`
- Host GraphQL endpoint at: `http://localhost:5000/graphql`
- Enable MCP endpoint at: `http://localhost:5000/mcp` (if configured)

### 5. Configure MCP in Visual Studio

Use the installation link at the top of this document to automatically configure the DAB MCP server in Visual Studio, or manually add the configuration to Visual Studio's MCP settings. Once configured, the server will be available for AI-powered database interactions.

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

## Using DAB with SQL Project Power Tools

SQL Database Project Power Tools can automatically scaffold DAB configuration files from your database project:

1. **Import your database** using Power Tools if you haven't already
2. **Scaffold DAB configuration** from the context menu
3. **Customize the generated config** to fit your needs
4. **Deploy your database** using the project
5. **Run DAB** with the generated configuration

This workflow integrates database development, versioning (via SQL projects), and API generation seamlessly.

## Key Features

### REST API
Access your data via RESTful endpoints:
```
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

- [Data API Builder Documentation](https://learn.microsoft.com/azure/data-api-builder/)
- [DAB GitHub Repository](https://github.com/Azure/data-api-builder)
- [SQL Project Power Tools Documentation](getting-started.md)

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
- Check that DAB is running (`dab start --no-https-redirect`)
- Review DAB logs for errors
- Ensure the `.env` file exists with your connection string

## Support

For issues related to:
- **SQL Project Power Tools**: [GitHub Issues](https://github.com/ErikEJ/SqlProjectPowerTools/issues)
- **Data API Builder**: [DAB GitHub Issues](https://github.com/Azure/data-api-builder/issues)
- **Visual Studio MCP**: [Visual Studio Feedback](https://developercommunity.visualstudio.com/)
