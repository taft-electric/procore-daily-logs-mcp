namespace ProcoreDailyLogsConsole.Models;

public class RateLimitInfo
{
    public int Limit { get; set; }
    public int Remaining { get; set; }
    public DateTime ResetTime { get; set; }
    
    public bool IsApproachingLimit(int buffer = 100) => Remaining <= buffer;
    public bool IsExceeded => Remaining <= 0;
    public TimeSpan TimeUntilReset => ResetTime - DateTime.UtcNow;
}