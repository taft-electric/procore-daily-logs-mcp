using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProcoreDailyLogsConsole.Configuration;

namespace ProcoreDailyLogsConsole.Services;

public interface IDailyLogCacheService
{
    bool TryGetCachedLog(string projectId, DateOnly date, out JsonElement? log);
    void CacheLog(string projectId, DateOnly date, JsonElement log);
    bool ShouldCache(DateOnly date);
}

public class DailyLogCacheService : IDailyLogCacheService
{
    private readonly IMemoryCache _cache;
    private readonly CacheOptions _options;
    private readonly ILogger<DailyLogCacheService> _logger;

    public DailyLogCacheService(IMemoryCache cache, IOptions<CacheOptions> options, ILogger<DailyLogCacheService> logger)
    {
        _cache = cache;
        _options = options.Value;
        _logger = logger;
    }

    public bool TryGetCachedLog(string projectId, DateOnly date, out JsonElement? log)
    {
        if (!_options.Enabled)
        {
            log = null;
            return false;
        }

        var key = GetCacheKey(projectId, date);
        if (_cache.TryGetValue(key, out JsonElement cachedLog))
        {
            log = cachedLog;
            _logger.LogDebug("Cache hit for daily log: {ProjectId} - {Date}", projectId, date);
            return true;
        }

        log = null;
        return false;
    }

    public void CacheLog(string projectId, DateOnly date, JsonElement log)
    {
        if (!_options.Enabled || !ShouldCache(date))
        {
            return;
        }

        var key = GetCacheKey(projectId, date);
        
        // Permanent cache for immutable data (older than threshold)
        _cache.Set(key, log);
        _logger.LogDebug("Cached daily log: {ProjectId} - {Date}", projectId, date);
    }

    public bool ShouldCache(DateOnly date)
    {
        if (!_options.Enabled) return false;
        
        var threshold = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-_options.ImmutableDataThresholdDays));
        return date < threshold;
    }

    private string GetCacheKey(string projectId, DateOnly date)
    {
        return $"procore_daily_log_{projectId}_{date:yyyy-MM-dd}";
    }
}