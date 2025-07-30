using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProcoreDailyLogsConsole.Configuration;
using ProcoreDailyLogsConsole.Models;

namespace ProcoreDailyLogsConsole.Services;

public interface IRateLimitService
{
    RateLimitInfo? CurrentStatus { get; }
    void UpdateFromHeaders(HttpResponseMessage response);
    Task WaitIfNeededAsync(CancellationToken cancellationToken = default);
    bool ShouldThrottle();
}

public class RateLimitService : IRateLimitService
{
    private readonly ProcoreOptions _options;
    private readonly ILogger<RateLimitService> _logger;
    private RateLimitInfo? _currentStatus;
    private readonly object _lock = new();

    public RateLimitService(IOptions<ProcoreOptions> options, ILogger<RateLimitService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public RateLimitInfo? CurrentStatus
    {
        get
        {
            lock (_lock)
            {
                return _currentStatus;
            }
        }
    }

    public void UpdateFromHeaders(HttpResponseMessage response)
    {
        lock (_lock)
        {
            if (response.Headers.TryGetValues("X-Rate-Limit-Limit", out var limitValues) &&
                response.Headers.TryGetValues("X-Rate-Limit-Remaining", out var remainingValues) &&
                response.Headers.TryGetValues("X-Rate-Limit-Reset", out var resetValues))
            {
                if (int.TryParse(limitValues.FirstOrDefault(), out var limit) &&
                    int.TryParse(remainingValues.FirstOrDefault(), out var remaining) &&
                    long.TryParse(resetValues.FirstOrDefault(), out var resetUnix))
                {
                    _currentStatus = new RateLimitInfo
                    {
                        Limit = limit,
                        Remaining = remaining,
                        ResetTime = DateTimeOffset.FromUnixTimeSeconds(resetUnix).UtcDateTime
                    };

                    _logger.LogDebug("Rate limit updated: {Remaining}/{Limit}, resets at {ResetTime}", 
                        remaining, limit, _currentStatus.ResetTime);

                    if (_currentStatus.IsApproachingLimit(_options.RateLimitBuffer))
                    {
                        _logger.LogWarning("Approaching rate limit: {Remaining} requests remaining", remaining);
                    }
                }
            }
        }
    }

    public async Task WaitIfNeededAsync(CancellationToken cancellationToken = default)
    {
        var status = CurrentStatus;
        if (status?.IsExceeded == true)
        {
            var waitTime = status.TimeUntilReset;
            if (waitTime > TimeSpan.Zero)
            {
                _logger.LogWarning("Rate limit exceeded. Waiting {WaitTime:mm\\:ss} until reset at {ResetTime}", 
                    waitTime, status.ResetTime);
                await Task.Delay(waitTime, cancellationToken);
            }
        }
    }

    public bool ShouldThrottle()
    {
        var status = CurrentStatus;
        return status?.IsApproachingLimit(_options.RateLimitBuffer) == true;
    }
}