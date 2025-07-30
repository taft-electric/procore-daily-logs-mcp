using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProcoreMcpServer.Configuration;
using ProcoreMcpServer.Models;

namespace ProcoreMcpServer.Services;

public interface IProcoreDailyLogsClient
{
    IAsyncEnumerable<DailyLogResponse> GetDailyLogsStreamAsync(DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default);
    Task<DailyLogResponse> GetDailyLogsAsync(DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default);
}

public class ProcoreDailyLogsClient : IProcoreDailyLogsClient
{
    private readonly HttpClient _httpClient;
    private readonly IProcoreAuthService _authService;
    private readonly IRateLimitService _rateLimitService;
    private readonly IDailyLogCacheService _cacheService;
    private readonly ProcoreOptions _options;
    private readonly ILogger<ProcoreDailyLogsClient> _logger;

    public ProcoreDailyLogsClient(
        HttpClient httpClient,
        IProcoreAuthService authService,
        IRateLimitService rateLimitService,
        IDailyLogCacheService cacheService,
        IOptions<ProcoreOptions> options,
        ILogger<ProcoreDailyLogsClient> logger)
    {
        _httpClient = httpClient;
        _authService = authService;
        _rateLimitService = rateLimitService;
        _cacheService = cacheService;
        _options = options.Value;
        _logger = logger;
        
        _httpClient.BaseAddress = new Uri(_options.BaseUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(_options.RequestTimeoutSeconds);
    }

    public async IAsyncEnumerable<DailyLogResponse> GetDailyLogsStreamAsync(
        DateOnly startDate, 
        DateOnly endDate, 
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (startDate > endDate)
        {
            yield return new DailyLogResponse
            {
                Errors = [new DailyLogError 
                { 
                    Code = "INVALID_DATE_RANGE", 
                    Message = "Start date must be before or equal to end date" 
                }]
            };
            yield break;
        }

        var currentDate = startDate;
        var successfulDates = 0;
        var failedDates = 0;
        var cachedDates = 0;
        var allLogs = new List<JsonElement>();
        var allErrors = new List<DailyLogError>();
        var warnings = new List<DailyLogWarning>();

        while (currentDate <= endDate)
        {
            // Check cache first
            if (_cacheService.TryGetCachedLog(_options.ProjectId, currentDate, out var cachedLog) && cachedLog.HasValue)
            {
                cachedDates++;
                successfulDates++;
                allLogs.Add(cachedLog.Value);
                
                // Yield intermediate results
                yield return new DailyLogResponse
                {
                    DailyLogs = [cachedLog.Value],
                    Metadata = new DailyLogMetadata
                    {
                        RequestedDates = 1,
                        SuccessfulDates = 1,
                        CachedDates = 1
                    }
                };
                
                currentDate = currentDate.AddDays(1);
                continue;
            }

            // Check rate limits
            await _rateLimitService.WaitIfNeededAsync(cancellationToken);
            
            // Add warning if approaching limit
            if (_rateLimitService.ShouldThrottle())
            {
                var status = _rateLimitService.CurrentStatus;
                if (status != null)
                {
                    warnings.Add(new DailyLogWarning
                    {
                        Code = "RATE_LIMIT_APPROACHING",
                        Message = $"Approaching rate limit: {status.Remaining} requests remaining",
                        ResetTime = status.ResetTime
                    });
                }
            }

            DailyLogResponse? intermediateResponse = null;
            try
            {
                var log = await FetchDailyLogAsync(currentDate, cancellationToken);
                if (log.HasValue)
                {
                    successfulDates++;
                    allLogs.Add(log.Value);
                    
                    // Cache if appropriate
                    _cacheService.CacheLog(_options.ProjectId, currentDate, log.Value);
                    
                    // Prepare intermediate results
                    intermediateResponse = new DailyLogResponse
                    {
                        DailyLogs = [log.Value],
                        Warnings = warnings.ToList(),
                        Metadata = new DailyLogMetadata
                        {
                            RequestedDates = 1,
                            SuccessfulDates = 1
                        }
                    };
                }
            }
            catch (Exception ex)
            {
                failedDates++;
                var error = new DailyLogError
                {
                    Date = currentDate.ToString("yyyy-MM-dd"),
                    Code = "PROCORE_API_ERROR",
                    Message = "Failed to retrieve log for this date",
                    Details = ex.Message
                };
                allErrors.Add(error);
                
                // Prepare error response
                intermediateResponse = new DailyLogResponse
                {
                    Errors = [error],
                    Warnings = warnings.ToList(),
                    Metadata = new DailyLogMetadata
                    {
                        RequestedDates = 1,
                        FailedDates = 1
                    }
                };
            }
            
            // Yield outside of try-catch
            if (intermediateResponse != null)
            {
                yield return intermediateResponse;
            }

            currentDate = currentDate.AddDays(1);
        }
    }

    public async Task<DailyLogResponse> GetDailyLogsAsync(DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default)
    {
        var response = new DailyLogResponse();
        var totalRequested = 0;
        
        await foreach (var partialResponse in GetDailyLogsStreamAsync(startDate, endDate, cancellationToken))
        {
            response.DailyLogs.AddRange(partialResponse.DailyLogs);
            response.Errors.AddRange(partialResponse.Errors);
            
            // Only keep the latest warnings
            if (partialResponse.Warnings.Any())
            {
                response.Warnings = partialResponse.Warnings;
            }
            
            totalRequested += partialResponse.Metadata.RequestedDates;
            response.Metadata.SuccessfulDates += partialResponse.Metadata.SuccessfulDates;
            response.Metadata.FailedDates += partialResponse.Metadata.FailedDates;
            response.Metadata.CachedDates += partialResponse.Metadata.CachedDates;
        }
        
        response.Metadata.RequestedDates = totalRequested;
        return response;
    }

    private async Task<JsonElement?> FetchDailyLogAsync(DateOnly date, CancellationToken cancellationToken)
    {
        var token = await _authService.GetAccessTokenAsync(cancellationToken);
        
        var retryCount = 0;
        while (retryCount < _options.MaxRetryAttempts)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, 
                    $"/rest/v1.0/projects/{_options.ProjectId}/daily_logs");
                
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                request.Headers.Add("Procore-Company-Id", _options.CompanyId);
                
                // Use correct filter format for daily_logs endpoint
                var queryParams = $"?filters[start_date]={date:yyyy-MM-dd}&filters[end_date]={date:yyyy-MM-dd}";
                request.RequestUri = new Uri(request.RequestUri + queryParams, UriKind.Relative);
                
                var response = await _httpClient.SendAsync(request, cancellationToken);
                
                // Update rate limit info
                _rateLimitService.UpdateFromHeaders(response);
                
                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    await _rateLimitService.WaitIfNeededAsync(cancellationToken);
                    retryCount++;
                    continue;
                }
                
                response.EnsureSuccessStatusCode();
                
                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                var doc = JsonDocument.Parse(json);
                
                // Return the entire response for the date (includes weather_logs and other log types)
                return doc.RootElement;
            }
            catch (HttpRequestException ex) when (retryCount < _options.MaxRetryAttempts - 1)
            {
                _logger.LogWarning("Request failed, retrying... Attempt {Attempt}/{Max}: {Message}", 
                    retryCount + 1, _options.MaxRetryAttempts, ex.Message);
                retryCount++;
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, retryCount)), cancellationToken);
            }
        }
        
        throw new InvalidOperationException($"Failed to fetch daily log after {_options.MaxRetryAttempts} attempts");
    }
}