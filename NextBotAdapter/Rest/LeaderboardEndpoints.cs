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
}
