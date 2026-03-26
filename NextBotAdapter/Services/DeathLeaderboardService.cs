using NextBotAdapter.Models.Responses;

namespace NextBotAdapter.Services;

public static class DeathLeaderboardService
{
    public static IReadOnlyList<DeathLeaderboardEntryResponse> GetLeaderboard(IUserDataGateway gateway)
    {
        var accounts = gateway.GetAllUserAccounts();
        var entries = new List<DeathLeaderboardEntryResponse>(accounts.Count);

        foreach (var (accountId, username) in accounts)
        {
            if (!gateway.TryGetPlayerData(accountId, out var playerData))
            {
                continue;
            }

            var deathsPve = PlayerStatisticsReader.ReadDeaths(playerData, "deathsPVE");
            var deathsPvp = PlayerStatisticsReader.ReadDeaths(playerData, "deathsPVP");
            entries.Add(new DeathLeaderboardEntryResponse(username, deathsPve + deathsPvp));
        }

        return entries.OrderByDescending(e => e.Deaths).ToList();
    }
}
