using System.IO;
using Newtonsoft.Json;
using NextBotAdapter.Models;
using NextBotAdapter.Services;

namespace NextBotAdapter.Tests;

public sealed class WhitelistFileServiceTests
{
    private static readonly JsonSerializerSettings JsonSettings = new() { Formatting = Formatting.Indented };

    [Fact]
    public void LoadWhitelist_ShouldFallbackToEmptyWhenJsonIsInvalid()
    {
        var (_, fileService) = CreateServices();
        const string invalidJson = "{invalid json}";
        File.WriteAllText(fileService.WhitelistFilePath, invalidJson);

        var store = fileService.LoadWhitelist();

        Assert.Equal(WhitelistStore.Empty, store);
        Assert.Equal(invalidJson, File.ReadAllText(fileService.WhitelistFilePath));
    }

    [Fact]
    public void WhitelistFilePath_ShouldUseCapitalizedFileName()
    {
        var (_, fileService) = CreateServices();

        Assert.EndsWith("Whitelist.json", fileService.WhitelistFilePath);
    }

    private static (PluginConfigService configService, WhitelistFileService fileService) CreateServices()
    {
        var root = Path.Combine(Path.GetTempPath(), "NextBotAdapter.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return (new PluginConfigService(root), new WhitelistFileService(root));
    }
}
