using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ProcoreDailyLogsConsole.Configuration;
using ProcoreDailyLogsConsole.Services;

public class FindProject
{
    public static async Task SearchForComptonProject()
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
        var logger = serviceProvider.GetRequiredService<ILogger<FindProject>>();
        var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<ProcoreOptions>>().Value;
        
        try
        {
            var token = await authService.GetAccessTokenAsync();
            
            using var httpClient = new HttpClient { BaseAddress = new Uri(options.BaseUrl) };
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            httpClient.DefaultRequestHeaders.Add("Procore-Company-Id", options.CompanyId);
            
            // Get all projects
            var response = await httpClient.GetAsync($"/rest/v1.0/companies/{options.CompanyId}/projects");
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(content);
                
                logger.LogInformation($"Total projects: {doc.RootElement.GetArrayLength()}");
                
                // Search for Compton
                foreach (var project in doc.RootElement.EnumerateArray())
                {
                    var id = project.GetProperty("id").GetInt32();
                    var name = project.GetProperty("name").GetString() ?? "";
                    
                    if (name.Contains("COMPTON", StringComparison.OrdinalIgnoreCase) ||
                        name.Contains("COLLEGE", StringComparison.OrdinalIgnoreCase))
                    {
                        logger.LogInformation($"Found possible match: ID={id}, Name='{name}'");
                        
                        // Print full project details
                        var formatted = JsonSerializer.Serialize(project, new JsonSerializerOptions { WriteIndented = true });
                        logger.LogInformation($"Details:\n{formatted}");
                    }
                }
                
                // Also search by the provided ID
                logger.LogInformation($"\nSearching for ID {options.ProjectId}...");
                foreach (var project in doc.RootElement.EnumerateArray())
                {
                    var id = project.GetProperty("id").GetInt32();
                    if (id.ToString() == options.ProjectId)
                    {
                        var name = project.GetProperty("name").GetString() ?? "";
                        logger.LogInformation($"Found project with ID {id}: '{name}'");
                        break;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Search failed");
        }
    }
}