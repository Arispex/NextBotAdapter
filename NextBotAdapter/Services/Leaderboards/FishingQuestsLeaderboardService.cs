using NextBotAdapter.Models.Responses;

namespace NextBotAdapter.Services;

public static class FishingQuestsLeaderboardService
{
    public static IReadOnlyList<FishingQuestsLeaderboardEntryResponse> GetLeaderboard(IUserDataGateway gateway)
    {
        var accounts = gateway.GetAllUserAccounts();
        var entries = new List<FishingQuestsLeaderboardEntryResponse>(accounts.Count);

        foreach (var (accountId, username) in accounts)
        {
            if (!gateway.TryGetPlayerData(accountId, out var playerData))
            {
                continue;
            }

            var questsCompleted = PlayerStatisticsReader.ReadDeaths(playerData, "questsCompleted");
            entries.Add(new FishingQuestsLeaderboardEntryResponse(username, questsCompleted));
        }

        return entries.OrderByDescending(e => e.QuestsCompleted).ToList();
    }
}
