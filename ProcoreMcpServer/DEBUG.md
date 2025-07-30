# Debugging Procore MCP Server

## Overview
This MCP server provides read-only access to Procore Daily Construction Report Logs via the Model Context Protocol.

## Available Tools

1. **test_connection** - Test API connectivity
2. **get_daily_logs** - Get logs for a date range
3. **get_daily_log_for_date** - Get log for a specific date  
4. **stream_daily_logs** - Stream logs with progress reporting

## Debugging Methods

### Method 1: VS Code Debugging (Recommended)

1. Open VS Code in the ProcoreDailyLogsConsole directory
2. Select "MCP Server (STDIO)" from the debug dropdown
3. Press F5 to start debugging
4. The server will start in the integrated terminal
5. You can now send JSON-RPC messages to test

### Method 2: Manual Testing with Test Script

```bash
cd ProcoreMcpServer
./test-mcp-server.sh | dotnet run
```

This will send test messages to the server and show responses.

### Method 3: Add to Claude Code for Testing

1. Build the server:
   ```bash
   dotnet build ProcoreMcpServer/ProcoreMcpServer.csproj
   ```

2. Add to Claude Code:
   ```bash
   claude mcp add procore-logs "dotnet" "run" "--project" "/Users/jmarsh/Developer/csharp-sdk/ProcoreDailyLogsConsole/ProcoreMcpServer/ProcoreMcpServer.csproj"
   ```

3. Restart Claude Code to load the server

4. Test by asking Claude to use the Procore tools

### Method 4: Debug with Wrapper Script

Create a debug wrapper script:

```bash
#!/bin/bash
# debug-mcp-server.sh
export PROCORE_CLIENT_ID="your-client-id"
export PROCORE_CLIENT_SECRET="your-client-secret"

# For VS Code debugging, uncomment:
# dotnet run --project /path/to/ProcoreMcpServer -- --debugger-attach

# For normal run:
dotnet run --project /path/to/ProcoreMcpServer
```

Then add the wrapper to Claude Code:
```bash
claude mcp add procore-logs-debug "/path/to/debug-mcp-server.sh"
```

## Testing JSON-RPC Messages

### Initialize Connection
```json
{
  "jsonrpc": "2.0",
  "method": "initialize",
  "params": {
    "protocolVersion": "0.1.0",
    "capabilities": {
      "roots": {}
    }
  },
  "id": 1
}
```

### List Tools
```json
{
  "jsonrpc": "2.0",
  "method": "tools/list",
  "params": {},
  "id": 2
}
```

### Call a Tool
```json
{
  "jsonrpc": "2.0",
  "method": "tools/call",
  "params": {
    "name": "get_daily_log_for_date",
    "arguments": {
      "date": "2025-07-28"
    }
  },
  "id": 3
}
```

## Environment Variables

The server requires these environment variables:
- `PROCORE_CLIENT_ID` - Your Procore API client ID
- `PROCORE_CLIENT_SECRET` - Your Procore API client secret

These can be set in:
- VS Code launch.json (already configured)
- Shell environment
- .env file (not recommended for production)

## Troubleshooting

1. **Server doesn't start**: Check that all dependencies are restored
2. **Authentication fails**: Verify environment variables are set
3. **No output**: Remember that logs go to stderr, protocol messages to stdout
4. **Tools not found**: Ensure the tools class has `[McpServerToolType]` attribute

## Log Output

- All logs are sent to stderr
- Protocol messages are sent to stdout
- Set log level in appsettings.json or via environment