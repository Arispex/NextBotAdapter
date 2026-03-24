using NextBotAdapter.Models;
using NextBotAdapter.Services;

namespace NextBotAdapter.Tests;

public sealed class WhitelistServiceTests
{
    [Fact]
    public void IsWhitelisted_ShouldMatchExactNameWhenCaseSensitive()
    {
        var service = new WhitelistService(new WhitelistSettings(true, "Denied", true), new WhitelistStore(["Arispex"]));

        Assert.True(service.IsWhitelisted("Arispex"));
        Assert.False(service.IsWhitelisted("arispex"));
    }

    [Fact]
    public void IsWhitelisted_ShouldIgnoreCaseWhenConfigured()
    {
        var service = new WhitelistService(new WhitelistSettings(true, "Denied", false), new WhitelistStore(["Arispex"]));

        Assert.True(service.IsWhitelisted("arispex"));
    }

    [Fact]
    public void Add_ShouldRejectExistingName()
    {
        var service = new WhitelistService(new WhitelistSettings(true, "Denied", true), new WhitelistStore(["Arispex"]));

        var added = service.TryAdd("Arispex", out var error);

        Assert.False(added);
        Assert.NotNull(error);
        Assert.Equal("User already exists in whitelist.", error);
    }

    [Fact]
    public void Add_ShouldAppendNewName()
    {
        var service = new WhitelistService(new WhitelistSettings(true, "Denied", true), new WhitelistStore(["Arispex"]));

        var added = service.TryAdd("NextBot", out var error);

        Assert.True(added);
        Assert.Null(error);
        Assert.Equal(["Arispex", "NextBot"], service.GetAll());
    }

    [Fact]
    public void Remove_ShouldDeleteExistingName()
    {
        var service = new WhitelistService(new WhitelistSettings(true, "Denied", true), new WhitelistStore(["Arispex", "NextBot"]));

        var removed = service.TryRemove("NextBot", out var error);

        Assert.True(removed);
        Assert.Null(error);
        Assert.Equal(["Arispex"], service.GetAll());
    }

    [Fact]
    public void Remove_ShouldReturnErrorWhenNameDoesNotExist()
    {
        var service = new WhitelistService(new WhitelistSettings(true, "Denied", true), new WhitelistStore(["Arispex"]));

        var removed = service.TryRemove("Missing", out var error);

        Assert.False(removed);
        Assert.NotNull(error);
        Assert.Equal("User not found in whitelist.", error);
    }

    [Fact]
    public void ValidateJoin_ShouldRejectWhenWhitelistEnabledAndNameMissing()
    {
        var service = new WhitelistService(new WhitelistSettings(true, "Access denied", true), new WhitelistStore(["Arispex"]));

        var allowed = service.TryValidateJoin("NextBot", out var denialReason);

        Assert.False(allowed);
        Assert.Equal("Access denied", denialReason);
    }

    [Fact]
    public void ValidateJoin_ShouldAllowWhenWhitelistDisabled()
    {
        var service = new WhitelistService(new WhitelistSettings(false, "Access denied", true), new WhitelistStore([]));

        var allowed = service.TryValidateJoin("NextBot", out var denialReason);

        Assert.True(allowed);
        Assert.Null(denialReason);
    }
}
