using System.IO;
using System.Text.Encodings.Web;
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
        const string invalidJson = "{invalid json}";
        File.WriteAllText(service.SettingsFilePath, invalidJson);

        var settings = service.LoadSettings();

        Assert.Equal(WhitelistSettings.Default, settings);
        Assert.Equal(invalidJson, File.ReadAllText(service.SettingsFilePath));
    }

    [Fact]
    public void LoadWhitelist_ShouldFallbackToEmptyWhenJsonIsInvalid()
    {
        var service = CreateService();
        const string invalidJson = "{invalid json}";
        File.WriteAllText(service.WhitelistFilePath, invalidJson);

        var store = service.LoadWhitelist();

        Assert.Equal(WhitelistStore.Empty, store);
        Assert.Equal(invalidJson, File.ReadAllText(service.WhitelistFilePath));
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

    [Fact]
    public void EnsureConfigComplete_ShouldAddMissingTopLevelSection()
    {
        var service = CreateService();
        var partial = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        // Config with only whitelist, no loginConfirmation
        File.WriteAllText(service.SettingsFilePath,
            JsonSerializer.Serialize(new { whitelist = WhitelistSettings.Default }, partial));

        service.EnsureConfigComplete();

        var raw = File.ReadAllText(service.SettingsFilePath);
        Assert.Contains("\"loginConfirmation\"", raw);
        Assert.Contains("\"detectUuid\"", raw);
    }

    [Fact]
    public void EnsureConfigComplete_ShouldNotOverwriteExistingValues()
    {
        var service = CreateService();
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        var config = new NextBotAdapterConfig(
            new WhitelistSettings(false, "Custom deny", false),
            new LoginConfirmationSettings(false, false, true));
        File.WriteAllText(service.SettingsFilePath, JsonSerializer.Serialize(config, options));

        service.EnsureConfigComplete();

        var result = JsonSerializer.Deserialize<NextBotAdapterConfig>(
            File.ReadAllText(service.SettingsFilePath), options);
        Assert.NotNull(result);
        Assert.False(result!.Whitelist.Enabled);
        Assert.Equal("Custom deny", result.Whitelist.DenyMessage);
        Assert.False(result.LoginConfirmation!.Enabled);
        Assert.False(result.LoginConfirmation.DetectUuid);
    }

    [Fact]
    public void EnsureConfigComplete_ShouldNotRewriteWhenAlreadyComplete()
    {
        var service = CreateService();
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        var config = NextBotAdapterConfig.Default;
        var fullText = JsonSerializer.Serialize(config, options);
        File.WriteAllText(service.SettingsFilePath, fullText);
        var lastWrite = File.GetLastWriteTimeUtc(service.SettingsFilePath);

        // Small delay to detect write time change
        System.Threading.Thread.Sleep(50);
        service.EnsureConfigComplete();

        Assert.Equal(lastWrite, File.GetLastWriteTimeUtc(service.SettingsFilePath));
    }

    [Fact]
    public void EnsureConfigComplete_ShouldSkipWhenFileDoesNotExist()
    {
        var service = CreateService();

        // Should not throw, and should not create the file
        service.EnsureConfigComplete();

        Assert.False(File.Exists(service.SettingsFilePath));
    }

    private static WhitelistConfigService CreateService()
    {
        var root = Path.Combine(Path.GetTempPath(), "NextBotAdapter.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return new WhitelistConfigService(root);
    }
}
