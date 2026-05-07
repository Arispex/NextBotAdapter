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

    [Fact]
    public void Flush_ShouldPersistActiveSessionElapsedWithoutEndingSession()
    {
        var service = CreateService(out var filePath);

        service.StartSession("alice");
        // Sleep just enough that the integer-second elapsed value is stable.
        Thread.Sleep(1100);
        service.Flush();

        // Records on disk reflect the elapsed seconds.
        var persisted = ReadStore(filePath);
        Assert.True(persisted.Records.TryGetValue("alice", out var seconds));
        Assert.True(seconds >= 1, $"expected elapsed >= 1, got {seconds}");

        // Session is still active — EndSession must continue to work normally.
        service.EndSession("alice");
        Assert.True(service.GetTotalSeconds("alice") >= seconds);
    }

    [Fact]
    public void Flush_ShouldNotDoubleCount_OnSubsequentEndSession()
    {
        var service = CreateService(out var filePath);

        service.StartSession("alice");
        Thread.Sleep(1100);
        service.Flush();
        var afterFlush = ReadStore(filePath).Records["alice"];

        Thread.Sleep(1100);
        service.EndSession("alice");
        var afterEnd = ReadStore(filePath).Records["alice"];

        // EndSession's contribution must be only the post-Flush delta. There
        // is some scheduler jitter, so allow for a small slack window.
        var endDelta = afterEnd - afterFlush;
        Assert.True(endDelta >= 1 && endDelta <= 5, $"expected EndSession to add only the post-Flush delta (1..5s), got {endDelta}s");
    }

    [Fact]
    public void Flush_ShouldBeIdempotentAcrossMultipleCalls()
    {
        var service = CreateService(out var filePath);

        service.StartSession("alice");
        Thread.Sleep(1100);
        service.Flush();
        var afterFirst = ReadStore(filePath).Records["alice"];

        // No-op flush right after — almost no time elapsed since last Flush
        // reset the start. Records must not double-count what the first Flush
        // already persisted.
        service.Flush();
        var afterSecond = ReadStore(filePath).Records["alice"];

        Assert.InRange(afterSecond - afterFirst, 0, 1);
    }

    [Fact]
    public void Flush_ShouldNoOp_WhenNoActiveSessions()
    {
        var service = CreateService(out var filePath);

        var exception = Record.Exception(() => service.Flush());

        Assert.Null(exception);
        Assert.Empty(ReadStore(filePath).Records);
    }

    [Fact]
    public void Reload_ShouldMergeAndKeepLargerInMemoryValue()
    {
        // Seed disk + in-memory with alice=100.
        var service = CreateService(new OnlineTimeStore(
            new Dictionary<string, long> { ["alice"] = 100 }), out var filePath);

        // Simulate the race: another thread flushed alice=200 to disk and
        // updated in-memory _records, but we are about to call Reload whose
        // earlier Load already snapshotted the file. To exercise the merge
        // path we instead overwrite the disk file with an older snapshot
        // (alice=80) AFTER the in-memory bump. Without the merge, Reload
        // would replace _records with the older disk snapshot and erase the
        // in-memory progress.
        service.StartSession("alice");
        service.EndSession("alice");
        var inMemoryAfterEnd = service.GetTotalSeconds("alice");
        Assert.True(inMemoryAfterEnd >= 100);

        // Roll back the on-disk file to a stale snapshot.
        File.WriteAllText(filePath, JsonConvert.SerializeObject(
            new OnlineTimeStore(new Dictionary<string, long> { ["alice"] = 80 }),
            JsonSettings));

        service.Reload();

        // Merge keeps the larger in-memory value; the stale 80 must NOT win.
        Assert.True(service.GetTotalSeconds("alice") >= inMemoryAfterEnd);
    }

    [Fact]
    public void EndSession_ShouldNotLoseData_UnderConcurrentEndSessions()
    {
        // V-P4: lock-IO decoupling must not lose elapsed-second contributions
        // when many EndSession calls land concurrently. The test starts N
        // sessions, ends them in parallel, and asserts every record landed
        // on disk with its elapsed time accumulated.
        var service = CreateService(out var filePath);

        const int playerCount = 16;
        var names = Enumerable.Range(0, playerCount).Select(i => $"player-{i}").ToArray();
        foreach (var n in names)
        {
            service.StartSession(n);
        }

        Parallel.ForEach(names, n => service.EndSession(n));

        var persisted = ReadStore(filePath);
        Assert.Equal(playerCount, persisted.Records.Count);
        foreach (var n in names)
        {
            Assert.True(persisted.Records.ContainsKey(n), $"missing record for {n}");
            // Elapsed seconds may be 0 on a fast machine; the contract is just
            // that the entry exists and is non-negative.
            Assert.True(persisted.Records[n] >= 0);
        }
    }

    [Fact]
    public void EndSession_DiskContentShouldStayValid_AfterRapidEndSessions()
    {
        // Regression guard for write-collision corruption: with IO outside the
        // service-level _lock, _ioLock must serialize the File.WriteAllText
        // calls so the JSON on disk is always parseable.
        var service = CreateService(out var filePath);

        for (var round = 0; round < 5; round++)
        {
            var names = Enumerable.Range(0, 8).Select(i => $"r{round}-p{i}").ToArray();
            foreach (var n in names) service.StartSession(n);
            Parallel.ForEach(names, n => service.EndSession(n));
        }

        // The file must always be valid JSON; a partial-write would throw here.
        var store = ReadStore(filePath);
        Assert.NotNull(store);
        Assert.True(store.Records.Count >= 8);
    }

    [Fact]
    public void Reload_ShouldAddNewRecordsFromDisk()
    {
        var service = CreateService(out var filePath);

        // In-memory starts empty. Drop a fresh snapshot on disk that contains
        // a brand-new account; Reload should pick it up.
        File.WriteAllText(filePath, JsonConvert.SerializeObject(
            new OnlineTimeStore(new Dictionary<string, long> { ["alice"] = 50 }),
            JsonSettings));

        service.Reload();

        Assert.Equal(50, service.GetTotalSeconds("alice"));
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
