using Microsoft.Extensions.Configuration;

namespace ProcoreMcpServer.Configuration;

public class ProcoreOptions
{
    public const string SectionName = "Procore";
    
    public string BaseUrl { get; set; } = "https://api.procore.com";
    public string AuthUrl { get; set; } = "https://api.procore.com/oauth/token";
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public string CompanyId { get; set; } = "";
    public string ProjectId { get; set; } = "";
    public int RateLimitBuffer { get; set; } = 100;
    public int MaxRetryAttempts { get; set; } = 3;
    public int RequestTimeoutSeconds { get; set; } = 30;
    
    public void Configure(IConfiguration configuration)
    {
        // Try to get from environment variables first
        ClientId = Environment.GetEnvironmentVariable("PROCORE_CLIENT_ID") ?? ClientId;
        ClientSecret = Environment.GetEnvironmentVariable("PROCORE_CLIENT_SECRET") ?? ClientSecret;
    }
}