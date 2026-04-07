using System.IO;
using Newtonsoft.Json;
using NextBotAdapter.Models;
using NextBotAdapter.Services;

namespace NextBotAdapter.Tests;

public sealed class WhitelistServicePersistenceTests
{
    private static readonly JsonSerializerSettings JsonSettings = new() { Formatting = Formatting.Indented };

    [Fact]
    public void Load_ShouldFallbackToEmptyWhenJsonIsInvalid()
    {
        var (_, service) = CreateServices();
        const string invalidJson = "{invalid json}";
        File.WriteAllText(service.FilePath!, invalidJson);

        var store = service.Load();

        Assert.Equal(WhitelistStore.Empty, store);
        Assert.Equal(invalidJson, File.ReadAllText(service.FilePath!));
    }

    [Fact]
    public void FilePath_ShouldUseDataDirectoryAndCapitalizedFileName()
    {
        var (_, service) = CreateServices();

        Assert.NotNull(service.FilePath);
        Assert.EndsWith(Path.Combine("Data", "Whitelist.json"), service.FilePath);
    }

    [Fact]
    public void TryAdd_ShouldPersistToDataFile()
    {
        var (_, service) = CreateServices();

        var added = service.TryAdd("Arispex", out var error);

        Assert.True(added);
        Assert.Null(error);

        var raw = File.ReadAllText(service.FilePath!);
        var store = JsonConvert.DeserializeObject<WhitelistStore>(raw, JsonSettings);
        Assert.NotNull(store);
        Assert.Equal(["Arispex"], store!.Users);
    }

    private static (PluginConfigService configService, WhitelistService service) CreateServices()
    {
        var root = Path.Combine(Path.GetTempPath(), "NextBotAdapter.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var configService = new PluginConfigService(root);
        configService.EnsureConfigComplete();
        return (configService, new WhitelistService(configService));
    }
}
