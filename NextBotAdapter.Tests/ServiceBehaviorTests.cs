using System.Collections.Generic;
using NextBotAdapter.Infrastructure;
using NextBotAdapter.Models;
using NextBotAdapter.Models.Responses;
using NextBotAdapter.Services;

namespace NextBotAdapter.Tests;

public sealed class ServiceBehaviorTests
{
    [Fact]
    public void TryGetInventory_ShouldReturnMappedItemsWhenAccessorSucceeds()
    {
        var accessor = new FakePlayerDataAccessor(new FakePlayerData(
            [
                new InventoryItemResponse(0, 100, 2, 1),
                new InventoryItemResponse(1, 200, 5, 3)
            ],
            new UserInfoResponse(120, 400, 80, 200, 9, 4, 2)));

        var success = UserInventoryService.TryGetInventory("alice", accessor, out var response, out var error);

        Assert.True(success);
        Assert.Null(error);
        Assert.Collection(
            response.Items,
            item =>
            {
                Assert.Equal(0, item.Slot);
                Assert.Equal(100, item.NetId);
                Assert.Equal(2, item.Stack);
                Assert.Equal(1, item.PrefixId);
            },
            item =>
            {
                Assert.Equal(1, item.Slot);
                Assert.Equal(200, item.NetId);
                Assert.Equal(5, item.Stack);
                Assert.Equal(3, item.PrefixId);
            });
    }

    [Fact]
    public void TryGetInventory_ShouldBubbleAccessorErrors()
    {
        var expectedError = new UserLookupError("User was not found.");
        var accessor = new FakePlayerDataAccessor(expectedError);

        var success = UserInventoryService.TryGetInventory("alice", accessor, out var response, out var error);

        Assert.False(success);
        Assert.Empty(response.Items);
        Assert.Equal(expectedError, error);
    }

    [Fact]
    public void TryGetUserInfo_ShouldReturnMappedResponseWhenAccessorSucceeds()
    {
        var accessor = new FakePlayerDataAccessor(new FakePlayerData(
            [new InventoryItemResponse(0, 100, 2, 1)],
            new UserInfoResponse(120, 400, 80, 200, 9, 4, 2)));

        var success = UserInfoService.TryGetUserInfo("alice", accessor, out var response, out var error);

        Assert.True(success);
        Assert.Null(error);
        Assert.Equal(120, response.Health);
        Assert.Equal(400, response.MaxHealth);
        Assert.Equal(80, response.Mana);
        Assert.Equal(200, response.MaxMana);
        Assert.Equal(9, response.QuestsCompleted);
        Assert.Equal(4, response.DeathsPve);
        Assert.Equal(2, response.DeathsPvp);
    }

    [Fact]
    public void TryGetUserInfo_ShouldBubbleAccessorErrors()
    {
        var expectedError = new UserLookupError("Player data was not found.");
        var accessor = new FakePlayerDataAccessor(expectedError);

        var success = UserInfoService.TryGetUserInfo("alice", accessor, out var response, out var error);

        Assert.False(success);
        Assert.Equal(new UserInfoResponse(0, 0, 0, 0, 0, 0, 0), response);
        Assert.Equal(expectedError, error);
    }

    [Fact]
    public void GetProgress_ShouldMapSnapshotFromInjectedSource()
    {
        var source = new FakeWorldProgressSource(new WorldProgressSnapshot(
            true,
            true,
            false,
            false,
            true,
            false,
            true,
            true,
            false,
            false,
            true,
            true,
            false,
            false,
            true,
            true,
            false,
            true,
            false,
            true,
            false));

        var response = WorldProgressService.GetProgress(source);

        Assert.True(response.KingSlime);
        Assert.True(response.EyeOfCthulhu);
        Assert.False(response.EaterOfWorldsOrBrainOfCthulhu);
        Assert.True(response.WallOfFlesh);
        Assert.True(response.QueenSlime);
        Assert.True(response.SkeletronPrime);
        Assert.True(response.Plantera);
        Assert.True(response.EmpressOfLight);
        Assert.True(response.LunaticCultist);
        Assert.True(response.NebulaPillar);
        Assert.True(response.StardustPillar);
        Assert.False(response.MoonLord);
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
            return _data is not null;
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
