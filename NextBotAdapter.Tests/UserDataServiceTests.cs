using NextBotAdapter.Models;
using NextBotAdapter.Services;

namespace NextBotAdapter.Tests;

public sealed class UserDataServiceTests
{
    [Fact]
    public void TryGetPlayerData_ShouldReturnMissingUserWhenUserIsBlank()
    {
        var service = new UserDataService(new FakeGateway());

        var success = service.TryGetPlayerData(" ", out var data, out var error);

        Assert.False(success);
        Assert.Null(error is null ? null : data);
        Assert.Equal("User cannot be empty.", error);
    }

    [Fact]
    public void TryGetPlayerData_ShouldReturnUserNotFoundWhenGatewayHasNoAccount()
    {
        var service = new UserDataService(new FakeGateway(accountLookupSucceeds: false));

        var success = service.TryGetPlayerData("alice", out _, out var error);

        Assert.False(success);
        Assert.Equal("User was not found.", error);
    }

    [Fact]
    public void TryGetPlayerData_ShouldReturnUserDataNotFoundWhenGatewayHasNoPlayerData()
    {
        var service = new UserDataService(new FakeGateway(playerDataLookupSucceeds: false));

        var success = service.TryGetPlayerData("alice", out _, out var error);

        Assert.False(success);
        Assert.Equal("Player data was not found.", error);
    }

    [Fact]
    public void TryGetPlayerData_ShouldReturnDataWhenGatewaySucceeds()
    {
        var expected = new object();
        var service = new UserDataService(new FakeGateway(playerData: expected));

        var success = service.TryGetPlayerData("alice", out var data, out var error);

        Assert.True(success);
        Assert.Same(expected, data);
        Assert.Null(error);
    }

    private sealed class FakeGateway : IUserDataGateway
    {
        private readonly bool _accountLookupSucceeds;
        private readonly bool _playerDataLookupSucceeds;
        private readonly object _playerData;

        public FakeGateway(bool accountLookupSucceeds = true, bool playerDataLookupSucceeds = true, object? playerData = null)
        {
            _accountLookupSucceeds = accountLookupSucceeds;
            _playerDataLookupSucceeds = playerDataLookupSucceeds;
            _playerData = playerData ?? new object();
        }

        public bool TryGetUserAccountId(string user, out int accountId)
        {
            accountId = 42;
            return _accountLookupSucceeds;
        }

        public bool TryGetPlayerData(int accountId, out object playerData)
        {
            playerData = _playerData;
            return _playerDataLookupSucceeds;
        }

        public IReadOnlyList<(int AccountId, string Username)> GetAllUserAccounts() => [];
    }
}
