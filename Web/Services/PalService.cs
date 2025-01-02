using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Web.Configurations;
using Web.Utilities;

namespace Web.Services;

public class PalService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<PalService> _logger;
    private readonly string _host;

    public PalService(HttpClient httpClient, IOptions<PalServiceOptions> options, ILogger<PalService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _host = options.Value.Url;
        var authHeader = BasicAuthHelper.GetBasicAuthHeader(options.Value.Username, options.Value.Password);
        _httpClient.DefaultRequestHeaders.Authorization = authHeader;
    }

    private string GetUrl(string path) => $"{_host}/{path}";


    public async Task<PalServerInfo?> GetServerInfoAsync()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<PalServerInfo>(GetUrl("v1/api/info"));
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to get server info");
            return null;
        }
    }
}

public class PalServerInfo
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = "";

    [JsonPropertyName("servername")]
    public string ServerName { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("worldguid")]
    public string WorldGuid { get; set; } = "";
}
