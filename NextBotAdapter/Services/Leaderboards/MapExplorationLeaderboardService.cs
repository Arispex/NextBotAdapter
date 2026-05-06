using NextBotAdapter.Models.Responses;

namespace NextBotAdapter.Services;

public static class MapExplorationLeaderboardService
{
    public static IReadOnlyList<MapExplorationLeaderboardEntryResponse> GetLeaderboard(
        IUserDataGateway gateway,
        IPlayerExplorationTracker tracker)
    {
        var accounts = gateway.GetAllUserAccounts();
        var entries = new List<MapExplorationLeaderboardEntryResponse>(accounts.Count);

        foreach (var (_, username) in accounts)
        {
            var percent = tracker.GetExplorationPercent(username);
            entries.Add(new MapExplorationLeaderboardEntryResponse(username, percent));
        }

        return entries.OrderByDescending(e => e.MapExplorationPercent).ToList();
    }
}
