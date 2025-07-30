using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProcoreMcpServer.Configuration;
using ProcoreMcpServer.Models;

namespace ProcoreMcpServer.Services;

public interface IProcoreAuthService
{
    Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default);
}

public class ProcoreAuthService : IProcoreAuthService
{
    private readonly HttpClient _httpClient;
    private readonly ProcoreOptions _options;
    private readonly ILogger<ProcoreAuthService> _logger;
    private ProcoreTokenResponse? _cachedToken;
    private readonly SemaphoreSlim _tokenSemaphore = new(1, 1);

    public ProcoreAuthService(HttpClient httpClient, IOptions<ProcoreOptions> options, ILogger<ProcoreAuthService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        await _tokenSemaphore.WaitAsync(cancellationToken);
        try
        {
            // Check if we have a valid cached token
            if (_cachedToken != null && _cachedToken.ExpiresAt > DateTime.UtcNow.AddMinutes(5))
            {
                _logger.LogDebug("Using cached access token");
                return _cachedToken.AccessToken;
            }

            _logger.LogInformation("Requesting new access token from Procore");
            return await RequestNewTokenAsync(cancellationToken);
        }
        finally
        {
            _tokenSemaphore.Release();
        }
    }

    private async Task<string> RequestNewTokenAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_options.ClientId))
            throw new InvalidOperationException("ClientId not configured");
        if (string.IsNullOrEmpty(_options.ClientSecret))
            throw new InvalidOperationException("ClientSecret not configured");
            
        _logger.LogDebug("Auth URL: {AuthUrl}", _options.AuthUrl);
        _logger.LogDebug("Client ID length: {Length}", _options.ClientId.Length);
        
        var tokenRequest = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("grant_type", "client_credentials"),
            new KeyValuePair<string, string>("client_id", _options.ClientId),
            new KeyValuePair<string, string>("client_secret", _options.ClientSecret)
        });

        var response = await _httpClient.PostAsync(_options.AuthUrl, tokenRequest, cancellationToken);
        
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Failed to obtain access token: {StatusCode} - {Error}", response.StatusCode, error);
            throw new InvalidOperationException($"Failed to obtain access token: {response.StatusCode}");
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        _cachedToken = JsonSerializer.Deserialize<ProcoreTokenResponse>(json) 
            ?? throw new InvalidOperationException("Invalid token response");
        
        _logger.LogInformation("Successfully obtained new access token, expires at {ExpiresAt}", _cachedToken.ExpiresAt);
        return _cachedToken.AccessToken;
    }
}