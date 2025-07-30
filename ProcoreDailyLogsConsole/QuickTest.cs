using System.Net.Http;
using System.Text;
using System.Text.Json;

public class QuickTest
{
    public static async Task TestAuth()
    {
        // The credentials from the conversation history
        var clientId = "50e3cd7c88fc8c96e17174db0bcd68ed1bb08b69fb96b7a02efea6ebf9e7d0ac";
        var clientSecret = "f0b39e1a896b8fc7df4e652cce82d8ea27ab2f65f35b20cebd7090a019b6c6f2";
        
        using var httpClient = new HttpClient();
        
        var tokenRequest = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("grant_type", "client_credentials"),
            new KeyValuePair<string, string>("client_id", clientId),
            new KeyValuePair<string, string>("client_secret", clientSecret)
        });
        
        try
        {
            var response = await httpClient.PostAsync("https://api.procore.com/oauth/token", tokenRequest);
            var content = await response.Content.ReadAsStringAsync();
            
            Console.WriteLine($"Status: {(int)response.StatusCode} {response.StatusCode}");
            Console.WriteLine($"Response: {content}");
            
            if (response.IsSuccessStatusCode)
            {
                var tokenResponse = JsonDocument.Parse(content);
                var accessToken = tokenResponse.RootElement.GetProperty("access_token").GetString();
                Console.WriteLine($"\nGot access token: {accessToken?.Substring(0, 20)}...");
                
                // Try a simple API call
                httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
                httpClient.DefaultRequestHeaders.Add("Procore-Company-Id", "8038");
                
                var projectResponse = await httpClient.GetAsync("https://api.procore.com/rest/v1.0/companies/8038/projects/3041331");
                Console.WriteLine($"\nProject API call: {(int)projectResponse.StatusCode} {projectResponse.StatusCode}");
                if (projectResponse.IsSuccessStatusCode)
                {
                    var projectContent = await projectResponse.Content.ReadAsStringAsync();
                    var project = JsonDocument.Parse(projectContent);
                    Console.WriteLine($"Project Name: {project.RootElement.GetProperty("name").GetString()}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }
}