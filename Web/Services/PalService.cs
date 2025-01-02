using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Web.Configurations;
using Web.Utilities;

namespace Web.Services;

public class PalService
{
    private readonly HttpClient _httpClient;
    private readonly string _host;

    public PalService(HttpClient httpClient, IOptions<PalServiceOptions> options)
    {
        _httpClient = httpClient;
        _host = options.Value.Url;
        var authHeader = BasicAuthHelper.GetBasicAuthHeader(options.Value.Username, options.Value.Password);
        _httpClient.DefaultRequestHeaders.Authorization = authHeader;
    }

    private string GetUrl(string path) => $"{_host}/{path}";


    public async Task<PalServerInfo> GetServerInfoAsync()
    {
        return (await _httpClient.GetFromJsonAsync<PalServerInfo>(GetUrl("v1/api/info")))!;
    }

    public async Task<List<PlayerInfo>> GetPlayersAsync()
    {
        return (await _httpClient.GetFromJsonAsync<PlayerList>(GetUrl("v1/api/players")))!.Players;
    }

    public async Task KickPlayerAsync(string userId)
    {
        var resp = await _httpClient.PostAsync(GetUrl("v1/api/kick"), new StringContent(
            JsonSerializer.Serialize(
                new KickPlayerRequest
                {
                    UserId = userId,
                    Message = "Kicked by admin",
                }),
            Encoding.UTF8,
            "application/json")
        );
        if (!resp.IsSuccessStatusCode)
        {
            throw new Exception(await resp.Content.ReadAsStringAsync());
        }
    }

    public async Task BanPlayerAsync(string userId)
    {
        throw new NotImplementedException();
    }

    public async Task KillPlayerAsync(string userId)
    {
        throw new NotImplementedException();
    }

    public async Task Announce(string message)
    {
        var resp = await _httpClient.PostAsync(GetUrl("v1/api/announce"), new StringContent(
            JsonSerializer.Serialize(
                new AnnounceRequest
                {
                    Message = message,
                }),
            Encoding.UTF8,
            "application/json")
        );
        if (!resp.IsSuccessStatusCode)
        {
            throw new Exception(await resp.Content.ReadAsStringAsync());
        }
    }
}

public class PalServerInfo
{
    [JsonPropertyName("version")] public string Version { get; set; } = "";

    [JsonPropertyName("servername")] public string ServerName { get; set; } = "";

    [JsonPropertyName("description")] public string Description { get; set; } = "";

    [JsonPropertyName("worldguid")] public string WorldGuid { get; set; } = "";
}

public class PlayerList
{
    [JsonPropertyName("players")] public List<PlayerInfo> Players { get; set; } = [];
}

public class PlayerInfo
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";

    [JsonPropertyName("accountName")] public string AccountName { get; set; } = "";

    [JsonPropertyName("playerId")] public string PlayerId { get; set; } = "";

    [JsonPropertyName("userId")] public string UserId { get; set; } = "";

    [JsonPropertyName("ip")] public string Ip { get; set; } = "";

    [JsonPropertyName("ping")] public double Ping { get; set; }

    [JsonPropertyName("location_x")] public double LocationX { get; set; }

    [JsonPropertyName("location_y")] public double LocationY { get; set; }

    [JsonPropertyName("level")] public int Level { get; set; }

    [JsonPropertyName("building_count")] public int BuildingCount { get; set; }
}

public class KickPlayerRequest
{
    [JsonPropertyName("userid")] public string UserId { get; set; }

    [JsonPropertyName("message")] public string Message { get; set; }
}

public class AnnounceRequest
{
    [JsonPropertyName("message")] public string Message { get; set; }
}