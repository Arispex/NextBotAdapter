using NextBotAdapter.Infrastructure;

namespace NextBotAdapter.Tests;

public sealed class EndpointRouteDefinitionsTests
{
    [Theory]
    [InlineData(EndpointRoutes.UserInventory, "/nextbot/users/{user}/inventory")]
    [InlineData(EndpointRoutes.UserStats, "/nextbot/users/{user}/stats")]
    [InlineData(EndpointRoutes.WorldProgress, "/nextbot/world/progress")]
    public void RouteConstants_ShouldUseExpectedPaths(string actual, string expected)
    {
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(Permissions.UserInventory, "nextbot.users.inventory")]
    [InlineData(Permissions.UserStats, "nextbot.users.stats")]
    [InlineData(Permissions.WorldProgress, "nextbot.world.progress")]
    public void PermissionConstants_ShouldUseExpectedNodes(string actual, string expected)
    {
        Assert.Equal(expected, actual);
    }
}
