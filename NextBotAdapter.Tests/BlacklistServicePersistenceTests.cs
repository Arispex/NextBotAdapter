using System.IO;
using Newtonsoft.Json;
using NextBotAdapter.Models;
using NextBotAdapter.Services;

namespace NextBotAdapter.Tests;

public sealed class BlacklistServicePersistenceTests
{
    private static readonly JsonSerializerSettings JsonSettings = new() { Formatting = Formatting.Indented };

    [Fact]
    public void FilePath_UsesDataDirectory()
    {
        var root = CreateTempRoot();
        var configService = new PluginConfigService(root);
        WriteDefaultConfig(configService);
        var service = new BlacklistService(configService);

        Assert.Equal(Path.Combine(root, "Data", "Blacklist.json"), service.FilePath);
    }

    [Fact]
    public void Load_CreatesEmptyFile_WhenNotExists()
    {
        var root = CreateTempRoot();
        var configService = new PluginConfigService(root);
        WriteDefaultConfig(configService);
        var service = new BlacklistService(configService);

        var store = service.Load();

        Assert.Empty(store.Entries);
        Assert.True(File.Exists(service.FilePath));
    }

    [Fact]
    public void TryAdd_PersistsToDisk()
    {
        var root = CreateTempRoot();
        var configService = new PluginConfigService(root);
        WriteDefaultConfig(configService);
        var service = new BlacklistService(configService);

        service.TryAdd("Arispex", "作弊", out _);

        var raw = File.ReadAllText(service.FilePath!);
        var store = JsonConvert.DeserializeObject<BlacklistStore>(raw, JsonSettings);
        Assert.Single(store!.Entries);
        Assert.Equal("Arispex", store.Entries[0].Username);
        Assert.Equal("作弊", store.Entries[0].Reason);
    }

    [Fact]
    public void TryRemove_PersistsToDisk()
    {
        var root = CreateTempRoot();
        var configService = new PluginConfigService(root);
        WriteDefaultConfig(configService);
        var service = new BlacklistService(configService);
        service.TryAdd("Arispex", "作弊", out _);

        service.TryRemove("Arispex", out _);

        var raw = File.ReadAllText(service.FilePath!);
        var store = JsonConvert.DeserializeObject<BlacklistStore>(raw, JsonSettings);
        Assert.Empty(store!.Entries);
    }

    [Fact]
    public void Load_FallsBackToEmpty_OnInvalidJson()
    {
        var root = CreateTempRoot();
        var configService = new PluginConfigService(root);
        WriteDefaultConfig(configService);
        var service = new BlacklistService(configService);
        File.WriteAllText(service.FilePath!, "not json");

        var store = service.Load();

        Assert.Empty(store.Entries);
    }

    [Fact]
    public void Reload_ReloadsSettingsAndData()
    {
        var root = CreateTempRoot();
        var configService = new PluginConfigService(root);
        WriteDefaultConfig(configService);
        var service = new BlacklistService(configService);
        service.TryAdd("Arispex", "作弊", out _);

        var reloaded = service.Reload();

        Assert.Single(reloaded.Entries);
    }

    private static string CreateTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "NextBotAdapter.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void WriteDefaultConfig(PluginConfigService configService)
    {
        Directory.CreateDirectory(Path.Combine(configService.ConfigDirectoryPath, "Data"));
        File.WriteAllText(configService.ConfigFilePath,
            JsonConvert.SerializeObject(NextBotAdapterConfig.Default, JsonSettings));
    }
}
