using NextBotAdapter.Models;
using NextBotAdapter.Services;
using System.IO;

namespace NextBotAdapter.Tests;

public sealed class OnlineTimeServiceTests
{
    [Fact]
    public void GetTotalSeconds_ShouldReturnZeroForUnknownPlayer()
    {
        var service = CreateService();

        Assert.Equal(0, service.GetTotalSeconds("unknown"));
    }

    [Fact]
    public void GetTotalSeconds_ShouldReturnPersistedSeconds()
    {
        var fileService = new FakeOnlineTimeFileService(new OnlineTimeStore(
            new Dictionary<string, long> { ["alice"] = 500 }));
        var service = new OnlineTimeService(fileService);

        Assert.Equal(500, service.GetTotalSeconds("alice"));
    }

    [Fact]
    public void EndSession_ShouldAddElapsedSecondsToPersistedRecord()
    {
        var fileService = new FakeOnlineTimeFileService();
        var service = new OnlineTimeService(fileService);

        service.StartSession("alice");
        service.EndSession("alice");

        Assert.True(service.GetTotalSeconds("alice") >= 0);
        Assert.NotNull(fileService.LastSaved);
        Assert.True(fileService.LastSaved!.Records.ContainsKey("alice"));
    }

    [Fact]
    public void EndSession_ShouldAccumulateAcrossMultipleSessions()
    {
        var fileService = new FakeOnlineTimeFileService(new OnlineTimeStore(
            new Dictionary<string, long> { ["alice"] = 100 }));
        var service = new OnlineTimeService(fileService);

        service.StartSession("alice");
        service.EndSession("alice");

        Assert.True(service.GetTotalSeconds("alice") >= 100);
    }

    [Fact]
    public void EndSession_ShouldBeNoOpIfSessionNotStarted()
    {
        var fileService = new FakeOnlineTimeFileService();
        var service = new OnlineTimeService(fileService);

        service.EndSession("alice");

        Assert.Equal(0, service.GetTotalSeconds("alice"));
        Assert.Null(fileService.LastSaved);
    }

    [Fact]
    public void GetTotalSeconds_ShouldIncludeActiveSessionTime()
    {
        var service = CreateService();

        service.StartSession("alice");
        var total = service.GetTotalSeconds("alice");

        Assert.True(total >= 0);
    }

    [Fact]
    public void GetAllRecords_ShouldReturnEmptyWhenNoData()
    {
        var service = CreateService();

        var records = service.GetAllRecords();

        Assert.Empty(records);
    }

    [Fact]
    public void GetAllRecords_ShouldReturnAllPlayersWithPersistedTime()
    {
        var fileService = new FakeOnlineTimeFileService(new OnlineTimeStore(
            new Dictionary<string, long> { ["alice"] = 100, ["bob"] = 500 }));
        var service = new OnlineTimeService(fileService);

        var records = service.GetAllRecords();

        Assert.Equal(2, records.Count);
        Assert.Equal("bob", records[0].Username);
        Assert.Equal(500, records[0].OnlineSeconds);
    }

    [Fact]
    public void PersistAllSessions_ShouldSaveActiveSessionsAndClearThem()
    {
        var fileService = new FakeOnlineTimeFileService();
        var service = new OnlineTimeService(fileService);

        service.StartSession("alice");
        service.PersistAllSessions();

        Assert.NotNull(fileService.LastSaved);
        Assert.True(fileService.LastSaved!.Records.ContainsKey("alice"));
        Assert.Equal(0, service.GetTotalSeconds("alice"));
    }

    private static OnlineTimeService CreateService()
        => new(new FakeOnlineTimeFileService());

    private sealed class FakeOnlineTimeFileService : IOnlineTimeFileService
    {
        private readonly OnlineTimeStore _initialStore;

        public FakeOnlineTimeFileService(OnlineTimeStore? store = null)
        {
            _initialStore = store ?? OnlineTimeStore.Empty;
        }

        public OnlineTimeStore? LastSaved { get; private set; }

        public OnlineTimeStore Load() => _initialStore;

        public void Save(OnlineTimeStore store)
        {
            LastSaved = store;
        }
    }
}
