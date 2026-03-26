using NextBotAdapter.Models.Responses;
using NextBotAdapter.Services;

namespace NextBotAdapter.Tests;

public sealed class DeathLeaderboardServiceTests
{
    [Fact]
    public void GetLeaderboard_ShouldReturnEmptyListWhenNoAccountsExist()
    {
        var gateway = new FakeUserDataGateway([]);

        var result = DeathLeaderboardService.GetLeaderboard(gateway);

        Assert.Empty(result);
    }

    [Fact]
    public void GetLeaderboard_ShouldSkipAccountsWithNoPlayerData()
    {
        var gateway = new FakeUserDataGateway(
        [
            (1, "alice", null)
        ]);

        var result = DeathLeaderboardService.GetLeaderboard(gateway);

        Assert.Empty(result);
    }

    [Fact]
    public void GetLeaderboard_ShouldSumPveAndPvpDeaths()
    {
        var gateway = new FakeUserDataGateway(
        [
            (1, "alice", new FakePlayerData(deathsPve: 3, deathsPvp: 2))
        ]);

        var result = DeathLeaderboardService.GetLeaderboard(gateway);

        Assert.Single(result);
        Assert.Equal("alice", result[0].Username);
        Assert.Equal(5, result[0].Deaths);
    }

    [Fact]
    public void GetLeaderboard_ShouldSortByTotalDeathsDescending()
    {
        var gateway = new FakeUserDataGateway(
        [
            (1, "alice", new FakePlayerData(deathsPve: 1, deathsPvp: 0)),
            (2, "bob",   new FakePlayerData(deathsPve: 5, deathsPvp: 3)),
            (3, "carol", new FakePlayerData(deathsPve: 2, deathsPvp: 0))
        ]);

        var result = DeathLeaderboardService.GetLeaderboard(gateway);

        Assert.Equal(3, result.Count);
        Assert.Equal("bob",   result[0].Username);
        Assert.Equal(8,       result[0].Deaths);
        Assert.Equal("carol", result[1].Username);
        Assert.Equal(2,       result[1].Deaths);
        Assert.Equal("alice", result[2].Username);
        Assert.Equal(1,       result[2].Deaths);
    }

    [Fact]
    public void GetLeaderboard_ShouldIncludePlayersWithZeroDeaths()
    {
        var gateway = new FakeUserDataGateway(
        [
            (1, "alice", new FakePlayerData(deathsPve: 0, deathsPvp: 0))
        ]);

        var result = DeathLeaderboardService.GetLeaderboard(gateway);

        Assert.Single(result);
        Assert.Equal(0, result[0].Deaths);
    }

    private sealed class FakePlayerData(int deathsPve, int deathsPvp)
    {
        public int deathsPVE { get; } = deathsPve;
        public int deathsPVP { get; } = deathsPvp;
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
