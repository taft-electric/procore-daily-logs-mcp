using System.Text.Json;

namespace ProcoreDailyLogsConsole.Models;

public class DailyLogResponse
{
    public List<JsonElement> DailyLogs { get; set; } = new();
    public List<DailyLogError> Errors { get; set; } = new();
    public List<DailyLogWarning> Warnings { get; set; } = new();
    public DailyLogMetadata Metadata { get; set; } = new();
}

public class DailyLogError
{
    public string Date { get; set; } = "";
    public string Code { get; set; } = "";
    public string Message { get; set; } = "";
    public string Details { get; set; } = "";
}

public class DailyLogWarning
{
    public string Code { get; set; } = "";
    public string Message { get; set; } = "";
    public DateTime? ResetTime { get; set; }
}

public class DailyLogMetadata
{
    public int RequestedDates { get; set; }
    public int SuccessfulDates { get; set; }
    public int FailedDates { get; set; }
    public int CachedDates { get; set; }
}