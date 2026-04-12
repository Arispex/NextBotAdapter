using NextBotAdapter.Models;
using NextBotAdapter.Services;

namespace NextBotAdapter.Tests;

public sealed class BlacklistServiceTests
{
    private static BlacklistService CreateService(
        bool enabled = true,
        string denyMessage = "你已被封禁，原因：{reason}。如有疑问，请联系管理员。",
        params BlacklistEntry[] entries)
    {
        var settings = new BlacklistSettings(enabled, denyMessage);
        var store = new BlacklistStore(entries);
        return new BlacklistService(settings, store);
    }

    [Fact]
    public void IsBlacklisted_ReturnsFalse_WhenDisabled()
    {
        var service = CreateService(enabled: false, entries: new BlacklistEntry("Arispex", "作弊"));

        Assert.False(service.IsBlacklisted("Arispex"));
    }

    [Fact]
    public void IsBlacklisted_ReturnsTrue_WhenUserInList()
    {
        var service = CreateService(entries: new BlacklistEntry("Arispex", "作弊"));

        Assert.True(service.IsBlacklisted("Arispex"));
    }

    [Fact]
    public void IsBlacklisted_IsCaseInsensitive()
    {
        var service = CreateService(entries: new BlacklistEntry("Arispex", "作弊"));

        Assert.True(service.IsBlacklisted("Arispex"));
        Assert.True(service.IsBlacklisted("arispex"));
        Assert.True(service.IsBlacklisted("ARISPEX"));
    }

    [Fact]
    public void IsBlacklisted_ReturnsFalse_WhenUserNotInList()
    {
        var service = CreateService(entries: new BlacklistEntry("Arispex", "作弊"));

        Assert.False(service.IsBlacklisted("OtherPlayer"));
    }

    [Fact]
    public void TryAdd_Succeeds_ForNewUser()
    {
        var service = CreateService();

        var result = service.TryAdd("Arispex", "作弊", out var error);

        Assert.True(result);
        Assert.Null(error);
        Assert.True(service.IsBlacklisted("Arispex"));
    }

    [Fact]
    public void TryAdd_Fails_ForDuplicateUser()
    {
        var service = CreateService(entries: new BlacklistEntry("Arispex", "作弊"));

        var result = service.TryAdd("Arispex", "再次作弊", out var error);

        Assert.False(result);
        Assert.Equal("User already exists in blacklist.", error);
    }

    [Fact]
    public void TryAdd_Fails_ForDuplicateUser_CaseInsensitive()
    {
        var service = CreateService(entries: new BlacklistEntry("Arispex", "作弊"));

        var result = service.TryAdd("arispex", "再次作弊", out var error);

        Assert.False(result);
        Assert.Equal("User already exists in blacklist.", error);
    }

    [Fact]
    public void TryAdd_Fails_ForEmptyUser()
    {
        var service = CreateService();

        var result = service.TryAdd("", "reason", out var error);

        Assert.False(result);
        Assert.Equal("Blacklist user is invalid.", error);
    }

    [Fact]
    public void TryRemove_Succeeds_ForExistingUser()
    {
        var service = CreateService(entries: new BlacklistEntry("Arispex", "作弊"));

        var result = service.TryRemove("Arispex", out var error);

        Assert.True(result);
        Assert.Null(error);
        Assert.False(service.IsBlacklisted("Arispex"));
    }

    [Fact]
    public void TryRemove_Succeeds_CaseInsensitive()
    {
        var service = CreateService(entries: new BlacklistEntry("Arispex", "作弊"));

        var result = service.TryRemove("arispex", out var error);

        Assert.True(result);
        Assert.Null(error);
    }

    [Fact]
    public void TryRemove_Fails_ForMissingUser()
    {
        var service = CreateService();

        var result = service.TryRemove("Arispex", out var error);

        Assert.False(result);
        Assert.Equal("User not found in blacklist.", error);
    }

    [Fact]
    public void TryRemove_Fails_ForEmptyUser()
    {
        var service = CreateService();

        var result = service.TryRemove("", out var error);

        Assert.False(result);
        Assert.Equal("Blacklist user is invalid.", error);
    }

    [Fact]
    public void TryValidateJoin_Allows_WhenDisabled()
    {
        var service = CreateService(enabled: false, entries: new BlacklistEntry("Arispex", "作弊"));

        var result = service.TryValidateJoin("Arispex", out var reason);

        Assert.True(result);
        Assert.Null(reason);
    }

    [Fact]
    public void TryValidateJoin_Allows_WhenNotInBlacklist()
    {
        var service = CreateService();

        var result = service.TryValidateJoin("Arispex", out var reason);

        Assert.True(result);
        Assert.Null(reason);
    }

    [Fact]
    public void TryValidateJoin_Denies_WhenInBlacklist()
    {
        var service = CreateService(entries: new BlacklistEntry("Arispex", "作弊"));

        var result = service.TryValidateJoin("Arispex", out var reason);

        Assert.False(result);
        Assert.Equal("你已被封禁，原因：作弊。如有疑问，请联系管理员。", reason);
    }

    [Fact]
    public void TryValidateJoin_ReplacesReasonPlaceholder()
    {
        var service = CreateService(
            denyMessage: "封禁原因：{reason}",
            entries: new BlacklistEntry("Arispex", "使用外挂"));

        var result = service.TryValidateJoin("Arispex", out var reason);

        Assert.False(result);
        Assert.Equal("封禁原因：使用外挂", reason);
    }

    [Fact]
    public void GetAll_ReturnsAllEntries()
    {
        var service = CreateService(entries:
        [
            new BlacklistEntry("Player1", "reason1"),
            new BlacklistEntry("Player2", "reason2"),
        ]);

        var all = service.GetAll();

        Assert.Equal(2, all.Count);
        Assert.Equal("Player1", all[0].Username);
        Assert.Equal("reason1", all[0].Reason);
        Assert.Equal("Player2", all[1].Username);
        Assert.Equal("reason2", all[1].Reason);
    }
}
