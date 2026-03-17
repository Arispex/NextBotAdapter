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
            PluginLogger.Info("创建默认白名单配置文件成功。");
            return WhitelistSettings.Default;
        }

        try
        {
            var config = JsonSerializer.Deserialize<NextBotAdapterConfig>(File.ReadAllText(SettingsFilePath), _jsonOptions);
            var settings = config?.Whitelist ?? WhitelistSettings.Default;
            PluginLogger.Info($"加载白名单配置成功。当前启用状态为 {settings.Enabled}，区分大小写为 {settings.CaseSensitive}。");
            return settings;
        }
        catch (Exception ex)
        {
            PluginLogger.Warn($"加载白名单配置失败，将回退为默认配置，原因：{ex.Message}");
            return WhitelistSettings.Default;
        }
    }

    public void SaveSettings(WhitelistSettings settings)
    {
        EnsureDirectory();
        WriteSettingsFile(settings);
        PluginLogger.Info($"保存白名单配置成功。当前启用状态为 {settings.Enabled}，区分大小写为 {settings.CaseSensitive}。");
    }

    public WhitelistStore LoadWhitelist()
    {
        EnsureDirectory();
        if (!File.Exists(WhitelistFilePath))
        {
            WriteWhitelistFile(WhitelistStore.Empty);
            PluginLogger.Info("创建默认白名单文件成功。");
            return WhitelistStore.Empty;
        }

        try
        {
            var store = JsonSerializer.Deserialize<WhitelistStore>(File.ReadAllText(WhitelistFilePath), _jsonOptions);
            var whitelist = store ?? WhitelistStore.Empty;
            PluginLogger.Info($"加载白名单数据成功，当前共有 {whitelist.Users.Count} 个条目。");
            return whitelist;
        }
        catch (Exception ex)
        {
            PluginLogger.Warn($"加载白名单数据失败，将回退为空白名单，原因：{ex.Message}");
            return WhitelistStore.Empty;
        }
    }

    public void SaveWhitelist(WhitelistStore store)
    {
        EnsureDirectory();
        WriteWhitelistFile(store);
        PluginLogger.Info($"保存白名单数据成功，当前共有 {store.Users.Count} 个条目。");
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
