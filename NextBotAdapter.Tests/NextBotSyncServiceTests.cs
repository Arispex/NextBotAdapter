using NextBotAdapter.Models;
using NextBotAdapter.Models.Responses;
using NextBotAdapter.Services;

namespace NextBotAdapter.Tests;

public sealed class NextBotSyncServiceTests
{
    private static readonly WhitelistSettings WlSettings = new(true, "Denied");
    private static readonly BlacklistSettings BlSettings = new(true, "Banned");

    #region SyncWhitelist

    [Fact]
    public void SyncWhitelist_AddsMissingUsers()
    {
        var wl = new WhitelistService(WlSettings, new WhitelistStore([]));
        var bl = new BlacklistService(BlSettings, BlacklistStore.Empty);
        var sync = new NextBotSyncService(wl, bl);

        var result = sync.SyncWhitelist([
            new NextBotUserEntry("Alice", false, ""),
            new NextBotUserEntry("Bob", false, ""),
        ]);

        Assert.Equal(2, result.Added);
        Assert.Equal(0, result.Removed);
        Assert.Contains("Alice", wl.GetAll());
        Assert.Contains("Bob", wl.GetAll());
    }

    [Fact]
    public void SyncWhitelist_RemovesExtraUsers()
    {
        var wl = new WhitelistService(WlSettings, new WhitelistStore(["Alice", "Charlie"]));
        var bl = new BlacklistService(BlSettings, BlacklistStore.Empty);
        var sync = new NextBotSyncService(wl, bl);

        var result = sync.SyncWhitelist([
            new NextBotUserEntry("Alice", false, ""),
        ]);

        Assert.Equal(0, result.Added);
        Assert.Equal(1, result.Removed);
        Assert.Single(wl.GetAll());
        Assert.Contains("Alice", wl.GetAll());
    }

    [Fact]
    public void SyncWhitelist_NoOpWhenInSync()
    {
        var wl = new WhitelistService(WlSettings, new WhitelistStore(["Alice", "Bob"]));
        var bl = new BlacklistService(BlSettings, BlacklistStore.Empty);
        var sync = new NextBotSyncService(wl, bl);

        var result = sync.SyncWhitelist([
            new NextBotUserEntry("Alice", false, ""),
            new NextBotUserEntry("Bob", false, ""),
        ]);

        Assert.Equal(0, result.Added);
        Assert.Equal(0, result.Removed);
    }

    [Fact]
    public void SyncWhitelist_IgnoresBannedUsers()
    {
        var wl = new WhitelistService(WlSettings, new WhitelistStore([]));
        var bl = new BlacklistService(BlSettings, BlacklistStore.Empty);
        var sync = new NextBotSyncService(wl, bl);

        var result = sync.SyncWhitelist([
            new NextBotUserEntry("Alice", false, ""),
            new NextBotUserEntry("Bob", true, "cheating"),
        ]);

        Assert.Equal(1, result.Added);
        Assert.Contains("Alice", wl.GetAll());
        Assert.DoesNotContain("Bob", wl.GetAll());
    }

    [Fact]
    public void SyncWhitelist_CaseInsensitiveMatch()
    {
        var wl = new WhitelistService(WlSettings, new WhitelistStore(["arispex"]));
        var bl = new BlacklistService(BlSettings, BlacklistStore.Empty);
        var sync = new NextBotSyncService(wl, bl);

        var result = sync.SyncWhitelist([
            new NextBotUserEntry("Arispex", false, ""),
        ]);

        Assert.Equal(0, result.Added);
        Assert.Equal(0, result.Removed);
    }

    [Fact]
    public void SyncWhitelist_ClearsAllWhenNextBotEmpty()
    {
        var wl = new WhitelistService(WlSettings, new WhitelistStore(["Alice"]));
        var bl = new BlacklistService(BlSettings, BlacklistStore.Empty);
        var sync = new NextBotSyncService(wl, bl);

        var result = sync.SyncWhitelist([]);

        Assert.Equal(0, result.Added);
        Assert.Equal(1, result.Removed);
        Assert.Empty(wl.GetAll());
    }

    #endregion

    #region SyncBlacklist

    [Fact]
    public void SyncBlacklist_AddsBannedUsersWithReason()
    {
        var wl = new WhitelistService(WlSettings, new WhitelistStore([]));
        var bl = new BlacklistService(BlSettings, BlacklistStore.Empty);
        var sync = new NextBotSyncService(wl, bl);

        var result = sync.SyncBlacklist([
            new NextBotUserEntry("Bob", true, "cheating"),
        ]);

        Assert.Equal(1, result.Added);
        Assert.Equal(0, result.Removed);
        var entries = bl.GetAll();
        Assert.Single(entries);
        Assert.Equal("Bob", entries[0].Username);
        Assert.Equal("cheating", entries[0].Reason);
    }

    [Fact]
    public void SyncBlacklist_RemovesExtraEntries()
    {
        var wl = new WhitelistService(WlSettings, new WhitelistStore([]));
        var bl = new BlacklistService(BlSettings, new BlacklistStore([new BlacklistEntry("Charlie", "griefing")]));
        var sync = new NextBotSyncService(wl, bl);

        var result = sync.SyncBlacklist([
            new NextBotUserEntry("Bob", true, "cheating"),
        ]);

        Assert.Equal(1, result.Added);
        Assert.Equal(1, result.Removed);
        var entries = bl.GetAll();
        Assert.Single(entries);
        Assert.Equal("Bob", entries[0].Username);
    }

    [Fact]
    public void SyncBlacklist_IgnoresNonBannedUsers()
    {
        var wl = new WhitelistService(WlSettings, new WhitelistStore([]));
        var bl = new BlacklistService(BlSettings, BlacklistStore.Empty);
        var sync = new NextBotSyncService(wl, bl);

        var result = sync.SyncBlacklist([
            new NextBotUserEntry("Alice", false, ""),
        ]);

        Assert.Equal(0, result.Added);
        Assert.Empty(bl.GetAll());
    }

    [Fact]
    public void SyncBlacklist_CaseInsensitiveMatch()
    {
        var wl = new WhitelistService(WlSettings, new WhitelistStore([]));
        var bl = new BlacklistService(BlSettings, new BlacklistStore([new BlacklistEntry("bob", "cheating")]));
        var sync = new NextBotSyncService(wl, bl);

        var result = sync.SyncBlacklist([
            new NextBotUserEntry("Bob", true, "cheating"),
        ]);

        Assert.Equal(0, result.Added);
        Assert.Equal(0, result.Removed);
    }

    [Fact]
    public void SyncBlacklist_NoOpWhenInSync()
    {
        var wl = new WhitelistService(WlSettings, new WhitelistStore([]));
        var bl = new BlacklistService(BlSettings, new BlacklistStore([new BlacklistEntry("Bob", "cheating")]));
        var sync = new NextBotSyncService(wl, bl);

        var result = sync.SyncBlacklist([
            new NextBotUserEntry("Bob", true, "cheating"),
        ]);

        Assert.Equal(0, result.Added);
        Assert.Equal(0, result.Removed);
    }

    [Fact]
    public void SyncBlacklist_InvokesBanService_WhenEntryAdded()
    {
        var wl = new WhitelistService(WlSettings, new WhitelistStore([]));
        var bl = new BlacklistService(BlSettings, BlacklistStore.Empty);
        var banService = new FakeTShockUserBanService();
        var sync = new NextBotSyncService(wl, bl, banService);

        sync.SyncBlacklist([
            new NextBotUserEntry("Bob", true, "cheating"),
        ]);

        Assert.Single(banService.BanCalls);
        Assert.Equal(("Bob", "cheating"), banService.BanCalls[0]);
        Assert.Empty(banService.UnbanCalls);
    }

    [Fact]
    public void SyncBlacklist_InvokesUnbanService_WhenEntryRemoved()
    {
        var wl = new WhitelistService(WlSettings, new WhitelistStore([]));
        var bl = new BlacklistService(BlSettings, new BlacklistStore([new BlacklistEntry("Charlie", "griefing")]));
        var banService = new FakeTShockUserBanService();
        var sync = new NextBotSyncService(wl, bl, banService);

        sync.SyncBlacklist([]);

        Assert.Single(banService.UnbanCalls);
        Assert.Equal("Charlie", banService.UnbanCalls[0]);
        Assert.Empty(banService.BanCalls);
    }

    [Fact]
    public void SyncBlacklist_DoesNotInvokeBanService_WhenInSync()
    {
        var wl = new WhitelistService(WlSettings, new WhitelistStore([]));
        var bl = new BlacklistService(BlSettings, new BlacklistStore([new BlacklistEntry("Bob", "cheating")]));
        var banService = new FakeTShockUserBanService();
        var sync = new NextBotSyncService(wl, bl, banService);

        sync.SyncBlacklist([
            new NextBotUserEntry("Bob", true, "cheating"),
        ]);

        Assert.Empty(banService.BanCalls);
        Assert.Empty(banService.UnbanCalls);
    }

    #endregion
}
