using NextBotAdapter.Infrastructure;
using NextBotAdapter.Models;
using NextBotAdapter.Models.Responses;
using NextBotAdapter.Rest;
using NextBotAdapter.Services;
using Rests;

namespace NextBotAdapter.Tests;

public sealed class RestEndpointLogicTests
{
    [Fact]
    public void Inventory_ShouldReturnBadRequestWhenUserIsBlank()
    {
        var result = Assert.IsType<RestObject>(UserEndpoints.Inventory(" ", new FakePlayerDataAccessor(new object())));

        Assert.Equal("400", result.Status);
        Assert.Equal("Missing required route parameter 'user'.", result.Error);
    }

    [Fact]
    public void Inventory_ShouldReturnOkWhenAccessorSucceeds()
    {
        var accessor = new FakePlayerDataAccessor(new FakePlayerData(
            [new InventoryItemResponse(0, 99, 3, 1)],
            new UserInfoResponse(1, 2, 3, 4, 5, 6, 7)));

        var result = Assert.IsType<RestObject>(UserEndpoints.Inventory("alice", accessor));

        Assert.Equal("200", result.Status);
        var items = Assert.IsAssignableFrom<IReadOnlyList<InventoryItemResponse>>(result["items"]);
        Assert.Single(items);
        Assert.Equal(99, items[0].NetId);
    }

    [Fact]
    public void Inventory_ShouldReturnBadRequestWhenAccessorFails()
    {
        var accessor = new FakePlayerDataAccessor("User was not found.");

        var result = Assert.IsType<RestObject>(UserEndpoints.Inventory("alice", accessor));

        Assert.Equal("400", result.Status);
        Assert.Equal("User was not found.", result.Error);
    }

    [Fact]
    public void Stats_ShouldReturnBadRequestWhenUserIsBlank()
    {
        var result = Assert.IsType<RestObject>(UserEndpoints.Stats(null, new FakePlayerDataAccessor(new object())));

        Assert.Equal("400", result.Status);
        Assert.Equal("Missing required route parameter 'user'.", result.Error);
    }

    [Fact]
    public void Stats_ShouldReturnOkWhenAccessorSucceeds()
    {
        var accessor = new FakePlayerDataAccessor(new FakePlayerData(
            [new InventoryItemResponse(0, 1, 2, 3)],
            new UserInfoResponse(120, 400, 80, 200, 9, 4, 2)));

        var result = Assert.IsType<RestObject>(UserEndpoints.Stats("alice", accessor));

        Assert.Equal("200", result.Status);
        Assert.Equal(120, result["health"]);
        Assert.Equal(4, result["deathsPve"]);
    }

    [Fact]
    public void Stats_ShouldReturnBadRequestWhenAccessorFails()
    {
        var accessor = new FakePlayerDataAccessor("Player data was not found.");

        var result = Assert.IsType<RestObject>(UserEndpoints.Stats("alice", accessor));

        Assert.Equal("400", result.Status);
        Assert.Equal("Player data was not found.", result.Error);
    }

    [Fact]
    public void Progress_ShouldReturnOkWithWorldProgressData()
    {
        var result = WorldEndpoints.Progress(new FakeWorldProgressSource(new WorldProgressSnapshot(
            true,
            false,
            false,
            false,
            false,
            false,
            true,
            false,
            false,
            false,
            false,
            false,
            false,
            false,
            false,
            false,
            false,
            false,
            false,
            false,
            true)));

        Assert.Equal("200", result.Status);
        Assert.Equal(true, result["kingSlime"]);
        Assert.Equal(true, result["wallOfFlesh"]);
        Assert.Equal(true, result["moonLord"]);
    }

    [Fact]
    public void MapImage_ShouldReturnOkWithGeneratedBase64()
    {
        var result = MapEndpoints.Image(new FakeMapImageService(("map-1.png", [1, 2, 3])));

        Assert.Equal("200", result.Status);
        Assert.Equal("map-1.png", result["fileName"]);
        Assert.Equal(Convert.ToBase64String([1, 2, 3]), result["base64"]);
    }

    [Fact]
    public void Deaths_ShouldReturnOkWithEntriesSortedByDeathsDescending()
    {
        var gateway = new FakeLeaderboardGateway(
        [
            (1, "alice", new FakePlayerData([new InventoryItemResponse(0, 1, 1, 0)], new UserInfoResponse(0, 0, 0, 0, 0, 1, 1))),
            (2, "bob",   new FakePlayerData([new InventoryItemResponse(0, 1, 1, 0)], new UserInfoResponse(0, 0, 0, 0, 0, 5, 3)))
        ]);

        var result = Assert.IsType<RestObject>(LeaderboardEndpoints.Deaths(gateway));

        Assert.Equal("200", result.Status);
        var entries = Assert.IsAssignableFrom<IReadOnlyList<NextBotAdapter.Models.Responses.DeathLeaderboardEntryResponse>>(result["entries"]);
        Assert.Equal(2, entries.Count);
        Assert.Equal("bob",   entries[0].Username);
        Assert.Equal(8,       entries[0].Deaths);
        Assert.Equal("alice", entries[1].Username);
        Assert.Equal(2,       entries[1].Deaths);
    }

    [Fact]
    public void Deaths_ShouldReturnEmptyEntriesWhenNoPlayersExist()
    {
        var gateway = new FakeLeaderboardGateway([]);

        var result = Assert.IsType<RestObject>(LeaderboardEndpoints.Deaths(gateway));

        Assert.Equal("200", result.Status);
        var entries = Assert.IsAssignableFrom<IReadOnlyList<NextBotAdapter.Models.Responses.DeathLeaderboardEntryResponse>>(result["entries"]);
        Assert.Empty(entries);
    }

    [Fact]
    public void ReadRouteUser_ShouldPreferVerbParametersWhenProvided()
    {
        var args = new RestRequestArgs(new RestVerbs { [RequestParameters.User] = "verb-user" }, null!, null!, null!);

        var user = InvokeReadRouteUser(args);

        Assert.Equal("verb-user", user);
    }

    private static string? InvokeReadRouteUser(RestRequestArgs args)
    {
        var method = typeof(UserEndpoints).GetMethod("ReadRouteUser", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(method);
        return method!.Invoke(null, [args]) as string;
    }

    private sealed class FakePlayerDataAccessor : IPlayerDataAccessor
    {
        private readonly object? _data;
        private readonly string? _error;

        public FakePlayerDataAccessor(object data)
        {
            _data = data;
        }

        public FakePlayerDataAccessor(string error)
        {
            _error = error;
        }

        public bool TryGetPlayerData(string user, out object data, out string? error)
        {
            data = _data!;
            error = _error;
            return _data is not null && _error is null;
        }
    }

    private sealed class FakePlayerData(IReadOnlyList<InventoryItemResponse> items, UserInfoResponse info)
    {
        public InventoryItemResponse[] inventory { get; } = [.. items];
        public int health { get; } = info.Health;
        public int maxHealth { get; } = info.MaxHealth;
        public int mana { get; } = info.Mana;
        public int maxMana { get; } = info.MaxMana;
        public int questsCompleted { get; } = info.QuestsCompleted;
        public int deathsPVE { get; } = info.DeathsPve;
        public int deathsPVP { get; } = info.DeathsPvp;
    }

    private sealed class FakeWorldProgressSource(WorldProgressSnapshot snapshot) : IWorldProgressSource
    {
        public WorldProgressSnapshot GetSnapshot() => snapshot;
    }

    private sealed class FakeMapImageService((string FileName, byte[] Content) result) : IMapImageService
    {
        public (string FileName, byte[] Content) Generate() => result;
    }

    private sealed class FakeLeaderboardGateway(
        IReadOnlyList<(int AccountId, string Username, FakePlayerData PlayerData)> accounts) : IUserDataGateway
    {
        public bool TryGetUserAccountId(string user, out int accountId)
        {
            accountId = default;
            return false;
        }

        public bool TryGetPlayerData(int accountId, out object playerData)
        {
            foreach (var (id, _, data) in accounts)
            {
                if (id == accountId)
                {
                    playerData = data;
                    return true;
                }
            }

            playerData = null!;
            return false;
        }

        public IReadOnlyList<(int AccountId, string Username)> GetAllUserAccounts()
            => accounts.Select(a => (a.AccountId, a.Username)).ToList();
    }
}
