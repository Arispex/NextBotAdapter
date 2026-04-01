using System.IO;
using Newtonsoft.Json;
using NextBotAdapter.Infrastructure;
using NextBotAdapter.Models;
using NextBotAdapter.Rest;
using NextBotAdapter.Services;
using Rests;

namespace NextBotAdapter.Tests;

public sealed class ConfigEndpointsTests
{
    private static readonly JsonSerializerSettings JsonSettings = new() { Formatting = Formatting.Indented };

    [Fact]
    public void Reload_ShouldReturnSuccessWhenReloadCompletes()
    {
        var service = new FakeReloadService();

        var result = Assert.IsType<RestObject>(ConfigEndpoints.Reload(service));

        Assert.Equal("200", result.Status);
        Assert.True(service.ReloadCalled);
        Assert.Equal("Configuration reloaded successfully.", result["response"]);
    }

    [Fact]
    public void Reload_ShouldReturnServerErrorWhenReloadThrows()
    {
        var service = new FakeReloadService(throwOnReload: true);

        var result = Assert.IsType<RestObject>(ConfigEndpoints.Reload(service));

        Assert.Equal("500", result.Status);
        Assert.Equal("reload failed", result.Error);
    }

    [Fact]
    public void Read_ShouldReturnFullConfig()
    {
        var configService = CreateConfigService();

        var result = Assert.IsType<RestObject>(ConfigEndpoints.Read(configService));

        Assert.Equal("200", result.Status);
        Assert.NotNull(result["whitelist"]);
        Assert.NotNull(result["loginConfirmation"]);
    }

    [Fact]
    public void Update_ShouldReturnErrorWhenNoFieldsProvided()
    {
        var configService = CreateConfigService();
        var reloadService = new FakeReloadService();

        var result = Assert.IsType<RestObject>(
            ConfigEndpoints.Update(null, configService, reloadService));

        Assert.Equal("400", result.Status);
        Assert.Equal("No fields specified for update.", result.Error);
    }

    [Fact]
    public void Update_ShouldReturnErrorForUnknownField()
    {
        var configService = CreateConfigService();
        var reloadService = new FakeReloadService();
        var fields = new List<KeyValuePair<string, string>>
        {
            new("whitelist.nonExistent", "true")
        };

        var ok = configService.TryUpdateConfig(fields, out var error);

        Assert.False(ok);
        Assert.Contains("nonExistent", error);
    }

    [Fact]
    public void Update_ShouldModifyConfigFileAndReload()
    {
        var configService = CreateConfigService();
        var reloadService = new FakeReloadService();
        var fields = new List<KeyValuePair<string, string>>
        {
            new("whitelist.enabled", "false")
        };

        var ok = configService.TryUpdateConfig(fields, out _);
        Assert.True(ok);

        var raw = File.ReadAllText(configService.SettingsFilePath);
        var config = JsonConvert.DeserializeObject<NextBotAdapterConfig>(raw, JsonSettings);
        Assert.False(config!.Whitelist.Enabled);
    }

    [Fact]
    public void Update_ShouldSupportDotNotationForNestedFields()
    {
        var configService = CreateConfigService();
        var fields = new List<KeyValuePair<string, string>>
        {
            new("loginConfirmation.detectUuid", "false"),
            new("whitelist.denyMessage", "Custom message")
        };

        var ok = configService.TryUpdateConfig(fields, out _);
        Assert.True(ok);

        var raw = File.ReadAllText(configService.SettingsFilePath);
        var config = JsonConvert.DeserializeObject<NextBotAdapterConfig>(raw, JsonSettings);
        Assert.False(config!.LoginConfirmation!.DetectUuid);
        Assert.Equal("Custom message", config.Whitelist.DenyMessage);
    }

    private static WhitelistConfigService CreateConfigService()
    {
        var root = Path.Combine(Path.GetTempPath(), "NextBotAdapter.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var service = new WhitelistConfigService(root);
        File.WriteAllText(service.SettingsFilePath,
            JsonConvert.SerializeObject(NextBotAdapterConfig.Default, JsonSettings));
        return service;
    }

    private sealed class FakeReloadService(bool throwOnReload = false) : IConfigurationReloadService
    {
        public bool ReloadCalled { get; private set; }

        public void ReloadAll()
        {
            ReloadCalled = true;
            if (throwOnReload)
            {
                throw new InvalidOperationException("reload failed");
            }
        }
    }
}
