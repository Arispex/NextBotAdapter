using System.Net;
using NextBotAdapter.Infrastructure;
using NextBotAdapter.Rest;
using RestService = Rests.Rest;
using Rests;

namespace NextBotAdapter.Tests;

public sealed class EndpointRegistrarTests
{
    [Fact]
    public void Register_ShouldAddAllCommandsToRestInstance()
    {
        var rest = new RestService(IPAddress.Loopback, 0);

        EndpointRegistrar.Register(rest);

        var commandsField = typeof(RestService).GetField("commands", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(commandsField);

        var commands = Assert.IsAssignableFrom<System.Collections.IEnumerable>(commandsField!.GetValue(rest));
        var registered = commands.Cast<RestCommand>().ToArray();

        Assert.Collection(
            registered,
            command => AssertRoute(command, EndpointRoutes.UserInventory, NextBotAdapter.Infrastructure.Permissions.UserInventory),
            command => AssertRoute(command, EndpointRoutes.UserStats, NextBotAdapter.Infrastructure.Permissions.UserStats),
            command => AssertRoute(command, EndpointRoutes.WorldProgress, NextBotAdapter.Infrastructure.Permissions.WorldProgress),
            command => AssertRoute(command, EndpointRoutes.WorldMapImage, NextBotAdapter.Infrastructure.Permissions.WorldMapImage),
            command => AssertRoute(command, EndpointRoutes.WorldFile, NextBotAdapter.Infrastructure.Permissions.WorldFile),
            command => AssertRoute(command, EndpointRoutes.WorldMapFile, NextBotAdapter.Infrastructure.Permissions.WorldMapFile),
            command => AssertRoute(command, EndpointRoutes.Whitelist, NextBotAdapter.Infrastructure.Permissions.WhitelistView),
            command => AssertRoute(command, EndpointRoutes.WhitelistAddUser, NextBotAdapter.Infrastructure.Permissions.WhitelistAdd),
            command => AssertRoute(command, EndpointRoutes.WhitelistRemoveUser, NextBotAdapter.Infrastructure.Permissions.WhitelistRemove),
            command => AssertRoute(command, EndpointRoutes.ConfigReload, NextBotAdapter.Infrastructure.Permissions.ConfigReload),
            command => AssertRoute(command, EndpointRoutes.ConfigRead, NextBotAdapter.Infrastructure.Permissions.ConfigRead),
            command => AssertRoute(command, EndpointRoutes.ConfigUpdate, NextBotAdapter.Infrastructure.Permissions.ConfigUpdate),
            command => AssertRoute(command, EndpointRoutes.ConfigVerifyNextBot, NextBotAdapter.Infrastructure.Permissions.ConfigVerifyNextBot),
            command => AssertRoute(command, EndpointRoutes.LeaderboardDeaths, NextBotAdapter.Infrastructure.Permissions.LeaderboardDeaths),
            command => AssertRoute(command, EndpointRoutes.LeaderboardFishingQuests, NextBotAdapter.Infrastructure.Permissions.LeaderboardFishingQuests),
            command => AssertRoute(command, EndpointRoutes.LeaderboardOnlineTime, NextBotAdapter.Infrastructure.Permissions.LeaderboardOnlineTime),
            command => AssertRoute(command, EndpointRoutes.SecurityConfirmLogin, NextBotAdapter.Infrastructure.Permissions.SecurityConfirmLogin),
            command => AssertRoute(command, EndpointRoutes.SecurityRejectLogin, NextBotAdapter.Infrastructure.Permissions.SecurityRejectLogin));
    }

    private static void AssertRoute(RestCommand command, string expectedRoute, string expectedPermission)
    {
        Assert.Equal(expectedRoute, command.UriTemplate);

        var permissionsProperty = command.GetType().GetProperty("Permissions");
        Assert.NotNull(permissionsProperty);

        var permissions = permissionsProperty!.GetValue(command) as string[];
        Assert.NotNull(permissions);
        Assert.Single(permissions!);
        Assert.Equal(expectedPermission, permissions[0]);
    }
}
