using System.Collections;
using NextBotAdapter.Services;

namespace NextBotAdapter.Tests;

public sealed class MapExplorationLeaderboardServiceTests
{
    [Fact]
    public void GetLeaderboard_ShouldReturnEmpty_WhenNoAccounts()
    {
        var gateway = new FakeUserDataGateway([]);
        var tracker = new FakeExplorationTracker(new Dictionary<string, double>());

        var result = MapExplorationLeaderboardService.GetLeaderboard(gateway, tracker);

        Assert.Empty(result);
    }

    [Fact]
    public void GetLeaderboard_ShouldReturnEntriesSortedDesc_ByPercent()
    {
        var gateway = new FakeUserDataGateway(
        [
            (1, "alice"),
            (2, "bob"),
            (3, "carol")
        ]);
        var tracker = new FakeExplorationTracker(new Dictionary<string, double>
        {
            ["alice"] = 10.0,
            ["bob"] = 50.0,
            ["carol"] = 30.0
        });

        var result = MapExplorationLeaderboardService.GetLeaderboard(gateway, tracker);

        Assert.Equal(3, result.Count);
        Assert.Equal("bob", result[0].Username);
        Assert.Equal(50.0, result[0].MapExplorationPercent);
        Assert.Equal("carol", result[1].Username);
        Assert.Equal(30.0, result[1].MapExplorationPercent);
        Assert.Equal("alice", result[2].Username);
        Assert.Equal(10.0, result[2].MapExplorationPercent);
    }

    [Fact]
    public void GetLeaderboard_ShouldIncludeAllAccounts_EvenWithZeroPercent()
    {
        var gateway = new FakeUserDataGateway(
        [
            (1, "alice"),
            (2, "bob"),
            (3, "carol")
        ]);
        var tracker = new FakeExplorationTracker(new Dictionary<string, double>
        {
            ["alice"] = 0.0,
            ["bob"] = 25.0,
            ["carol"] = 0.0
        });

        var result = MapExplorationLeaderboardService.GetLeaderboard(gateway, tracker);

        Assert.Equal(3, result.Count);
        Assert.Equal("bob", result[0].Username);
        Assert.Equal(25.0, result[0].MapExplorationPercent);
        Assert.Contains(result, e => e.Username == "alice" && e.MapExplorationPercent == 0.0);
        Assert.Contains(result, e => e.Username == "carol" && e.MapExplorationPercent == 0.0);
    }

    private sealed class FakeUserDataGateway(IReadOnlyList<(int AccountId, string Username)> accounts) : IUserDataGateway
    {
        public bool TryGetUserAccountId(string user, out int accountId)
        {
            accountId = default;
            return false;
        }

        public bool TryGetPlayerData(int accountId, out object playerData)
        {
            playerData = null!;
            return false;
        }

        public IReadOnlyList<(int AccountId, string Username)> GetAllUserAccounts() => accounts;
    }

    private sealed class FakeExplorationTracker(IReadOnlyDictionary<string, double> percents) : IPlayerExplorationTracker
    {
        public void MarkArea(string accountName, int tileX, int tileY) { }
        public void MarkAtPosition(string accountName, int tileX, int tileY) { }
        public void ForgetLastSample(string accountName) { }
        public BitArray? GetBitmap(string accountName) => null;
        public double GetExplorationPercent(string accountName)
            => percents.TryGetValue(accountName, out var p) ? p : 0.0;
        public void Load(string accountName) { }
        public void Save(string accountName) { }
        public void SaveAll() { }
        public bool TryOrInto(string accountName, BitArray target) => false;
    }
}
