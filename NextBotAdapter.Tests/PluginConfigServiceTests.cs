using System.IO;
using Newtonsoft.Json;
using NextBotAdapter.Models;
using NextBotAdapter.Services;

namespace NextBotAdapter.Tests;

public sealed class PluginConfigServiceTests
{
    private static readonly JsonSerializerSettings JsonSettings = new() { Formatting = Formatting.Indented };

    [Fact]
    public void LoadWhitelistSettings_ShouldFallbackToDefaultWhenJsonIsInvalid()
    {
        var service = CreateService();
        const string invalidJson = "{invalid json}";
        File.WriteAllText(service.ConfigFilePath, invalidJson);

        var settings = service.LoadWhitelistSettings();

        Assert.Equal(WhitelistSettings.Default, settings);
        Assert.Equal(invalidJson, File.ReadAllText(service.ConfigFilePath));
    }

    [Fact]
    public void LoadWhitelistSettings_ShouldReturnDefaultWhenFileMissing()
    {
        var service = CreateService();

        var settings = service.LoadWhitelistSettings();

        Assert.Equal(WhitelistSettings.Default, settings);
        Assert.False(File.Exists(service.ConfigFilePath));
    }

    [Fact]
    public void EnsureConfigComplete_ShouldAddMissingTopLevelSection()
    {
        var service = CreateService();
        File.WriteAllText(service.ConfigFilePath,
            JsonConvert.SerializeObject(new { whitelist = WhitelistSettings.Default }, JsonSettings));

        service.EnsureConfigComplete();

        var raw = File.ReadAllText(service.ConfigFilePath);
        Assert.Contains("\"loginConfirmation\"", raw);
        Assert.Contains("\"detectUuid\"", raw);
    }

    [Fact]
    public void EnsureConfigComplete_ShouldNotOverwriteExistingValues()
    {
        var service = CreateService();
        var config = new NextBotAdapterConfig(
            new WhitelistSettings(false, "Custom deny", false),
            new LoginConfirmationSettings(false, false, true));
        File.WriteAllText(service.ConfigFilePath, JsonConvert.SerializeObject(config, JsonSettings));

        service.EnsureConfigComplete();

        var result = JsonConvert.DeserializeObject<NextBotAdapterConfig>(
            File.ReadAllText(service.ConfigFilePath), JsonSettings);
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
        var config = NextBotAdapterConfig.Default;
        var fullText = JsonConvert.SerializeObject(config, JsonSettings);
        File.WriteAllText(service.ConfigFilePath, fullText);
        var lastWrite = File.GetLastWriteTimeUtc(service.ConfigFilePath);

        System.Threading.Thread.Sleep(50);
        service.EnsureConfigComplete();

        Assert.Equal(lastWrite, File.GetLastWriteTimeUtc(service.ConfigFilePath));
    }

    [Fact]
    public void EnsureConfigComplete_ShouldCreateDefaultFileWhenMissing()
    {
        var service = CreateService();

        service.EnsureConfigComplete();

        Assert.True(File.Exists(service.ConfigFilePath));

        var raw = File.ReadAllText(service.ConfigFilePath);
        Assert.Contains("\"whitelist\"", raw);
        Assert.Contains("\"loginConfirmation\"", raw);

        var config = JsonConvert.DeserializeObject<NextBotAdapterConfig>(raw, JsonSettings);
        Assert.NotNull(config);
        Assert.Equal(WhitelistSettings.Default, config!.Whitelist);
        Assert.Equal(LoginConfirmationSettings.Default, config.LoginConfirmation);
    }

    [Fact]
    public void EnsureConfigComplete_ShouldCreateDataDirectory()
    {
        var service = CreateService();

        service.EnsureConfigComplete();

        Assert.True(Directory.Exists(service.DataDirectoryPath));
    }

    [Fact]
    public void EnsureConfigComplete_ShouldFillMissingNestedStringFieldWithDefault()
    {
        var service = CreateService();
        const string partialJson = """
            {
              "whitelist": {
                "enabled": true,
                "caseSensitive": false
              }
            }
            """;
        File.WriteAllText(service.ConfigFilePath, partialJson);

        service.EnsureConfigComplete();

        var result = JsonConvert.DeserializeObject<NextBotAdapterConfig>(
            File.ReadAllText(service.ConfigFilePath), JsonSettings);
        Assert.NotNull(result);
        Assert.Equal(WhitelistSettings.Default.DenyMessage, result!.Whitelist.DenyMessage);
        Assert.True(result.Whitelist.Enabled);
        Assert.False(result.Whitelist.CaseSensitive);
    }

    [Fact]
    public void EnsureConfigComplete_ShouldFillMissingNestedBoolFieldWithDefault()
    {
        var service = CreateService();
        const string partialJson = """
            {
              "whitelist": {
                "enabled": true,
                "denyMessage": "xxxx"
              }
            }
            """;
        File.WriteAllText(service.ConfigFilePath, partialJson);

        service.EnsureConfigComplete();

        var result = JsonConvert.DeserializeObject<NextBotAdapterConfig>(
            File.ReadAllText(service.ConfigFilePath), JsonSettings);
        Assert.NotNull(result);
        Assert.Equal(WhitelistSettings.Default.CaseSensitive, result!.Whitelist.CaseSensitive);
        Assert.Equal("xxxx", result.Whitelist.DenyMessage);
        Assert.True(result.Whitelist.Enabled);
    }

    [Fact]
    public void EnsureConfigComplete_ShouldPreserveUserProvidedNestedValuesWhenCompletingSiblings()
    {
        var service = CreateService();
        const string partialJson = """
            {
              "whitelist": {
                "denyMessage": "xxxx"
              },
              "loginConfirmation": {
                "enabled": false
              }
            }
            """;
        File.WriteAllText(service.ConfigFilePath, partialJson);

        service.EnsureConfigComplete();

        var result = JsonConvert.DeserializeObject<NextBotAdapterConfig>(
            File.ReadAllText(service.ConfigFilePath), JsonSettings);
        Assert.NotNull(result);
        Assert.Equal("xxxx", result!.Whitelist.DenyMessage);
        Assert.Equal(WhitelistSettings.Default.Enabled, result.Whitelist.Enabled);
        Assert.Equal(WhitelistSettings.Default.CaseSensitive, result.Whitelist.CaseSensitive);
        Assert.NotNull(result.LoginConfirmation);
        Assert.False(result.LoginConfirmation!.Enabled);
        Assert.Equal(LoginConfirmationSettings.Default.DetectUuid, result.LoginConfirmation.DetectUuid);
        Assert.Equal(LoginConfirmationSettings.Default.DetectIp, result.LoginConfirmation.DetectIp);
        Assert.Equal(LoginConfirmationSettings.Default.EmptyUuidMessage, result.LoginConfirmation.EmptyUuidMessage);
        Assert.Equal(LoginConfirmationSettings.Default.ChangeDetectedMessage, result.LoginConfirmation.ChangeDetectedMessage);
        Assert.Equal(LoginConfirmationSettings.Default.DeviceMismatchMessage, result.LoginConfirmation.DeviceMismatchMessage);
        Assert.Equal(LoginConfirmationSettings.Default.PendingExistsMessage, result.LoginConfirmation.PendingExistsMessage);
    }

    [Fact]
    public void EnsureConfigComplete_ShouldIgnoreExplicitNullFromUser()
    {
        var service = CreateService();
        const string partialJson = """
            {
              "whitelist": {
                "enabled": true,
                "denyMessage": null,
                "caseSensitive": false
              }
            }
            """;
        File.WriteAllText(service.ConfigFilePath, partialJson);

        service.EnsureConfigComplete();

        var result = JsonConvert.DeserializeObject<NextBotAdapterConfig>(
            File.ReadAllText(service.ConfigFilePath), JsonSettings);
        Assert.NotNull(result);
        Assert.Equal(WhitelistSettings.Default.DenyMessage, result!.Whitelist.DenyMessage);
        Assert.False(result.Whitelist.CaseSensitive);
    }

    private static PluginConfigService CreateService()
    {
        var root = Path.Combine(Path.GetTempPath(), "NextBotAdapter.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return new PluginConfigService(root);
    }
}
