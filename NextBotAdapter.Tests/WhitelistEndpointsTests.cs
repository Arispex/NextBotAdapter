using NextBotAdapter.Infrastructure;
using NextBotAdapter.Models;
using NextBotAdapter.Models.Responses;
using NextBotAdapter.Rest;
using NextBotAdapter.Services;
using Rests;

namespace NextBotAdapter.Tests;

public sealed class WhitelistEndpointsTests
{
    [Fact]
    public void List_ShouldReturnWhitelistEntries()
    {
        var service = new FakeWhitelistService(new WhitelistListResponse(["Arispex", "NextBot"]));

        var result = Assert.IsType<RestObject>(WhitelistEndpoints.List(service));

        Assert.Equal("200", result.Status);
        var response = Assert.IsType<WhitelistListResponse>(result["data"]);
        Assert.Equal(["Arispex", "NextBot"], response.Users);
    }

    [Fact]
    public void Add_ShouldReturnBadRequestWhenUserMissing()
    {
        var result = Assert.IsType<RestObject>(WhitelistEndpoints.Add(null, new FakeWhitelistService()));

        Assert.Equal("400", result.Status);
        var error = Assert.IsType<ApiError>(result["error"]);
        Assert.Equal("whitelist_user_invalid", error.Code);
    }

    [Fact]
    public void Add_ShouldReturnConflictWhenUserAlreadyExists()
    {
        var service = new FakeWhitelistService(addError: new UserLookupError("whitelist_user_exists", "User already exists in whitelist."));

        var result = Assert.IsType<RestObject>(WhitelistEndpoints.Add("Arispex", service));

        Assert.Equal("409", result.Status);
        var error = Assert.IsType<ApiError>(result["error"]);
        Assert.Equal("whitelist_user_exists", error.Code);
    }

    [Fact]
    public void Remove_ShouldReturnNotFoundWhenUserMissingFromWhitelist()
    {
        var service = new FakeWhitelistService(removeError: new UserLookupError("whitelist_user_not_found", "User not found in whitelist."));

        var result = Assert.IsType<RestObject>(WhitelistEndpoints.Remove("Missing", service));

        Assert.Equal("404", result.Status);
        var error = Assert.IsType<ApiError>(result["error"]);
        Assert.Equal("whitelist_user_not_found", error.Code);
    }

    private sealed class FakeWhitelistService : IWhitelistService
    {
        private readonly WhitelistListResponse _list;
        private readonly UserLookupError? _addError;
        private readonly UserLookupError? _removeError;

        public FakeWhitelistService(WhitelistListResponse? list = null, UserLookupError? addError = null, UserLookupError? removeError = null)
        {
            _list = list ?? new WhitelistListResponse([]);
            _addError = addError;
            _removeError = removeError;
        }

        public WhitelistSettings Settings => new(true, "Denied", true);

        public IReadOnlyList<string> GetAll() => _list.Users;

        public bool IsWhitelisted(string user) => _list.Users.Contains(user);

        public bool TryAdd(string user, out UserLookupError? error)
        {
            error = _addError;
            return error is null;
        }

        public bool TryRemove(string user, out UserLookupError? error)
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
