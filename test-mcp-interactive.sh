#!/bin/bash
# Interactive test for Procore MCP Server

export PROCORE_CLIENT_ID="aL520UinxnlSRWC2H7QvPA81c4l10R-PSkBwwOCRRXo"
export PROCORE_CLIENT_SECRET="67uOA5sM0gHlsNAcrIovjHIPZaMBMoqAulrUTWB9wSY"

echo "Starting Procore MCP Server in interactive mode..."
echo "You can now type JSON-RPC commands. Press Ctrl+C to exit."
echo ""
echo "Example commands:"
echo '{"jsonrpc":"2.0","method":"initialize","params":{"protocolVersion":"0.1.0","capabilities":{"roots":{}}},"id":1}'
echo '{"jsonrpc":"2.0","method":"tools/list","params":{},"id":2}'
echo '{"jsonrpc":"2.0","method":"tools/call","params":{"name":"test_connection","arguments":{}},"id":3}'
echo ""

cd /Users/jmarsh/Developer/csharp-sdk/ProcoreDailyLogsConsole
dotnet run --project ProcoreMcpServer