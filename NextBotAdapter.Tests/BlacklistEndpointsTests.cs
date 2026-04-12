using NextBotAdapter.Models;
using NextBotAdapter.Rest;
using NextBotAdapter.Services;
using Rests;

namespace NextBotAdapter.Tests;

public sealed class BlacklistEndpointsTests
{
    [Fact]
    public void List_ReturnsAllEntries()
    {
        var service = CreateService(new BlacklistEntry("Arispex", "作弊"));

        var result = Assert.IsType<RestObject>(BlacklistEndpoints.List(service));

        Assert.Equal("200", result.Status);
        var entries = Assert.IsAssignableFrom<IReadOnlyList<BlacklistEntry>>(result["entries"]);
        Assert.Single(entries);
        Assert.Equal("Arispex", entries[0].Username);
    }

    [Fact]
    public void Add_ReturnsSuccess_ForNewUser()
    {
        var service = CreateService();

        var result = Assert.IsType<RestObject>(BlacklistEndpoints.Add("Arispex", "作弊", service));

        Assert.Equal("200", result.Status);
        Assert.Contains("Arispex", (string)result["response"]!);
    }

    [Fact]
    public void Add_ReturnsError_ForMissingUser()
    {
        var service = CreateService();

        var result = Assert.IsType<RestObject>(BlacklistEndpoints.Add(null, "reason", service));

        Assert.Equal("400", result.Status);
    }

    [Fact]
    public void Add_ReturnsError_ForMissingReason()
    {
        var service = CreateService();

        var result = Assert.IsType<RestObject>(BlacklistEndpoints.Add("Arispex", null, service));

        Assert.Equal("400", result.Status);
        Assert.Contains("reason", result.Error);
    }

    [Fact]
    public void Add_ReturnsError_ForDuplicateUser()
    {
        var service = CreateService(new BlacklistEntry("Arispex", "作弊"));

        var result = Assert.IsType<RestObject>(BlacklistEndpoints.Add("Arispex", "再次作弊", service));

        Assert.Equal("400", result.Status);
        Assert.Contains("already exists", result.Error);
    }

    [Fact]
    public void Remove_ReturnsSuccess_ForExistingUser()
    {
        var service = CreateService(new BlacklistEntry("Arispex", "作弊"));

        var result = Assert.IsType<RestObject>(BlacklistEndpoints.Remove("Arispex", service));

        Assert.Equal("200", result.Status);
        Assert.Contains("Arispex", (string)result["response"]!);
    }

    [Fact]
    public void Remove_ReturnsError_ForMissingUser()
    {
        var service = CreateService();

        var result = Assert.IsType<RestObject>(BlacklistEndpoints.Remove(null, service));

        Assert.Equal("400", result.Status);
    }

    [Fact]
    public void Remove_ReturnsError_ForNonExistentUser()
    {
        var service = CreateService();

        var result = Assert.IsType<RestObject>(BlacklistEndpoints.Remove("Arispex", service));

        Assert.Equal("400", result.Status);
        Assert.Contains("not found", result.Error);
    }

    private static IBlacklistService CreateService(params BlacklistEntry[] entries)
    {
        var settings = new BlacklistSettings(true, "你已被封禁，原因：{reason}。如有疑问，请联系管理员。");
        var store = new BlacklistStore(entries);
        return new BlacklistService(settings, store);
    }
}
