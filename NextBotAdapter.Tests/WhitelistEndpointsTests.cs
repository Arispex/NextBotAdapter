using NextBotAdapter.Infrastructure;
using NextBotAdapter.Models;
using NextBotAdapter.Rest;
using NextBotAdapter.Services;
using Rests;

namespace NextBotAdapter.Tests;

public sealed class WhitelistEndpointsTests
{
    [Fact]
    public void List_ShouldReturnWhitelistEntries()
    {
        var service = new FakeWhitelistService(["Arispex", "NextBot"]);

        var result = Assert.IsType<RestObject>(WhitelistEndpoints.List(service));

        Assert.Equal("200", result.Status);
        var users = Assert.IsAssignableFrom<IReadOnlyList<string>>(result["users"]);
        Assert.Equal(["Arispex", "NextBot"], users);
    }

    [Fact]
    public void Add_ShouldReturnResponseOnSuccess()
    {
        var result = Assert.IsType<RestObject>(WhitelistEndpoints.Add("Arispex", new FakeWhitelistService()));

        Assert.Equal("200", result.Status);
        Assert.Equal("User 'Arispex' has been added to the whitelist.", result["response"]);
    }

    [Fact]
    public void Add_ShouldReturnBadRequestWhenUserMissing()
    {
        var result = Assert.IsType<RestObject>(WhitelistEndpoints.Add(null, new FakeWhitelistService()));

        Assert.Equal("400", result.Status);
        Assert.Equal("Whitelist user is invalid.", result.Error);
    }

    [Fact]
    public void Add_ShouldReturnBadRequestWhenUserAlreadyExists()
    {
        var service = new FakeWhitelistService(addError: "User already exists in whitelist.");

        var result = Assert.IsType<RestObject>(WhitelistEndpoints.Add("Arispex", service));

        Assert.Equal("400", result.Status);
        Assert.Equal("User already exists in whitelist.", result.Error);
    }

    [Fact]
    public void Remove_ShouldReturnResponseOnSuccess()
    {
        var result = Assert.IsType<RestObject>(WhitelistEndpoints.Remove("Arispex", new FakeWhitelistService()));

        Assert.Equal("200", result.Status);
        Assert.Equal("User 'Arispex' has been removed from the whitelist.", result["response"]);
    }

    [Fact]
    public void Remove_ShouldReturnBadRequestWhenUserMissingFromWhitelist()
    {
        var service = new FakeWhitelistService(removeError: "User not found in whitelist.");

        var result = Assert.IsType<RestObject>(WhitelistEndpoints.Remove("Missing", service));

        Assert.Equal("400", result.Status);
        Assert.Equal("User not found in whitelist.", result.Error);
    }

    private sealed class FakeWhitelistService : IWhitelistService
    {
        private readonly IReadOnlyList<string> _users;
        private readonly string? _addError;
        private readonly string? _removeError;

        public FakeWhitelistService(IReadOnlyList<string>? users = null, string? addError = null, string? removeError = null)
        {
            _users = users ?? [];
            _addError = addError;
            _removeError = removeError;
        }

        public WhitelistSettings Settings => new(true, "Denied", true);

        public IReadOnlyList<string> GetAll() => _users;

        public bool IsWhitelisted(string user) => _users.Contains(user);

        public bool TryAdd(string user, out string? error)
        {
            error = _addError;
            return error is null;
        }

        public bool TryRemove(string user, out string? error)
        {
            error = _removeError;
            return error is null;
        }

        public bool TryValidateJoin(string user, out string? denialReason)
        {
            denialReason = null;
            return true;
        }
    }
}
