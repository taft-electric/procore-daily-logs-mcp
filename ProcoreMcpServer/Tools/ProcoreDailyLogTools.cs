using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using ProcoreMcpServer.Services;
using ProcoreMcpServer.Models;

namespace ProcoreMcpServer.Tools;

[McpServerToolType]
public class ProcoreDailyLogTools
{
    [McpServerTool, Description("Get daily construction logs from Procore for a specific date range")]
    public static async Task<ContentBlock> GetDailyLogs(
        string startDate,
        string endDate,
        IProcoreDailyLogsClient dailyLogsClient,
        ILogger<ProcoreDailyLogTools> logger,
        CancellationToken cancellationToken)
    {
        try
        {
            // Parse dates
            if (!DateOnly.TryParse(startDate, out var start))
            {
                throw new ArgumentException($"Invalid start date format: {startDate}. Use YYYY-MM-DD");
            }
            
            if (!DateOnly.TryParse(endDate, out var end))
            {
                throw new ArgumentException($"Invalid end date format: {endDate}. Use YYYY-MM-DD");
            }
            
            if (start > end)
            {
                throw new ArgumentException("Start date must be before or equal to end date");
            }
            
            logger.LogInformation("Fetching daily logs from {StartDate} to {EndDate}", start, end);
            
            // Get logs using the existing client
            var response = await dailyLogsClient.GetDailyLogsAsync(start, end, cancellationToken);
            
            // Prepare response
            var result = new
            {
                success = true,
                summary = new
                {
                    requested_dates = response.Metadata.RequestedDates,
                    successful_dates = response.Metadata.SuccessfulDates,
                    failed_dates = response.Metadata.FailedDates,
                    cached_dates = response.Metadata.CachedDates,
                    total_logs = response.DailyLogs.Count
                },
                daily_logs = response.DailyLogs,
                errors = response.Errors,
                warnings = response.Warnings
            };
            
            var json = JsonSerializer.Serialize(result, new JsonSerializerOptions 
            { 
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            
            return new TextContentBlock { Text = json };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error fetching daily logs");
            
            var errorResult = new
            {
                success = false,
                error = ex.Message,
                error_type = ex.GetType().Name
            };
            
            var json = JsonSerializer.Serialize(errorResult, new JsonSerializerOptions 
            { 
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            
            return new TextContentBlock { Text = json };
        }
    }
    
    [McpServerTool, Description("Test connection to Procore API and verify authentication")]
    public static async Task<ContentBlock> TestConnection(
        IProcoreAuthService authService,
        ILogger<ProcoreDailyLogTools> logger,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Testing Procore API connection");
            
            // Try to get an access token
            var token = await authService.GetAccessTokenAsync(cancellationToken);
            
            var result = new
            {
                success = true,
                message = "Successfully connected to Procore API",
                authenticated = true,
                token_length = token.Length
            };
            
            var json = JsonSerializer.Serialize(result, new JsonSerializerOptions 
            { 
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            
            return new TextContentBlock { Text = json };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error testing Procore connection");
            
            var errorResult = new
            {
                success = false,
                message = "Failed to connect to Procore API",
                authenticated = false,
                error = ex.Message,
                error_type = ex.GetType().Name
            };
            
            var json = JsonSerializer.Serialize(errorResult, new JsonSerializerOptions 
            { 
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            
            return new TextContentBlock { Text = json };
        }
    }
    
    [McpServerTool, Description("Get a single day's daily construction log from Procore")]
    public static async Task<ContentBlock> GetDailyLogForDate(
        string date,
        IProcoreDailyLogsClient dailyLogsClient,
        ILogger<ProcoreDailyLogTools> logger,
        CancellationToken cancellationToken)
    {
        // Reuse the GetDailyLogs method with same date for start and end
        return await GetDailyLogs(date, date, dailyLogsClient, logger, cancellationToken);
    }
    
    [McpServerTool, Description("Stream daily logs for a date range, returning results as they arrive")]
    public static async Task<ContentBlock> StreamDailyLogs(
        string startDate,
        string endDate,
        IProcoreDailyLogsClient dailyLogsClient,
        ILogger<ProcoreDailyLogTools> logger,
        IProgress<ProgressNotificationValue> progress,
        CancellationToken cancellationToken)
    {
        try
        {
            // Parse dates
            if (!DateOnly.TryParse(startDate, out var start))
            {
                throw new ArgumentException($"Invalid start date format: {startDate}. Use YYYY-MM-DD");
            }
            
            if (!DateOnly.TryParse(endDate, out var end))
            {
                throw new ArgumentException($"Invalid end date format: {endDate}. Use YYYY-MM-DD");
            }
            
            logger.LogInformation("Streaming daily logs from {StartDate} to {EndDate}", start, end);
            
            var allResponses = new List<DailyLogResponse>();
            var totalDays = (end.ToDateTime(TimeOnly.MinValue) - start.ToDateTime(TimeOnly.MinValue)).Days + 1;
            var processedDays = 0;
            
            await foreach (var partialResponse in dailyLogsClient.GetDailyLogsStreamAsync(start, end, cancellationToken))
            {
                allResponses.Add(partialResponse);
                processedDays++;
                
                // Report progress
                var progressValue = (float)processedDays / totalDays;
                progress.Report(new ProgressNotificationValue 
                { 
                    Progress = progressValue,
                    Total = totalDays
                });
            }
            
            // Aggregate results
            var aggregatedResponse = new DailyLogResponse();
            foreach (var response in allResponses)
            {
                aggregatedResponse.DailyLogs.AddRange(response.DailyLogs);
                aggregatedResponse.Errors.AddRange(response.Errors);
                aggregatedResponse.Warnings = response.Warnings; // Use latest warnings
                aggregatedResponse.Metadata.RequestedDates += response.Metadata.RequestedDates;
                aggregatedResponse.Metadata.SuccessfulDates += response.Metadata.SuccessfulDates;
                aggregatedResponse.Metadata.FailedDates += response.Metadata.FailedDates;
                aggregatedResponse.Metadata.CachedDates += response.Metadata.CachedDates;
            }
            
            var result = new
            {
                success = true,
                summary = new
                {
                    requested_dates = aggregatedResponse.Metadata.RequestedDates,
                    successful_dates = aggregatedResponse.Metadata.SuccessfulDates,
                    failed_dates = aggregatedResponse.Metadata.FailedDates,
                    cached_dates = aggregatedResponse.Metadata.CachedDates,
                    total_logs = aggregatedResponse.DailyLogs.Count
                },
                daily_logs = aggregatedResponse.DailyLogs,
                errors = aggregatedResponse.Errors,
                warnings = aggregatedResponse.Warnings
            };
            
            var json = JsonSerializer.Serialize(result, new JsonSerializerOptions 
            { 
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            
            return new TextContentBlock { Text = json };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error streaming daily logs");
            
            var errorResult = new
            {
                success = false,
                error = ex.Message,
                error_type = ex.GetType().Name
            };
            
            var json = JsonSerializer.Serialize(errorResult, new JsonSerializerOptions 
            { 
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            
            return new TextContentBlock { Text = json };
        }
    }
}