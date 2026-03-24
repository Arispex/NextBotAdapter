using NextBotAdapter.Infrastructure;
using NextBotAdapter.Models;
using NextBotAdapter.Rest;
using Rests;

namespace NextBotAdapter.Tests;

public sealed class EndpointBehaviorTests
{
    [Fact]
    public void MissingUser_ShouldMapToBadRequestRestObject()
    {
        var result = EndpointResponseFactory.FromUserLookupError(new UserLookupError("User cannot be empty."));

        Assert.Equal("400", result.Status);
        Assert.Equal("User cannot be empty.", result.Error);
    }

    [Fact]
    public void UserDataNotFound_ShouldMapToBadRequestRestObject()
    {
        var result = EndpointResponseFactory.FromUserLookupError(new UserLookupError("Player data was not found."));

        Assert.Equal("400", result.Status);
        Assert.Equal("Player data was not found.", result.Error);
    }

    [Fact]
    public void NullLookupError_ShouldFallbackToUserNotFoundRestObject()
    {
        var result = EndpointResponseFactory.FromUserLookupError(null);

        Assert.Equal("400", result.Status);
        Assert.Equal("User was not found.", result.Error);
    }

    [Fact]
    public void CreateCommands_ShouldReturnAllRoutesWithExpectedMetadata()
    {
        var commands = EndpointRegistrar.CreateCommands();

        Assert.Collection(
            commands,
            command => AssertRoute(command, EndpointRoutes.UserInventory, Permissions.UserInventory),
            command => AssertRoute(command, EndpointRoutes.UserStats, Permissions.UserStats),
            command => AssertRoute(command, EndpointRoutes.WorldProgress, Permissions.WorldProgress),
            command => AssertRoute(command, EndpointRoutes.WorldMapImage, Permissions.WorldMapImage),
            command => AssertRoute(command, EndpointRoutes.WorldFile, Permissions.WorldFile),
            command => AssertRoute(command, EndpointRoutes.WorldMapFile, Permissions.WorldMapFile),
            command => AssertRoute(command, EndpointRoutes.Whitelist, Permissions.WhitelistView),
            command => AssertRoute(command, EndpointRoutes.WhitelistAddUser, Permissions.WhitelistAdd),
            command => AssertRoute(command, EndpointRoutes.WhitelistRemoveUser, Permissions.WhitelistRemove),
            command => AssertRoute(command, EndpointRoutes.ConfigReload, Permissions.ConfigReload));
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
