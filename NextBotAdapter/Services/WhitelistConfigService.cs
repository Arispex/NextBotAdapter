using System.IO;
using System.Text.Json;
using NextBotAdapter.Models;
using TShockAPI;

namespace NextBotAdapter.Services;

public sealed class WhitelistConfigService
{
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly string _configDirectoryPath;

    public WhitelistConfigService()
        : this(Path.Combine(TShock.SavePath, "NextBotAdapter"))
    {
    }

    public WhitelistConfigService(string configDirectoryPath)
    {
        _configDirectoryPath = configDirectoryPath;
    }

    public string ConfigDirectoryPath => _configDirectoryPath;
    public string SettingsFilePath => Path.Combine(ConfigDirectoryPath, "NextBotAdapter.json");
    public string WhitelistFilePath => Path.Combine(ConfigDirectoryPath, "whitelist.json");

    public WhitelistSettings LoadSettings()
    {
        EnsureDirectory();
        if (!File.Exists(SettingsFilePath))
        {
            SaveSettings(WhitelistSettings.Default);
            return WhitelistSettings.Default;
        }

        try
        {
            var settings = JsonSerializer.Deserialize<WhitelistSettings>(File.ReadAllText(SettingsFilePath), _jsonOptions);
            return settings ?? WhitelistSettings.Default;
        }
        catch (Exception ex)
        {
            TryLog($"Failed to load whitelist settings, using defaults: {ex.Message}");
            SaveSettings(WhitelistSettings.Default);
            return WhitelistSettings.Default;
        }
    }

    public void SaveSettings(WhitelistSettings settings)
    {
        EnsureDirectory();
        File.WriteAllText(SettingsFilePath, JsonSerializer.Serialize(settings, _jsonOptions));
    }

    public WhitelistStore LoadWhitelist()
    {
        EnsureDirectory();
        if (!File.Exists(WhitelistFilePath))
        {
            SaveWhitelist(WhitelistStore.Empty);
            return WhitelistStore.Empty;
        }

        try
        {
            var store = JsonSerializer.Deserialize<WhitelistStore>(File.ReadAllText(WhitelistFilePath), _jsonOptions);
            return store ?? WhitelistStore.Empty;
        }
        catch (Exception ex)
        {
            TryLog($"Failed to load whitelist data, using empty whitelist: {ex.Message}");
            SaveWhitelist(WhitelistStore.Empty);
            return WhitelistStore.Empty;
        }
    }

    public void SaveWhitelist(WhitelistStore store)
    {
        EnsureDirectory();
        File.WriteAllText(WhitelistFilePath, JsonSerializer.Serialize(store, _jsonOptions));
    }

    private void EnsureDirectory()
    {
        Directory.CreateDirectory(ConfigDirectoryPath);
    }

    private static void TryLog(string message)
    {
        TShock.Log?.ConsoleError(message);
    }
}
