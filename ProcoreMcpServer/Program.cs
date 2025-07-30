using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using ModelContextProtocol;
using ProcoreMcpServer.Tools;
using ProcoreMcpServer.Configuration;
using ProcoreMcpServer.Services;

var builder = Host.CreateApplicationBuilder(args);

// Configure logging to stderr (stdout is reserved for MCP protocol messages)
builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});

// Add configuration
builder.Services.Configure<ProcoreOptions>(options =>
{
    builder.Configuration.GetSection(ProcoreOptions.SectionName).Bind(options);
    // Override with environment variables
    options.ClientId = Environment.GetEnvironmentVariable("PROCORE_CLIENT_ID") ?? options.ClientId;
    options.ClientSecret = Environment.GetEnvironmentVariable("PROCORE_CLIENT_SECRET") ?? options.ClientSecret;
});
builder.Services.Configure<CacheOptions>(builder.Configuration.GetSection(CacheOptions.SectionName));

// Add memory cache
builder.Services.AddMemoryCache();

// Add HTTP clients
builder.Services.AddHttpClient<IProcoreAuthService, ProcoreAuthService>();
builder.Services.AddHttpClient<IProcoreDailyLogsClient, ProcoreDailyLogsClient>();

// Add Procore services
builder.Services.AddSingleton<IRateLimitService, RateLimitService>();
builder.Services.AddSingleton<IDailyLogCacheService, DailyLogCacheService>();
builder.Services.AddTransient<IProcoreAuthService, ProcoreAuthService>();
builder.Services.AddTransient<IProcoreDailyLogsClient, ProcoreDailyLogsClient>();

// Add MCP server with STDIO transport
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<ProcoreDailyLogTools>();

var host = builder.Build();

// Log startup information
var logger = host.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("Procore MCP Server starting...");
logger.LogInformation("Server Name: procore-daily-logs");
logger.LogInformation("Transport: STDIO");

await host.RunAsync();