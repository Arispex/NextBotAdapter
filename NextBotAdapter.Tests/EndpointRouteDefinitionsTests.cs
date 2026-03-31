using NextBotAdapter.Infrastructure;

namespace NextBotAdapter.Tests;

public sealed class EndpointRouteDefinitionsTests
{
    [Theory]
    [InlineData(EndpointRoutes.UserInventory, "/nextbot/users/{user}/inventory")]
    [InlineData(EndpointRoutes.UserStats, "/nextbot/users/{user}/stats")]
    [InlineData(EndpointRoutes.WorldProgress, "/nextbot/world/progress")]
    [InlineData(EndpointRoutes.WorldMapImage, "/nextbot/world/map-image")]
    [InlineData(EndpointRoutes.LeaderboardDeaths, "/nextbot/leaderboards/deaths")]
    [InlineData(EndpointRoutes.LeaderboardFishingQuests, "/nextbot/leaderboards/fishing-quests")]
    [InlineData(EndpointRoutes.LeaderboardOnlineTime, "/nextbot/leaderboards/online-time")]
    [InlineData(EndpointRoutes.SecurityConfirmLogin, "/nextbot/security/confirm-login/{user}")]
    public void RouteConstants_ShouldUseExpectedPaths(string actual, string expected)
    {
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(Permissions.UserInventory, "nextbot.users.inventory")]
    [InlineData(Permissions.UserStats, "nextbot.users.stats")]
    [InlineData(Permissions.WorldProgress, "nextbot.world.progress")]
    [InlineData(Permissions.WorldMapImage, "nextbot.world.map_image")]
    [InlineData(Permissions.LeaderboardDeaths, "nextbot.leaderboards.deaths")]
    [InlineData(Permissions.LeaderboardFishingQuests, "nextbot.leaderboards.fishing_quests")]
    [InlineData(Permissions.LeaderboardOnlineTime, "nextbot.leaderboards.online_time")]
    [InlineData(Permissions.SecurityConfirmLogin, "nextbot.security.confirm_login")]
    public void PermissionConstants_ShouldUseExpectedNodes(string actual, string expected)
    {
        Assert.Equal(expected, actual);
    }
}
