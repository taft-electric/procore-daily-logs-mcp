#!/bin/bash

# Test script for Procore MCP Server
# This script helps test the MCP server by sending JSON-RPC messages via STDIO

echo "=== Procore MCP Server Test Script ===" >&2
echo "Starting MCP server..." >&2

# Export credentials
export PROCORE_CLIENT_ID="aL520UinxnlSRWC2H7QvPA81c4l10R-PSkBwwOCRRXo"
export PROCORE_CLIENT_SECRET="67uOA5sM0gHlsNAcrIovjHIPZaMBMoqAulrUTWB9wSY"

# Function to send a JSON-RPC request
send_request() {
    local request=$1
    echo "$request"
}

# Initialize the connection
send_request '{"jsonrpc":"2.0","method":"initialize","params":{"protocolVersion":"0.1.0","capabilities":{"roots":{}}},"id":1}'

# Wait for response
sleep 2

# List available tools
send_request '{"jsonrpc":"2.0","method":"tools/list","params":{},"id":2}'

# Wait for response
sleep 2

# Test connection tool
send_request '{"jsonrpc":"2.0","method":"tools/call","params":{"name":"test_connection","arguments":{}},"id":3}'

# Wait for response
sleep 2

# Get daily logs for a specific date
send_request '{"jsonrpc":"2.0","method":"tools/call","params":{"name":"get_daily_log_for_date","arguments":{"date":"2025-07-28"}},"id":4}'

# Keep the script running to see responses
sleep 5