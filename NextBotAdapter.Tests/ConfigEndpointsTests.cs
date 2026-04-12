using System.IO;
using System.Threading;
using System.Threading.Tasks;
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
        Assert.NotNull(result["nextbot"]);
        Assert.NotNull(result["whitelist"]);
        Assert.NotNull(result["blacklist"]);
        Assert.NotNull(result["sync"]);
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

        var raw = File.ReadAllText(configService.ConfigFilePath);
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
            new("loginConfirmation.autoLogin", "true"),
            new("whitelist.denyMessage", "Custom message"),
            new("nextbot.baseUrl", "https://example.com/api"),
            new("nextbot.token", "secret-token")
        };

        var ok = configService.TryUpdateConfig(fields, out _);
        Assert.True(ok);

        var raw = File.ReadAllText(configService.ConfigFilePath);
        var config = JsonConvert.DeserializeObject<NextBotAdapterConfig>(raw, JsonSettings);
        Assert.False(config!.LoginConfirmation!.DetectUuid);
        Assert.True(config.LoginConfirmation.AutoLogin);
        Assert.Equal("Custom message", config.Whitelist.DenyMessage);
        Assert.Equal("https://example.com/api", config.NextBot.BaseUrl);
        Assert.Equal("secret-token", config.NextBot.Token);
    }

    [Fact]
    public void VerifyNextBot_ReturnsProbeStatus()
    {
        var configService = CreateConfigService();
        configService.TryUpdateConfig(new List<KeyValuePair<string, string>>
        {
            new("nextbot.baseUrl", "https://example.com"),
            new("nextbot.token", "secret"),
        }, out _);

        var probe = new FakeProbeService(new NextBotProbeResult(NextBotProbeStatus.Ok, 201, "上游返回 201 Created，token 有效"));

        var result = Assert.IsType<RestObject>(ConfigEndpoints.VerifyNextBot(configService, probe));

        Assert.Equal("200", result.Status);
        Assert.Equal("Ok", result["probeStatus"]);
        Assert.Equal("https://example.com", result["baseUrl"]);
        Assert.Equal(201, result["httpStatus"]);
    }

    [Fact]
    public void VerifyNextBot_ReturnsSkippedWhenNotConfigured()
    {
        var configService = CreateConfigService();
        var probe = new FakeProbeService(new NextBotProbeResult(NextBotProbeStatus.Skipped, null, "未配置 baseUrl 或 token"));

        var result = Assert.IsType<RestObject>(ConfigEndpoints.VerifyNextBot(configService, probe));

        Assert.Equal("200", result.Status);
        Assert.Equal("Skipped", result["probeStatus"]);
    }

    private sealed class FakeProbeService(NextBotProbeResult result) : INextBotSessionProbeService
    {
        public Task<NextBotProbeResult> ProbeAsync(NextBotSettings settings, CancellationToken ct = default)
            => Task.FromResult(result);

        public Task<NextBotLoginRequestResult> NotifyLoginRequestAsync(NextBotSettings settings, string playerName, bool newDevice = false, bool newLocation = false, CancellationToken ct = default)
            => Task.FromResult(new NextBotLoginRequestResult(true, 201, "ok"));

        public Task<NextBotFetchUsersResult> FetchUsersAsync(NextBotSettings settings, CancellationToken ct = default)
            => Task.FromResult(new NextBotFetchUsersResult(false, null, "not implemented"));
    }

    private static PluginConfigService CreateConfigService()
    {
        var root = Path.Combine(Path.GetTempPath(), "NextBotAdapter.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var service = new PluginConfigService(root);
        Directory.CreateDirectory(Path.Combine(root, "Data"));
        File.WriteAllText(service.ConfigFilePath,
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
