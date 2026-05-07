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
        Assert.Contains("\"nextbot\"", raw);
        Assert.Contains("\"baseUrl\"", raw);
        Assert.Contains("\"loginConfirmation\"", raw);
        Assert.Contains("\"detectUuid\"", raw);
    }

    [Fact]
    public void EnsureConfigComplete_ShouldNotOverwriteExistingValues()
    {
        var service = CreateService();
        var config = new NextBotAdapterConfig(
            new NextBotSettings("https://example.com", "secret"),
            new WhitelistSettings(false, "Custom deny"),
            BlacklistSettings.Default,
            SyncSettings.Default,
            new LoginConfirmationSettings(false, false, true));
        File.WriteAllText(service.ConfigFilePath, JsonConvert.SerializeObject(config, JsonSettings));

        service.EnsureConfigComplete();

        var result = JsonConvert.DeserializeObject<NextBotAdapterConfig>(
            File.ReadAllText(service.ConfigFilePath), JsonSettings);
        Assert.NotNull(result);
        Assert.Equal("https://example.com", result!.NextBot.BaseUrl);
        Assert.Equal("secret", result.NextBot.Token);
        Assert.False(result.Whitelist.Enabled);
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
        Assert.Contains("\"nextbot\"", raw);
        Assert.Contains("\"baseUrl\"", raw);
        Assert.Contains("\"whitelist\"", raw);
        Assert.Contains("\"loginConfirmation\"", raw);
        Assert.True(raw.IndexOf("\"nextbot\"", StringComparison.Ordinal) < raw.IndexOf("\"whitelist\"", StringComparison.Ordinal),
            "nextbot section should serialize before whitelist section.");

        var config = JsonConvert.DeserializeObject<NextBotAdapterConfig>(raw, JsonSettings);
        Assert.NotNull(config);
        Assert.Equal(NextBotSettings.Default, config!.NextBot);
        Assert.Equal(WhitelistSettings.Default, config.Whitelist);
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
                "enabled": true
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
        Assert.Equal("xxxx", result!.Whitelist.DenyMessage);
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
        Assert.NotNull(result.LoginConfirmation);
        Assert.False(result.LoginConfirmation!.Enabled);
        Assert.Equal(LoginConfirmationSettings.Default.DetectUuid, result.LoginConfirmation.DetectUuid);
        Assert.Equal(LoginConfirmationSettings.Default.DetectIp, result.LoginConfirmation.DetectIp);
        Assert.Equal(LoginConfirmationSettings.Default.EmptyUuidMessage, result.LoginConfirmation.EmptyUuidMessage);
        Assert.Equal(LoginConfirmationSettings.Default.ChangeDetectedMessage, result.LoginConfirmation.ChangeDetectedMessage);
        Assert.Equal(LoginConfirmationSettings.Default.DeviceMismatchMessage, result.LoginConfirmation.DeviceMismatchMessage);
        Assert.Equal(LoginConfirmationSettings.Default.PendingExistsMessage, result.LoginConfirmation.PendingExistsMessage);
        Assert.False(result.LoginConfirmation.AutoLogin);
    }

    [Fact]
    public void LoginConfirmationDefault_AutoLoginDisabled()
    {
        Assert.False(LoginConfirmationSettings.Default.AutoLogin);
    }

    [Fact]
    public void EnsureConfigComplete_ShouldIgnoreExplicitNullFromUser()
    {
        var service = CreateService();
        const string partialJson = """
            {
              "whitelist": {
                "enabled": true,
                "denyMessage": null
              }
            }
            """;
        File.WriteAllText(service.ConfigFilePath, partialJson);

        service.EnsureConfigComplete();

        var result = JsonConvert.DeserializeObject<NextBotAdapterConfig>(
            File.ReadAllText(service.ConfigFilePath), JsonSettings);
        Assert.NotNull(result);
        Assert.Equal(WhitelistSettings.Default.DenyMessage, result!.Whitelist.DenyMessage);
    }

    [Fact]
    public void Load_ShouldReturnCachedInstance_OnSubsequentCalls()
    {
        var service = CreateService();
        // Seed a real config file so Load reads from disk on the first call.
        File.WriteAllText(service.ConfigFilePath,
            JsonConvert.SerializeObject(NextBotAdapterConfig.Default, JsonSettings));

        var first = service.Load();
        var second = service.Load();

        // Cache hit returns the same reference without re-reading the file.
        Assert.Same(first, second);
    }

    [Fact]
    public void Reload_ShouldInvalidateCache_AndReturnFreshInstance()
    {
        var service = CreateService();
        File.WriteAllText(service.ConfigFilePath,
            JsonConvert.SerializeObject(NextBotAdapterConfig.Default, JsonSettings));

        var first = service.Load();
        var afterReload = service.Reload();

        // Reload drops the cache, so the next Load must produce a different
        // instance even when the on-disk content hasn't changed.
        Assert.NotSame(first, afterReload);
    }

    [Fact]
    public void Save_ShouldInvalidateCache()
    {
        var service = CreateService();
        File.WriteAllText(service.ConfigFilePath,
            JsonConvert.SerializeObject(NextBotAdapterConfig.Default, JsonSettings));

        var first = service.Load();
        service.Save(NextBotAdapterConfig.Default);
        var second = service.Load();

        Assert.NotSame(first, second);
    }

    [Fact]
    public void EnsureConfigComplete_ShouldInvalidateCache_WhenFileRewritten()
    {
        var service = CreateService();
        // Write a partial config so EnsureConfigComplete actually rewrites the
        // file (originalText != completeText).
        File.WriteAllText(service.ConfigFilePath, """
            {
              "whitelist": { "enabled": true }
            }
            """);

        var first = service.Load();
        service.EnsureConfigComplete();
        var second = service.Load();

        Assert.NotSame(first, second);
    }

    [Fact]
    public void TryUpdateConfig_ShouldInvalidateCache()
    {
        var service = CreateService();
        File.WriteAllText(service.ConfigFilePath,
            JsonConvert.SerializeObject(NextBotAdapterConfig.Default, JsonSettings));

        var first = service.Load();
        var ok = service.TryUpdateConfig(
            new[] { new KeyValuePair<string, string>("$.whitelist.enabled", "false") },
            out var error);
        var second = service.Load();

        Assert.True(ok);
        Assert.Null(error);
        Assert.NotSame(first, second);
    }

    private static PluginConfigService CreateService()
    {
        var root = Path.Combine(Path.GetTempPath(), "NextBotAdapter.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return new PluginConfigService(root);
    }
}
