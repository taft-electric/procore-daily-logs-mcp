using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ProcoreDailyLogsConsole.Configuration;
using ProcoreDailyLogsConsole.Services;

public class TestProcore
{
    public static async Task TestEndpoints()
    {
        // Build configuration
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .AddEnvironmentVariables()
            .Build();

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());
        services.Configure<ProcoreOptions>(options =>
        {
            configuration.GetSection(ProcoreOptions.SectionName).Bind(options);
            options.ClientId = Environment.GetEnvironmentVariable("PROCORE_CLIENT_ID") ?? options.ClientId;
            options.ClientSecret = Environment.GetEnvironmentVariable("PROCORE_CLIENT_SECRET") ?? options.ClientSecret;
        });
        
        services.AddHttpClient<IProcoreAuthService, ProcoreAuthService>();
        
        var serviceProvider = services.BuildServiceProvider();
        var authService = serviceProvider.GetRequiredService<IProcoreAuthService>();
        var logger = serviceProvider.GetRequiredService<ILogger<TestProcore>>();
        var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<ProcoreOptions>>().Value;
        
        try
        {
            var token = await authService.GetAccessTokenAsync();
            logger.LogInformation("Got token successfully");
            
            using var httpClient = new HttpClient { BaseAddress = new Uri(options.BaseUrl) };
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            httpClient.DefaultRequestHeaders.Add("Procore-Company-Id", options.CompanyId);
            
            // Test various endpoints
            var endpoints = new[]
            {
                // Correct format based on error message!
                $"/rest/v1.0/projects/{options.ProjectId}/daily_logs?filters[start_date]=2025-07-28&filters[end_date]=2025-07-28",
                $"/rest/v1.0/projects/{options.ProjectId}/daily_logs?filters[start_date]=2025-07-01&filters[end_date]=2025-07-28",
                
                // Try without filters prefix
                $"/rest/v1.0/projects/{options.ProjectId}/daily_logs?start_date=2025-07-28&end_date=2025-07-28",
                
                // Try with specific log date
                $"/rest/v1.0/projects/{options.ProjectId}/daily_logs/2025-07-28",
                
                // Original working endpoint for comparison
                $"/rest/v1.0/projects/{options.ProjectId}/daily_construction_report_logs",
            };
            
            foreach (var endpoint in endpoints)
            {
                try
                {
                    logger.LogInformation($"Testing endpoint: {endpoint}");
                    var response = await httpClient.GetAsync(endpoint);
                    logger.LogInformation($"  Status: {(int)response.StatusCode} {response.StatusCode}");
                    
                    if (response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync();
                        logger.LogInformation($"  Success! Content length: {content.Length}");
                        
                        // Pretty print first 500 chars
                        if (content.Length > 0)
                        {
                            try
                            {
                                var json = JsonDocument.Parse(content);
                                
                                // Special handling for projects list
                                if (endpoint.Contains("/projects") && !endpoint.Contains("/projects/"))
                                {
                                    logger.LogInformation($"  Checking if project {options.ProjectId} is in the list...");
                                    bool found = false;
                                    foreach (var project in json.RootElement.EnumerateArray())
                                    {
                                        var id = project.GetProperty("id").GetInt32();
                                        if (id.ToString() == options.ProjectId)
                                        {
                                            var name = project.GetProperty("name").GetString();
                                            logger.LogInformation($"  ✓ Found project: ID={id}, Name='{name}'");
                                            found = true;
                                            break;
                                        }
                                    }
                                    if (!found)
                                    {
                                        logger.LogWarning($"  ✗ Project {options.ProjectId} NOT found in list!");
                                    }
                                }
                                else
                                {
                                    var formatted = JsonSerializer.Serialize(json, new JsonSerializerOptions { WriteIndented = true });
                                    logger.LogInformation($"  Sample:\n{formatted.Substring(0, Math.Min(2000, formatted.Length))}...");
                                }
                            }
                            catch
                            {
                                logger.LogInformation($"  Raw: {content.Substring(0, Math.Min(200, content.Length))}...");
                            }
                        }
                    }
                    else
                    {
                        // Log error response body
                        var errorContent = await response.Content.ReadAsStringAsync();
                        logger.LogWarning($"  Error response: {errorContent.Substring(0, Math.Min(500, errorContent.Length))}");
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError($"  Error: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Test failed");
        }
    }
}