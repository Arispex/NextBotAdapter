using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NextBotAdapter.Models;

namespace NextBotAdapter.Services;

public sealed class PluginConfigService
{
    private static readonly JsonSerializerSettings JsonSettings = new()
    {
        Formatting = Formatting.Indented
    };
    private static readonly JsonMergeSettings MergeSettings = new()
    {
        MergeArrayHandling = MergeArrayHandling.Replace,
        MergeNullValueHandling = MergeNullValueHandling.Ignore
    };
    private readonly string _configDirectoryPath;

    public PluginConfigService()
        : this(Path.Combine(TShockAPI.TShock.SavePath, "NextBotAdapter"))
    {
    }

    public PluginConfigService(string configDirectoryPath)
    {
        _configDirectoryPath = configDirectoryPath;
    }

    public string ConfigDirectoryPath => _configDirectoryPath;
    public string DataDirectoryPath => Path.Combine(ConfigDirectoryPath, "Data");
    public string ConfigFilePath => Path.Combine(ConfigDirectoryPath, "NextBotAdapter.json");

    public void EnsureConfigComplete()
    {
        EnsureDirectories();
        if (!File.Exists(ConfigFilePath))
        {
            Save(NextBotAdapterConfig.Default);
            PluginLogger.Info("默认插件配置文件已创建。");
            return;
        }

        try
        {
            var originalText = File.ReadAllText(ConfigFilePath);
            var completed = BuildCompletedJson(originalText);
            var completeText = completed.ToString(Formatting.Indented);

            if (originalText != completeText)
            {
                File.WriteAllText(ConfigFilePath, completeText);
                PluginLogger.Info("配置文件已自动补全缺失字段。");
            }
        }
        catch (Exception ex)
        {
            PluginLogger.Warn($"配置文件补全检查失败，原因：{ex.Message}");
        }
    }

    public NextBotAdapterConfig Load()
    {
        EnsureDirectories();
        if (!File.Exists(ConfigFilePath))
        {
            return NextBotAdapterConfig.Default;
        }

        try
        {
            var completed = BuildCompletedJson(File.ReadAllText(ConfigFilePath));
            var config = completed.ToObject<NextBotAdapterConfig>(JsonSerializer.Create(JsonSettings));
            return (config ?? NextBotAdapterConfig.Default).WithDefaults();
        }
        catch (Exception ex)
        {
            PluginLogger.Warn($"插件配置加载失败，已回退到默认配置，原因：{ex.Message}");
            return NextBotAdapterConfig.Default;
        }
    }

    public NextBotAdapterConfig Reload()
        => Load();

    public void Save(NextBotAdapterConfig config)
    {
        EnsureDirectories();
        WriteConfigFile(config.WithDefaults());
    }

    public WhitelistSettings LoadWhitelistSettings()
        => Load().Whitelist;

    public BlacklistSettings LoadBlacklistSettings()
        => Load().Blacklist ?? BlacklistSettings.Default;

    public SyncSettings LoadSyncSettings()
        => Load().Sync ?? SyncSettings.Default;

    public LoginConfirmationSettings LoadLoginConfirmationSettings()
        => Load().LoginConfirmation ?? LoginConfirmationSettings.Default;

    public PlayerEventsSettings LoadPlayerEventsSettings()
        => Load().PlayerEvents ?? PlayerEventsSettings.Default;

    public string ReadConfigRaw()
    {
        EnsureDirectories();
        return File.Exists(ConfigFilePath)
            ? File.ReadAllText(ConfigFilePath)
            : JsonConvert.SerializeObject(NextBotAdapterConfig.Default, JsonSettings);
    }

    public bool TryUpdateConfig(IEnumerable<KeyValuePair<string, string>> fields, out string? error)
    {
        EnsureDirectories();
        var text = File.Exists(ConfigFilePath)
            ? File.ReadAllText(ConfigFilePath)
            : JsonConvert.SerializeObject(NextBotAdapterConfig.Default, JsonSettings);

        var root = JObject.Parse(text);

        foreach (var (path, value) in fields)
        {
            var token = root.SelectToken(path);
            if (token is null)
            {
                error = $"Unknown config field '{path}'.";
                return false;
            }

            token.Replace(ParseValue(value));
        }

        File.WriteAllText(ConfigFilePath, root.ToString(Formatting.Indented));
        error = null;
        return true;
    }

    internal void EnsureDirectories()
    {
        Directory.CreateDirectory(ConfigDirectoryPath);
        Directory.CreateDirectory(DataDirectoryPath);
    }

    private static JObject BuildCompletedJson(string userText)
    {
        var userJson = JObject.Parse(userText);
        var completed = JObject.FromObject(
            NextBotAdapterConfig.Default,
            JsonSerializer.Create(JsonSettings));
        completed.Merge(userJson, MergeSettings);
        return completed;
    }

    private static JToken ParseValue(string value)
    {
        if (bool.TryParse(value, out var b)) return new JValue(b);
        if (long.TryParse(value, out var l)) return new JValue(l);
        if (double.TryParse(value, out var d)) return new JValue(d);
        return new JValue(value);
    }

    private void WriteConfigFile(NextBotAdapterConfig config)
    {
        File.WriteAllText(ConfigFilePath, JsonConvert.SerializeObject(config, JsonSettings));
    }
}
