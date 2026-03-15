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

    public WhitelistSettings LoadSettings()
    {
        EnsureDirectory();
        if (!File.Exists(SettingsFilePath))
        {
            SaveSettings(WhitelistSettings.Default);
            PluginLogger.Info("Config", "Settings file not found, created default NextBotAdapter.json.");
            return WhitelistSettings.Default;
        }

        try
        {
            var config = JsonSerializer.Deserialize<NextBotAdapterConfig>(File.ReadAllText(SettingsFilePath), _jsonOptions);
            PluginLogger.Info("Config", "Loaded whitelist settings.");
            return config?.Whitelist ?? WhitelistSettings.Default;
        }
        catch (Exception ex)
        {
            PluginLogger.Error("Config", $"Failed to load whitelist settings, using defaults: {ex.Message}");
            return WhitelistSettings.Default;
        }
    }

    public void SaveSettings(WhitelistSettings settings)
    {
        EnsureDirectory();
        var config = new NextBotAdapterConfig(settings);
        File.WriteAllText(SettingsFilePath, JsonSerializer.Serialize(config, _jsonOptions));
        PluginLogger.Info("Config", "Saved whitelist settings.");
    }

    public WhitelistStore LoadWhitelist()
    {
        EnsureDirectory();
        if (!File.Exists(WhitelistFilePath))
        {
            SaveWhitelist(WhitelistStore.Empty);
            PluginLogger.Info("Config", "Whitelist file not found, created default Whitelist.json.");
            return WhitelistStore.Empty;
        }

        try
        {
            var store = JsonSerializer.Deserialize<WhitelistStore>(File.ReadAllText(WhitelistFilePath), _jsonOptions);
            var whitelist = store ?? WhitelistStore.Empty;
            PluginLogger.Info("Config", $"Loaded whitelist entries: {whitelist.Users.Count}.");
            return whitelist;
        }
        catch (Exception ex)
        {
            PluginLogger.Error("Config", $"Failed to load whitelist data, using empty whitelist: {ex.Message}");
            return WhitelistStore.Empty;
        }
    }

    public void SaveWhitelist(WhitelistStore store)
    {
        EnsureDirectory();
        File.WriteAllText(WhitelistFilePath, JsonSerializer.Serialize(store, _jsonOptions));
        PluginLogger.Info("Config", $"Saved whitelist data: {store.Users.Count} entries.");
    }

    private void EnsureDirectory()
    {
        Directory.CreateDirectory(ConfigDirectoryPath);
    }
}
