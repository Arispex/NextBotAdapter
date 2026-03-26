using NextBotAdapter.Models.Responses;
using NextBotAdapter.Services;
using Rests;

namespace NextBotAdapter.Rest;

public static class LeaderboardEndpoints
{
    public static object Deaths(RestRequestArgs args)
        => Deaths(UserDataService.DefaultGateway);

    public static object Deaths(IUserDataGateway gateway)
    {
        var entries = DeathLeaderboardService.GetLeaderboard(gateway);
        return new RestObject("200") { { "entries", entries } };
    }

    public static object FishingQuests(RestRequestArgs args)
        => FishingQuests(UserDataService.DefaultGateway);

    public static object FishingQuests(IUserDataGateway gateway)
    {
        var entries = FishingQuestsLeaderboardService.GetLeaderboard(gateway);
        return new RestObject("200") { { "entries", entries } };
    }

    public static IOnlineTimeService? OnlineTimeService { get; set; }

    public static object OnlineTime(RestRequestArgs args)
        => OnlineTime(OnlineTimeService);

    public static object OnlineTime(IOnlineTimeService? onlineTimeService)
    {
        if (onlineTimeService is null)
        {
            return new RestObject("200") { { "entries", Array.Empty<OnlineTimeLeaderboardEntryResponse>() } };
        }

        var records = onlineTimeService.GetAllRecords();
        var entries = records
            .Select(r => new OnlineTimeLeaderboardEntryResponse(r.Username, r.OnlineSeconds))
            .ToList();
        return new RestObject("200") { { "entries", entries } };
    }
}
