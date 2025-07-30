using System.Text.Json.Serialization;

namespace ProcoreDailyLogsConsole.Models;

public class ProcoreTokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = "";
    
    [JsonPropertyName("token_type")]
    public string TokenType { get; set; } = "";
    
    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }
    
    [JsonPropertyName("created_at")]
    public long CreatedAt { get; set; }
    
    public DateTime ExpiresAt => DateTimeOffset.FromUnixTimeSeconds(CreatedAt).DateTime.AddSeconds(ExpiresIn);
}