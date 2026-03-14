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
        var error = Assert.IsType<ApiError>(result["error"]);
        Assert.Equal(ErrorCodes.MissingUser, error.Code);
        Assert.Equal("Missing required route parameter 'user'.", error.Message);
    }

    [Fact]
    public void Inventory_ShouldReturnOkWhenAccessorSucceeds()
    {
        var accessor = new FakePlayerDataAccessor(new FakePlayerData(
            [new InventoryItemResponse(0, 99, 3, 1)],
            new UserInfoResponse(1, 2, 3, 4, 5, 6, 7)));

        var result = Assert.IsType<RestObject>(UserEndpoints.Inventory("alice", accessor));

        Assert.Equal("200", result.Status);
        var response = Assert.IsType<UserInventoryResponse>(result["data"]);
        Assert.Single(response.Items);
        Assert.Equal(99, response.Items[0].NetId);
    }

    [Fact]
    public void Inventory_ShouldReturnNotFoundWhenAccessorFails()
    {
        var accessor = new FakePlayerDataAccessor(new UserLookupError(ErrorCodes.UserNotFound, "User was not found."));

        var result = Assert.IsType<RestObject>(UserEndpoints.Inventory("alice", accessor));

        Assert.Equal("404", result.Status);
        var error = Assert.IsType<ApiError>(result["error"]);
        Assert.Equal(ErrorCodes.UserNotFound, error.Code);
        Assert.Equal("User was not found.", error.Message);
    }

    [Fact]
    public void Stats_ShouldReturnBadRequestWhenUserIsBlank()
    {
        var result = Assert.IsType<RestObject>(UserEndpoints.Stats(null, new FakePlayerDataAccessor(new object())));

        Assert.Equal("400", result.Status);
        var error = Assert.IsType<ApiError>(result["error"]);
        Assert.Equal(ErrorCodes.MissingUser, error.Code);
        Assert.Equal("Missing required route parameter 'user'.", error.Message);
    }

    [Fact]
    public void Stats_ShouldReturnOkWhenAccessorSucceeds()
    {
        var accessor = new FakePlayerDataAccessor(new FakePlayerData(
            [new InventoryItemResponse(0, 1, 2, 3)],
            new UserInfoResponse(120, 400, 80, 200, 9, 4, 2)));

        var result = Assert.IsType<RestObject>(UserEndpoints.Stats("alice", accessor));

        Assert.Equal("200", result.Status);
        var response = Assert.IsType<UserInfoResponse>(result["data"]);
        Assert.Equal(120, response.Health);
        Assert.Equal(4, response.DeathsPve);
    }

    [Fact]
    public void Stats_ShouldReturnNotFoundWhenAccessorFails()
    {
        var accessor = new FakePlayerDataAccessor(new UserLookupError(ErrorCodes.UserDataNotFound, "Player data was not found."));

        var result = Assert.IsType<RestObject>(UserEndpoints.Stats("alice", accessor));

        Assert.Equal("404", result.Status);
        var error = Assert.IsType<ApiError>(result["error"]);
        Assert.Equal(ErrorCodes.UserDataNotFound, error.Code);
        Assert.Equal("Player data was not found.", error.Message);
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
        var response = Assert.IsType<WorldProgressResponse>(result["data"]);
        Assert.True(response.KingSlime);
        Assert.True(response.WallOfFlesh);
        Assert.True(response.MoonLord);
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
        private readonly UserLookupError? _error;

        public FakePlayerDataAccessor(object data)
        {
            _data = data;
        }

        public FakePlayerDataAccessor(UserLookupError error)
        {
            _error = error;
        }

        public bool TryGetPlayerData(string user, out object data, out UserLookupError? error)
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
}
