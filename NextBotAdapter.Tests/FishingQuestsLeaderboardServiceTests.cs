using NextBotAdapter.Services;

namespace NextBotAdapter.Tests;

public sealed class FishingQuestsLeaderboardServiceTests
{
    [Fact]
    public void GetLeaderboard_ShouldReturnEmptyListWhenNoAccountsExist()
    {
        var gateway = new FakeUserDataGateway([]);

        var result = FishingQuestsLeaderboardService.GetLeaderboard(gateway);

        Assert.Empty(result);
    }

    [Fact]
    public void GetLeaderboard_ShouldSkipAccountsWithNoPlayerData()
    {
        var gateway = new FakeUserDataGateway([(1, "alice", null)]);

        var result = FishingQuestsLeaderboardService.GetLeaderboard(gateway);

        Assert.Empty(result);
    }

    [Fact]
    public void GetLeaderboard_ShouldReturnQuestsCompletedForEachPlayer()
    {
        var gateway = new FakeUserDataGateway([(1, "alice", new FakePlayerData(questsCompleted: 15))]);

        var result = FishingQuestsLeaderboardService.GetLeaderboard(gateway);

        Assert.Single(result);
        Assert.Equal("alice", result[0].Username);
        Assert.Equal(15, result[0].QuestsCompleted);
    }

    [Fact]
    public void GetLeaderboard_ShouldSortByQuestsCompletedDescending()
    {
        var gateway = new FakeUserDataGateway(
        [
            (1, "alice", new FakePlayerData(questsCompleted: 5)),
            (2, "bob",   new FakePlayerData(questsCompleted: 42)),
            (3, "carol", new FakePlayerData(questsCompleted: 20))
        ]);

        var result = FishingQuestsLeaderboardService.GetLeaderboard(gateway);

        Assert.Equal(3, result.Count);
        Assert.Equal("bob",   result[0].Username);
        Assert.Equal(42,      result[0].QuestsCompleted);
        Assert.Equal("carol", result[1].Username);
        Assert.Equal(20,      result[1].QuestsCompleted);
        Assert.Equal("alice", result[2].Username);
        Assert.Equal(5,       result[2].QuestsCompleted);
    }

    [Fact]
    public void GetLeaderboard_ShouldIncludePlayersWithZeroQuestsCompleted()
    {
        var gateway = new FakeUserDataGateway([(1, "alice", new FakePlayerData(questsCompleted: 0))]);

        var result = FishingQuestsLeaderboardService.GetLeaderboard(gateway);

        Assert.Single(result);
        Assert.Equal(0, result[0].QuestsCompleted);
    }

    private sealed class FakePlayerData(int questsCompleted)
    {
        public int questsCompleted { get; } = questsCompleted;
    }

    private sealed class FakeUserDataGateway(
        IReadOnlyList<(int AccountId, string Username, object? PlayerData)> accounts) : IUserDataGateway
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
                    playerData = data!;
                    return data is not null;
                }
            }

            playerData = null!;
            return false;
        }

        public IReadOnlyList<(int AccountId, string Username)> GetAllUserAccounts()
            => accounts.Select(a => (a.AccountId, a.Username)).ToList();
    }
}
