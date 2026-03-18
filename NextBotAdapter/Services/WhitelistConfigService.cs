using System.IO;
using System.Text.Json;
using NextBotAdapter.Models;

namespace NextBotAdapter.Services;

public sealed class WhitelistConfigService
{
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
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
    public string CacheDirectoryPath => Path.Combine(ConfigDirectoryPath, "cache");

    public WhitelistSettings LoadSettings()
    {
        EnsureDirectory();
        if (!File.Exists(SettingsFilePath))
        {
            WriteSettingsFile(WhitelistSettings.Default);
            PluginLogger.Info("默认白名单配置文件已创建。");
            return WhitelistSettings.Default;
        }

        try
        {
            var config = JsonSerializer.Deserialize<NextBotAdapterConfig>(File.ReadAllText(SettingsFilePath), _jsonOptions);
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

    public void SaveSettings(WhitelistSettings settings)
    {
        EnsureDirectory();
        WriteSettingsFile(settings);
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
            var store = JsonSerializer.Deserialize<WhitelistStore>(File.ReadAllText(WhitelistFilePath), _jsonOptions);
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

    private void EnsureDirectory()
    {
        Directory.CreateDirectory(ConfigDirectoryPath);
        Directory.CreateDirectory(CacheDirectoryPath);
    }

    private void WriteSettingsFile(WhitelistSettings settings)
    {
        var config = new NextBotAdapterConfig(settings);
        File.WriteAllText(SettingsFilePath, JsonSerializer.Serialize(config, _jsonOptions));
    }

    private void WriteWhitelistFile(WhitelistStore store)
    {
        File.WriteAllText(WhitelistFilePath, JsonSerializer.Serialize(store, _jsonOptions));
    }
}
