using System.IO;
using System.Text.Json;
using NextBotAdapter.Models;
using NextBotAdapter.Services;

namespace NextBotAdapter.Tests;

public sealed class WhitelistConfigServiceTests
{
    [Fact]
    public void LoadSettings_ShouldFallbackToDefaultWhenJsonIsInvalid()
    {
        var service = CreateService();
        File.WriteAllText(service.SettingsFilePath, "{invalid json}");

        var settings = service.LoadSettings();

        Assert.Equal(WhitelistSettings.Default, settings);
    }

    [Fact]
    public void LoadWhitelist_ShouldFallbackToEmptyWhenJsonIsInvalid()
    {
        var service = CreateService();
        File.WriteAllText(service.WhitelistFilePath, "{invalid json}");

        var store = service.LoadWhitelist();

        Assert.Equal(WhitelistStore.Empty, store);
    }

    [Fact]
    public void LoadSettings_ShouldCreateDefaultFileWhenMissing()
    {
        var service = CreateService();
        if (File.Exists(service.SettingsFilePath))
        {
            File.Delete(service.SettingsFilePath);
        }

        var settings = service.LoadSettings();

        Assert.Equal(WhitelistSettings.Default, settings);
        Assert.True(File.Exists(service.SettingsFilePath));

        var raw = File.ReadAllText(service.SettingsFilePath);
        Assert.Contains("\"whitelist\"", raw);

        var config = JsonSerializer.Deserialize<NextBotAdapterConfig>(raw, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(config);
        Assert.Equal(WhitelistSettings.Default, config!.Whitelist);
    }

    [Fact]
    public void WhitelistFilePath_ShouldUseCapitalizedFileName()
    {
        var service = CreateService();

        Assert.EndsWith("Whitelist.json", service.WhitelistFilePath);
    }

    private static WhitelistConfigService CreateService()
    {
        var root = Path.Combine(Path.GetTempPath(), "NextBotAdapter.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return new WhitelistConfigService(root);
    }
}
