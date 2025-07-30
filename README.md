# Procore Daily Logs MCP Server

A C# implementation of a Model Context Protocol (MCP) server that provides read-only access to Procore Daily Construction Report Logs.

## Phase 1: Console Application (Current)

This console application demonstrates the core functionality for accessing Procore's API to retrieve daily construction logs.

### Features

- ✅ OAuth 2.0 authentication with Procore API
- ✅ Retrieve daily construction logs for specific projects
- ✅ Intelligent caching (permanent for logs >7 days old)
- ✅ Rate limiting with proactive throttling
- ✅ Streaming results with partial data on errors
- ✅ Error handling and retry logic

### Configuration

The application requires the following environment variables:
- `PROCORE_CLIENT_ID`: Your Procore API client ID
- `PROCORE_CLIENT_SECRET`: Your Procore API client secret

Additional settings in `appsettings.json`:
- `CompanyId`: Your Procore company ID
- `ProjectId`: The project ID to retrieve logs from

### Usage

```bash
# Run the main application
dotnet run

# Test endpoints
dotnet run test

# Find projects
dotnet run find
```

### API Endpoint

The application uses the Procore daily logs endpoint:
```
GET /rest/v1.0/projects/{projectId}/daily_logs?filters[start_date]={date}&filters[end_date]={date}
```

## Phase 2: MCP Server (Coming Soon)

The next phase will convert this console application into a full MCP server that can be integrated with LLM applications.

### Planned MCP Tools

1. **procore_get_daily_logs**
   - Retrieve daily logs for a date range
   - Support filtering and pagination
   - Return structured log data

2. **procore_search_logs**
   - Search logs by content, type, or metadata
   - Support complex query filters

3. **procore_get_log_summary**
   - Generate summaries of logs for specific periods
   - Aggregate data across multiple days

## Development

### Prerequisites

- .NET 9.0 SDK
- Procore API credentials
- Access to target Procore projects

### Building

```bash
dotnet build
```

### Running in VS Code

Press F5 to run with debugging. The launch configuration includes the necessary environment variables.

## Security

- This tool provides READ-ONLY access to Procore data
- API credentials should never be committed to the repository
- Use environment variables or secure credential storage
- All data is cached locally following Procore's data immutability rules

## License

[Your License Here]