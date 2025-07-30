using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ProcoreDailyLogsConsole.Configuration;
using ProcoreDailyLogsConsole.Services;

// Build configuration
var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false)
    .AddEnvironmentVariables()
    .Build();

// Build service provider
var services = new ServiceCollection();

// Add logging
services.AddLogging(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Debug);
});

// Add configuration
services.Configure<ProcoreOptions>(options =>
{
    configuration.GetSection(ProcoreOptions.SectionName).Bind(options);
    // Override with environment variables
    options.ClientId = Environment.GetEnvironmentVariable("PROCORE_CLIENT_ID") ?? options.ClientId;
    options.ClientSecret = Environment.GetEnvironmentVariable("PROCORE_CLIENT_SECRET") ?? options.ClientSecret;
});
services.Configure<CacheOptions>(configuration.GetSection(CacheOptions.SectionName));

// Add memory cache
services.AddMemoryCache();

// Add HTTP client
services.AddHttpClient<IProcoreAuthService, ProcoreAuthService>();
services.AddHttpClient<IProcoreDailyLogsClient, ProcoreDailyLogsClient>();

// Add services
services.AddSingleton<IRateLimitService, RateLimitService>();
services.AddSingleton<IDailyLogCacheService, DailyLogCacheService>();
services.AddTransient<IProcoreAuthService, ProcoreAuthService>();
services.AddTransient<IProcoreDailyLogsClient, ProcoreDailyLogsClient>();

var serviceProvider = services.BuildServiceProvider();
var logger = serviceProvider.GetRequiredService<ILogger<Program>>();

// Check if we should run endpoint test
if (args.Length > 0)
{
    if (args[0] == "test")
    {
        Console.WriteLine("=== Testing Procore Endpoints ===");
        await TestProcore.TestEndpoints();
        Console.WriteLine("\n=== End Endpoint Test ===");
        return;
    }
    else if (args[0] == "find")
    {
        Console.WriteLine("=== Searching for Compton College Project ===");
        await FindProject.SearchForComptonProject();
        Console.WriteLine("\n=== End Search ===");
        return;
    }
    else if (args[0] == "quicktest")
    {
        Console.WriteLine("=== Quick Auth Test ===");
        await QuickTest.TestAuth();
        Console.WriteLine("\n=== End Quick Test ===");
        return;
    }
}

try
{
    // Get the Procore client
    var procoreClient = serviceProvider.GetRequiredService<IProcoreDailyLogsClient>();
    
    Console.WriteLine("=== Procore Daily Logs Console App ===");
    Console.WriteLine($"Company ID: {configuration["Procore:CompanyId"]}");
    Console.WriteLine($"Project ID: {configuration["Procore:ProjectId"]}");
    Console.WriteLine();

    // Test 1: Single day request
    Console.WriteLine("Test 1: Fetching single day (yesterday)...");
    var yesterday = DateOnly.FromDateTime(DateTime.Today.AddDays(-1));
    var singleDayResponse = await procoreClient.GetDailyLogsAsync(yesterday, yesterday);
    DisplayResponse(singleDayResponse);
    
    // Test 2: Week range with streaming
    Console.WriteLine("\nTest 2: Streaming last 7 days...");
    var endDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-1));
    var startDate = endDate.AddDays(-6);
    
    await foreach (var partialResponse in procoreClient.GetDailyLogsStreamAsync(startDate, endDate))
    {
        Console.WriteLine($"  Received partial response:");
        if (partialResponse.DailyLogs.Any())
        {
            Console.WriteLine($"    - {partialResponse.DailyLogs.Count} log(s)");
        }
        if (partialResponse.Errors.Any())
        {
            Console.WriteLine($"    - {partialResponse.Errors.Count} error(s)");
            foreach (var error in partialResponse.Errors)
            {
                Console.WriteLine($"      ERROR: {error.Date} - {error.Message}");
            }
        }
        if (partialResponse.Warnings.Any())
        {
            foreach (var warning in partialResponse.Warnings)
            {
                Console.WriteLine($"    - WARNING: {warning.Message}");
            }
        }
    }
    
    // Test 3: Month range to test caching
    Console.WriteLine("\nTest 3: Fetching last 30 days (tests caching)...");
    var monthEnd = DateOnly.FromDateTime(DateTime.Today.AddDays(-1));
    var monthStart = monthEnd.AddDays(-29);
    
    var sw = System.Diagnostics.Stopwatch.StartNew();
    var monthResponse = await procoreClient.GetDailyLogsAsync(monthStart, monthEnd);
    sw.Stop();
    
    Console.WriteLine($"First request took: {sw.ElapsedMilliseconds}ms");
    DisplayResponse(monthResponse);
    
    // Request again to test cache
    Console.WriteLine("\nRequesting same date range again (should use cache for older dates)...");
    sw.Restart();
    var cachedResponse = await procoreClient.GetDailyLogsAsync(monthStart, monthEnd);
    sw.Stop();
    
    Console.WriteLine($"Second request took: {sw.ElapsedMilliseconds}ms");
    Console.WriteLine($"Cached dates: {cachedResponse.Metadata.CachedDates}");
}
catch (Exception ex)
{
    logger.LogError(ex, "Application error");
    Console.WriteLine($"\nERROR: {ex.Message}");
    if (ex.InnerException != null)
    {
        Console.WriteLine($"Inner: {ex.InnerException.Message}");
    }
}

void DisplayResponse(ProcoreDailyLogsConsole.Models.DailyLogResponse response)
{
    Console.WriteLine($"\nResponse Summary:");
    Console.WriteLine($"  Requested: {response.Metadata.RequestedDates} dates");
    Console.WriteLine($"  Successful: {response.Metadata.SuccessfulDates} dates");
    Console.WriteLine($"  Failed: {response.Metadata.FailedDates} dates");
    Console.WriteLine($"  Cached: {response.Metadata.CachedDates} dates");
    
    if (response.DailyLogs.Any())
    {
        Console.WriteLine($"\n  Retrieved {response.DailyLogs.Count} daily log(s)");
        
        // Display first log as sample
        var firstLog = response.DailyLogs.First();
        Console.WriteLine("\n  Sample log structure:");
        var json = JsonSerializer.Serialize(firstLog, new JsonSerializerOptions { WriteIndented = true });
        
        // Truncate if too long
        if (json.Length > 500)
        {
            Console.WriteLine(json.Substring(0, 500) + "\n  ... (truncated)");
        }
        else
        {
            Console.WriteLine(json);
        }
    }
    
    if (response.Errors.Any())
    {
        Console.WriteLine($"\n  Errors ({response.Errors.Count}):");
        foreach (var error in response.Errors)
        {
            Console.WriteLine($"    - {error.Date}: {error.Message} - {error.Details}");
        }
    }
    
    if (response.Warnings.Any())
    {
        Console.WriteLine($"\n  Warnings:");
        foreach (var warning in response.Warnings)
        {
            Console.WriteLine($"    - {warning.Code}: {warning.Message}");
            if (warning.ResetTime.HasValue)
            {
                Console.WriteLine($"      Reset time: {warning.ResetTime}");
            }
        }
    }
}