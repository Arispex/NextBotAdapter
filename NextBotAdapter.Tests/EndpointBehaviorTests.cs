using NextBotAdapter.Infrastructure;
using NextBotAdapter.Models;
using NextBotAdapter.Models.Responses;
using NextBotAdapter.Rest;
using Rests;

namespace NextBotAdapter.Tests;

public sealed class EndpointBehaviorTests
{
    [Fact]
    public void MissingUser_ShouldMapToBadRequestRestObject()
    {
        var result = EndpointResponseFactory.FromUserLookupError(new UserLookupError(ErrorCodes.MissingUser, "ignored"));

        Assert.Equal("400", result.Status);
        var error = Assert.IsType<ApiError>(result["error"]);
        Assert.Equal(ErrorCodes.MissingUser, error.Code);
        Assert.Equal("Missing required route parameter 'user'.", error.Message);
    }

    [Fact]
    public void UserDataNotFound_ShouldMapToNotFoundRestObject()
    {
        var result = EndpointResponseFactory.FromUserLookupError(new UserLookupError(ErrorCodes.UserDataNotFound, "Player data was not found."));

        Assert.Equal("404", result.Status);
        var error = Assert.IsType<ApiError>(result["error"]);
        Assert.Equal(ErrorCodes.UserDataNotFound, error.Code);
        Assert.Equal("Player data was not found.", error.Message);
    }

    [Fact]
    public void NullLookupError_ShouldFallbackToUserNotFoundRestObject()
    {
        var result = EndpointResponseFactory.FromUserLookupError(null);

        Assert.Equal("404", result.Status);
        var error = Assert.IsType<ApiError>(result["error"]);
        Assert.Equal(ErrorCodes.UserNotFound, error.Code);
        Assert.Equal("User was not found.", error.Message);
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
