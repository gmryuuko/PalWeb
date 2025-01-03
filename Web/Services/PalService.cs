using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Web.Configurations;
using Web.Utilities;

namespace Web.Services;

public class PalService : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _host;
    private readonly string _configPath;
    private readonly string _dockerComposeDirectory;
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly ILogger<PalService> _logger;

    public PalServerInfo PalServerInfo { get; private set; } = new();
    public PalServerMetrics PalServerMetrics { get; private set; } = new();
    public List<PlayerInfo> Players { get; private set; } = [];

    private int _onChangeServerInfoSubscriberCount = 0;
    private Action? _onChangeServerInfo;

    public event Action? OnChangeServerInfo
    {
        add
        {
            _onChangeServerInfoSubscriberCount++;
            _onChangeServerInfo += value;
        }
        remove
        {
            _onChangeServerInfoSubscriberCount--;
            _onChangeServerInfo -= value;
        }
    }

    private int _onChangePlayersSubscriberCount = 0;
    private Action? _onChangePlayers;

    public event Action? OnChangePlayers
    {
        add
        {
            _onChangePlayersSubscriberCount++;
            _onChangePlayers += value;
        }
        remove
        {
            _onChangePlayersSubscriberCount--;
            _onChangePlayers -= value;
        }
    }

    private void NotifyServerInfoChanged() => _onChangeServerInfo?.Invoke();
    private void NotifyPlayersChanged() => _onChangePlayers?.Invoke();

    public PalService(HttpClient httpClient, IOptions<PalServiceOptions> options, ILogger<PalService> logger)
    {
        Console.WriteLine("PalService created");
        _httpClient = httpClient;
        _host = options.Value.Url;
        var authHeader = BasicAuthHelper.GetBasicAuthHeader(options.Value.Username, options.Value.Password);
        _httpClient.DefaultRequestHeaders.Authorization = authHeader;
        _configPath = Path.Join(options.Value.Root, "Saved/Config/LinuxServer/PalWorldSettings.ini");
        _dockerComposeDirectory = options.Value.Root;
        _logger = logger;

        Task.Run(() => RefreshAsync(_cancellationTokenSource.Token));
    }

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
        GC.SuppressFinalize(this);
    }

    private string GetUrl(string path) => $"{_host}/{path}";

    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            if (_onChangeServerInfoSubscriberCount > 0)
            {
                try
                {
                    var newPalServerInfo = await GetServerInfoAsync();
                    var newPalServerMetrics = await GetServerMetricsAsync();
                    if (PalServerInfo != newPalServerInfo || PalServerMetrics != newPalServerMetrics)
                    {
                        PalServerInfo = newPalServerInfo;
                        PalServerMetrics = newPalServerMetrics;
                        NotifyServerInfoChanged();
                    }

                    _logger.LogInformation("Server info and metrics updated");
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Failed to get server info or metrics");
                }
            }

            if (_onChangePlayersSubscriberCount > 0)
            {
                try
                {
                    var newPlayers = await GetPlayersAsync();
                    if (!Players.SequenceEqual(newPlayers))
                    {
                        Players = newPlayers;
                        NotifyPlayersChanged();
                    }

                    _logger.LogInformation("Players updated");
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Failed to get players");
                }
            }

            await Task.Delay(1000, cancellationToken);
        }
    }

    private async Task<PalServerInfo> GetServerInfoAsync()
    {
        return (await _httpClient.GetFromJsonAsync<PalServerInfo>(GetUrl("v1/api/info")))!;
    }

    private async Task<PalServerMetrics> GetServerMetricsAsync()
    {
        return (await _httpClient.GetFromJsonAsync<PalServerMetrics>(GetUrl("v1/api/metrics")))!;
    }

    private async Task<List<PlayerInfo>> GetPlayersAsync()
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

    public async Task<PalWorldSettings> GetActivePalWorldSettingsAsync()
    {
        return (await _httpClient.GetFromJsonAsync<PalWorldSettings>(GetUrl("v1/api/settings")))!;
    }

    public async Task<PalWorldSettings> GetPalWorldSettingsAsync()
    {
        var ini = await File.ReadAllTextAsync(_configPath);
        var iniFile = PeanutButter.INI.INIFile.FromString(ini);
        var section = iniFile.GetSection("/Script/Pal.PalGameWorldSettings");
        var settingsStr = section["OptionSettings"]!;
        var settings = settingsStr.TrimStart('(').TrimEnd(')').Split(',').Select(
            setting =>
            {
                var kv = setting.Split('=');
                return (kv[0], kv[1]);
            }).ToList();

        var palWorldSettings = new PalWorldSettings();
        var palWorldSettingsType = typeof(PalWorldSettings);
        foreach (var (key, value) in settings)
        {
            var prop = palWorldSettingsType.GetProperty(key);
            if (prop == null)
            {
                continue;
            }

            if (prop.PropertyType == typeof(string))
            {
                var trimmedValue = value.Trim('"');
                prop.SetValue(palWorldSettings, trimmedValue);
            }
            else if (prop.PropertyType == typeof(double))
            {
                prop.SetValue(palWorldSettings, double.Parse(value));
            }
            else if (prop.PropertyType == typeof(int))
            {
                prop.SetValue(palWorldSettings, int.Parse(value));
            }
            else if (prop.PropertyType == typeof(bool))
            {
                prop.SetValue(palWorldSettings, bool.Parse(value));
            }
        }

        return palWorldSettings;
    }

    public async Task SetPalWorldSettingsAsync(PalWorldSettings settings)
    {
        var ini = settings.ToIni();
        await File.WriteAllTextAsync(_configPath, ini);
    }

    public async Task RestartServer()
    {
        // run docker compose down
        var dockerComposeDown = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "docker-compose",
                Arguments = "down",
                WorkingDirectory = _dockerComposeDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            }
        };
        dockerComposeDown.Start();
        await dockerComposeDown.WaitForExitAsync();

        // run docker compose up -d
        var dockerComposeUp = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "docker-compose",
                Arguments = "up -d",
                WorkingDirectory = _dockerComposeDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            }
        };
        dockerComposeUp.Start();
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

public class PalServerMetrics
{
    [JsonPropertyName("currentplayernum")]
    public int CurrentPlayerNum { get; set; }

    [JsonPropertyName("serverfps")]
    public double ServerFps => 1000.0 / ServerFrameTime;

    [JsonPropertyName("serverframetime")]
    public double ServerFrameTime { get; set; }

    [JsonPropertyName("days")]
    public int Days { get; set; }

    [JsonPropertyName("maxplayernum")]
    public int MaxPlayerNum { get; set; }

    [JsonPropertyName("uptime")]
    public int Uptime { get; set; }
}

public class PlayerList
{
    [JsonPropertyName("players")]
    public List<PlayerInfo> Players { get; set; } = [];
}

public class PlayerInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("accountName")]
    public string AccountName { get; set; } = "";

    [JsonPropertyName("playerId")]
    public string PlayerId { get; set; } = "";

    [JsonPropertyName("userId")]
    public string UserId { get; set; } = "";

    [JsonPropertyName("ip")]
    public string Ip { get; set; } = "";

    [JsonPropertyName("ping")]
    public double Ping { get; set; }

    [JsonPropertyName("location_x")]
    public double LocationX { get; set; }

    [JsonPropertyName("location_y")]
    public double LocationY { get; set; }

    [JsonPropertyName("level")]
    public int Level { get; set; }

    [JsonPropertyName("building_count")]
    public int BuildingCount { get; set; }
}

public class KickPlayerRequest
{
    [JsonPropertyName("userid")]
    public string UserId { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; }
}

public class AnnounceRequest
{
    [JsonPropertyName("message")]
    public string Message { get; set; }
}

public class PalWorldSettings
{
    #region 基本

    public string ServerName { get; set; } = "Default Palworld Server";
    public string ServerDescription { get; set; } = "";
    public int ServerPlayerMaxNum { get; set; } = 32;
    public int PublicPort { get; set; } = 8211;
    public string PublicIP { get; set; } = "";
    public int ChatPostLimitPerMinute { get; set; } = 10;
    public bool bShowPlayerList { get; set; } = false;

    #endregion

    #region 世界

    public double DayTimeSpeedRate { get; set; } = 1;
    public double NightTimeSpeedRate { get; set; } = 1;
    public double ExpRate { get; set; } = 1;

    public double EnemyDropItemRate { get; set; } = 1;
    public int DropItemMaxNum { get; set; } = 3000;
    public int BaseCampMaxNum { get; set; } = 128;
    public int BaseCampWorkerMaxNum { get; set; } = 15;
    public double DropItemAliveMaxHours { get; set; } = 1;
    public int SupplyDropSpan { get; set; } = 180;
    public double ItemWeightRate { get; set; } = 1;
    public bool bEnableInvaderEnemy { get; set; } = true;

    #endregion

    #region 采集物

    public double CollectionDropRate { get; set; } = 1;
    public double CollectionObjectHpRate { get; set; } = 1;
    public double CollectionObjectRespawnSpeedRate { get; set; } = 1;

    #endregion

    #region 帕鲁

    public double PalCaptureRate { get; set; } = 1;
    public double PalSpawnNumRate { get; set; } = 1;
    public double PalDamageRateAttack { get; set; } = 1;
    public double PalDamageRateDefense { get; set; } = 1;
    public double PalStomachDecreaceRate { get; set; } = 1;
    public double PalStaminaDecreaceRate { get; set; } = 1;
    public double PalAutoHPRegeneRate { get; set; } = 1;
    public double PalAutoHpRegeneRateInSleep { get; set; } = 1;
    public double PalEggDefaultHatchingTime { get; set; } = 72;

    #endregion

    #region 玩家

    public double PlayerDamageRateAttack { get; set; } = 1;
    public double PlayerDamageRateDefense { get; set; } = 1;
    public double PlayerStomachDecreaceRate { get; set; } = 1;
    public double PlayerStaminaDecreaceRate { get; set; } = 1;
    public double PlayerAutoHPRegeneRate { get; set; } = 1;
    public double PlayerAutoHpRegeneRateInSleep { get; set; } = 1;

    #endregion

    #region 建筑

    public double BuildObjectHpRate { get; set; } = 1;
    public double BuildObjectDamageRate { get; set; } = 1;
    public double BuildObjectDeteriorationDamageRate { get; set; } = 1;
    public int MaxBuildingLimitNum { get; set; } = 0;

    #endregion

    #region 工会

    public int GuildPlayerMaxNum { get; set; } = 20;
    public int BaseCampMaxNumInGuild { get; set; } = 4;
    public bool bInvisibleOtherGuildBaseCampAreaFX { get; set; } = false;

    #endregion

    #region 难度

    public string DeathPenalty { get; set; } = "All";

    public static readonly string[] DeathPenaltyOptions =
    [
        "None",
        "Item",
        "ItemAndEquipment",
        "All"
    ];

    public bool bPalLost { get; set; } = false;
    public bool bEnableFastTravel { get; set; } = true;

    #endregion

    #region 密码

    public string ServerPassword { get; set; } = "";
    public string AdminPassword { get; set; } = "";

    #endregion

    #region 没用

    public string Difficulty { get; set; } = "None";
    public string RandomizerType { get; set; } = "None";
    public string RandomizerSeed { get; set; } = "";
    public int DropItemMaxNum_UNKO { get; set; } = 100;
    public bool bActiveUNKO { get; set; } = false;
    public bool bEnablePlayerToPlayerDamage { get; set; } = false;
    public bool bEnableFriendlyFire { get; set; } = false;
    public bool bEnableAimAssistPad { get; set; } = true;
    public bool bEnableAimAssistKeyboard { get; set; } = false;
    public bool bAutoResetGuildNoOnlinePlayers { get; set; } = false;
    public double AutoResetGuildTimeNoOnlinePlayers { get; set; } = 72;
    public double WorkSpeedRate { get; set; } = 1;
    public int autoSaveSpan { get; set; } = 30;
    public bool bIsMultiplay { get; set; } = false;
    public bool bIsPvP { get; set; } = false;
    public bool bHardcore { get; set; } = false;
    public bool bCanPickupOtherGuildDeathPenaltyDrop { get; set; } = false;
    public bool bEnableNonLoginPenalty { get; set; } = true;
    public bool bIsStartLocationSelectByMap { get; set; } = true;
    public bool bExistPlayerAfterLogout { get; set; } = false;
    public bool bEnableDefenseOtherGuildPlayer { get; set; } = false;
    public bool bBuildAreaLimit { get; set; } = false;
    public int CoopPlayerMaxNum { get; set; } = 4;
    public bool RCONEnabled { get; set; } = false;
    public int RCONPort { get; set; } = 25575;
    public string Region { get; set; } = "";
    public bool bUseAuth { get; set; } = true;
    public string BanListURL { get; set; } = "https://api.palworldgame.com/api/banlist.txt";
    public bool RESTAPIEnabled { get; set; } = true;
    public int RESTAPIPort { get; set; } = 8212;
    public string AllowConnectPlatform { get; set; } = "Steam";
    public bool bIsUseBackupSaveData { get; set; } = true;
    public string LogFormatType { get; set; } = "Text";
    public bool EnablePredatorBossPal { get; set; } = true;
    public int ServerReplicatePawnCullDistance { get; set; } = 15000;

    #endregion

    public bool AreSettingsEqualExceptPassword(PalWorldSettings other)
    {
        var properties = typeof(PalWorldSettings).GetProperties();
        return !(from prop in properties
            where prop.Name != "ServerPassword" && prop.Name != "AdminPassword"
            let value1 = prop.GetValue(this)
            let value2 = prop.GetValue(other)
            where value1 != null || value2 != null
            where value1 == null || value2 == null || !value1.Equals(value2)
            select value1).Any();
    }

    public string ToIni()
    {
        var properties = typeof(PalWorldSettings).GetProperties();
        List<string> settings = [];
        foreach (var prop in properties)
        {
            var value = prop.GetValue(this);
            if (value == null)
            {
                continue;
            }

            if (prop.PropertyType == typeof(string))
            {
                if ((string)value != "None")
                {
                    value = $"\"{value}\"";
                }
            }
            else if (prop.PropertyType == typeof(bool))
            {
                value = (bool)value ? "True" : "False";
            }
            else if (prop.PropertyType == typeof(double))
            {
                value = ((double)value).ToString("F6");
            }

            settings.Add($"{prop.Name}={value}");
        }


        return $"[/Script/Pal.PalGameWorldSettings]\nOptionSettings=({string.Join(",", settings)})";
    }
}
