namespace ProcoreDailyLogsConsole.Configuration;

public class CacheOptions
{
    public const string SectionName = "Cache";
    
    public bool Enabled { get; set; } = true;
    public int ImmutableDataThresholdDays { get; set; } = 7;
}