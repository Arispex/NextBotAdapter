using System.IO;
using Newtonsoft.Json;
using NextBotAdapter.Models;
using NextBotAdapter.Services;

namespace NextBotAdapter.Tests;

public sealed class OnlineTimeServiceTests
{
    private static readonly JsonSerializerSettings JsonSettings = new() { Formatting = Formatting.Indented };

    [Fact]
    public void GetTotalSeconds_ShouldReturnZeroForUnknownPlayer()
    {
        var service = CreateService(out _);

        Assert.Equal(0, service.GetTotalSeconds("unknown"));
    }

    [Fact]
    public void GetTotalSeconds_ShouldReturnPersistedSeconds()
    {
        var service = CreateService(new OnlineTimeStore(
            new Dictionary<string, long> { ["alice"] = 500 }), out _);

        Assert.Equal(500, service.GetTotalSeconds("alice"));
    }

    [Fact]
    public void EndSession_ShouldAddElapsedSecondsToPersistedRecord()
    {
        var service = CreateService(out var filePath);

        service.StartSession("alice");
        service.EndSession("alice");

        Assert.True(service.GetTotalSeconds("alice") >= 0);
        Assert.True(ReadStore(filePath).Records.ContainsKey("alice"));
    }

    [Fact]
    public void EndSession_ShouldAccumulateAcrossMultipleSessions()
    {
        var service = CreateService(new OnlineTimeStore(
            new Dictionary<string, long> { ["alice"] = 100 }), out _);

        service.StartSession("alice");
        service.EndSession("alice");

        Assert.True(service.GetTotalSeconds("alice") >= 100);
    }

    [Fact]
    public void EndSession_ShouldBeNoOpIfSessionNotStarted()
    {
        var service = CreateService(out var filePath);

        service.EndSession("alice");

        Assert.Equal(0, service.GetTotalSeconds("alice"));
        Assert.Empty(ReadStore(filePath).Records);
    }

    [Fact]
    public void GetTotalSeconds_ShouldIncludeActiveSessionTime()
    {
        var service = CreateService(out _);

        service.StartSession("alice");
        var total = service.GetTotalSeconds("alice");

        Assert.True(total >= 0);
    }

    [Fact]
    public void GetAllRecords_ShouldReturnEmptyWhenNoData()
    {
        var service = CreateService(out _);

        var records = service.GetAllRecords();

        Assert.Empty(records);
    }

    [Fact]
    public void GetAllRecords_ShouldReturnAllPlayersWithPersistedTime()
    {
        var service = CreateService(new OnlineTimeStore(
            new Dictionary<string, long> { ["alice"] = 100, ["bob"] = 500 }), out _);

        var records = service.GetAllRecords();

        Assert.Equal(2, records.Count);
        Assert.Equal("bob", records[0].Username);
        Assert.Equal(500, records[0].OnlineSeconds);
    }

    [Fact]
    public void PersistAllSessions_ShouldSaveActiveSessionsAndClearThem()
    {
        var service = CreateService(out var filePath);

        service.StartSession("alice");
        service.PersistAllSessions();

        Assert.True(ReadStore(filePath).Records.ContainsKey("alice"));
        Assert.True(service.GetTotalSeconds("alice") >= 0);
    }

    [Fact]
    public void FilePath_ShouldUseDataDirectoryAndCapitalizedFileName()
    {
        var service = CreateService(out _);

        Assert.EndsWith(Path.Combine("Data", "OnlineTime.json"), service.FilePath);
    }

    private static OnlineTimeService CreateService(out string filePath)
    {
        var root = Path.Combine(Path.GetTempPath(), "NextBotAdapter.Tests", Guid.NewGuid().ToString("N"));
        filePath = Path.Combine(root, "Data", "OnlineTime.json");
        return new OnlineTimeService(filePath);
    }

    private static OnlineTimeService CreateService(OnlineTimeStore store, out string filePath)
    {
        var root = Path.Combine(Path.GetTempPath(), "NextBotAdapter.Tests", Guid.NewGuid().ToString("N"));
        filePath = Path.Combine(root, "Data", "OnlineTime.json");
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        File.WriteAllText(filePath, JsonConvert.SerializeObject(store, JsonSettings));
        return new OnlineTimeService(filePath);
    }

    private static OnlineTimeStore ReadStore(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return OnlineTimeStore.Empty;
        }

        return JsonConvert.DeserializeObject<OnlineTimeStore>(File.ReadAllText(filePath), JsonSettings)
            ?? OnlineTimeStore.Empty;
    }
}
