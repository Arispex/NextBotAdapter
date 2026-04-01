using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NextBotAdapter.Models;

namespace NextBotAdapter.Services;

public sealed class WhitelistConfigService
{
    private static readonly JsonSerializerSettings JsonSettings = new()
    {
        Formatting = Formatting.Indented
    };
    private readonly string _configDirectoryPath;

    public WhitelistConfigService()
        : this(Path.Combine(TShockAPI.TShock.SavePath, "NextBotAdapter"))
    {
    }

    public WhitelistConfigService(string configDirectoryPath)
    {
        _configDirectoryPath = configDirectoryPath;
    }

    public string ConfigDirectoryPath => _configDirectoryPath;
    public string SettingsFilePath => Path.Combine(ConfigDirectoryPath, "NextBotAdapter.json");
    public string WhitelistFilePath => Path.Combine(ConfigDirectoryPath, "Whitelist.json");

    public void EnsureConfigComplete()
    {
        EnsureDirectory();
        if (!File.Exists(SettingsFilePath)) return;

        try
        {
            var originalText = File.ReadAllText(SettingsFilePath);
            var config = JsonConvert.DeserializeObject<NextBotAdapterConfig>(originalText, JsonSettings);
            var complete = (config ?? NextBotAdapterConfig.Default).WithDefaults();
            var completeText = JsonConvert.SerializeObject(complete, JsonSettings);

            if (originalText != completeText)
            {
                File.WriteAllText(SettingsFilePath, completeText);
                PluginLogger.Info("配置文件已自动补全缺失字段。");
            }
        }
        catch (Exception ex)
        {
            PluginLogger.Warn($"配置文件补全检查失败，原因：{ex.Message}");
        }
    }

    public WhitelistSettings LoadSettings()
    {
        EnsureDirectory();
        if (!File.Exists(SettingsFilePath))
        {
            WriteConfigFile(NextBotAdapterConfig.Default);
            PluginLogger.Info("默认插件配置文件已创建。");
            return WhitelistSettings.Default;
        }

        try
        {
            var config = JsonConvert.DeserializeObject<NextBotAdapterConfig>(File.ReadAllText(SettingsFilePath), JsonSettings);
            var settings = config?.Whitelist ?? WhitelistSettings.Default;
            PluginLogger.Info($"白名单配置加载完成：启用状态：{settings.Enabled}，区分大小写：{settings.CaseSensitive}。");
            return settings;
        }
        catch (Exception ex)
        {
            PluginLogger.Warn($"白名单配置加载失败，已回退到默认配置，原因：{ex.Message}");
            return WhitelistSettings.Default;
        }
    }

    public LoginConfirmationSettings LoadLoginConfirmationSettings()
    {
        EnsureDirectory();
        if (!File.Exists(SettingsFilePath))
        {
            return LoginConfirmationSettings.Default;
        }

        try
        {
            var config = JsonConvert.DeserializeObject<NextBotAdapterConfig>(File.ReadAllText(SettingsFilePath), JsonSettings);
            return config?.LoginConfirmation ?? LoginConfirmationSettings.Default;
        }
        catch
        {
            return LoginConfirmationSettings.Default;
        }
    }

    public void SaveSettings(WhitelistSettings settings)
    {
        EnsureDirectory();
        WriteConfigFile(new NextBotAdapterConfig(settings));
        PluginLogger.Info($"白名单配置保存完成：启用状态：{settings.Enabled}，区分大小写：{settings.CaseSensitive}。");
    }

    public WhitelistStore LoadWhitelist()
    {
        EnsureDirectory();
        if (!File.Exists(WhitelistFilePath))
        {
            WriteWhitelistFile(WhitelistStore.Empty);
            PluginLogger.Info("默认白名单数据文件已创建。");
            return WhitelistStore.Empty;
        }

        try
        {
            var store = JsonConvert.DeserializeObject<WhitelistStore>(File.ReadAllText(WhitelistFilePath), JsonSettings);
            var whitelist = store ?? WhitelistStore.Empty;
            PluginLogger.Info($"白名单数据加载完成，共 {whitelist.Users.Count} 个条目。");
            return whitelist;
        }
        catch (Exception ex)
        {
            PluginLogger.Warn($"白名单数据加载失败，已回退为空白名单，原因：{ex.Message}");
            return WhitelistStore.Empty;
        }
    }

    public void SaveWhitelist(WhitelistStore store)
    {
        EnsureDirectory();
        WriteWhitelistFile(store);
        PluginLogger.Info($"白名单数据保存完成，共 {store.Users.Count} 个条目。");
    }

    public string ReadConfigRaw()
    {
        EnsureDirectory();
        return File.Exists(SettingsFilePath)
            ? File.ReadAllText(SettingsFilePath)
            : JsonConvert.SerializeObject(NextBotAdapterConfig.Default, JsonSettings);
    }

    public bool TryUpdateConfig(IEnumerable<KeyValuePair<string, string>> fields, out string? error)
    {
        EnsureDirectory();
        var text = File.Exists(SettingsFilePath)
            ? File.ReadAllText(SettingsFilePath)
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

        File.WriteAllText(SettingsFilePath, root.ToString(Formatting.Indented));
        error = null;
        return true;
    }

    private static JToken ParseValue(string value)
    {
        if (bool.TryParse(value, out var b)) return new JValue(b);
        if (long.TryParse(value, out var l)) return new JValue(l);
        if (double.TryParse(value, out var d)) return new JValue(d);
        return new JValue(value);
    }

    private void EnsureDirectory()
    {
        Directory.CreateDirectory(ConfigDirectoryPath);
    }

    private void WriteConfigFile(NextBotAdapterConfig config)
    {
        File.WriteAllText(SettingsFilePath, JsonConvert.SerializeObject(config, JsonSettings));
    }

    private void WriteWhitelistFile(WhitelistStore store)
    {
        File.WriteAllText(WhitelistFilePath, JsonConvert.SerializeObject(store, JsonSettings));
    }
}