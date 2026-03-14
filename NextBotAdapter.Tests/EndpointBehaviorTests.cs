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
        var result = EndpointResponseFactory.FromUserLookupError(new UserLookupError(ErrorCodes.MissingUser, "ignored"));

        Assert.Equal("400", result.Status);
        var error = Assert.IsType<ApiError>(result["error"]);
        Assert.Equal("Missing required route parameter 'user'.", error.Message);
    }

    [Fact]
    public void UserDataNotFound_ShouldMapToNotFoundRestObject()
    {
        var result = EndpointResponseFactory.FromUserLookupError(new UserLookupError(ErrorCodes.UserDataNotFound, "Player data was not found."));

        Assert.Equal("404", result.Status);
        var error = Assert.IsType<ApiError>(result["error"]);
        Assert.Equal("Player data was not found.", error.Message);
    }

    [Fact]
    public void NullLookupError_ShouldFallbackToUserNotFoundRestObject()
    {
        var result = EndpointResponseFactory.FromUserLookupError(null);

        Assert.Equal("404", result.Status);
        var error = Assert.IsType<ApiError>(result["error"]);
        Assert.Equal("User was not found.", error.Message);
    }

    [Fact]
    public void CreateCommands_ShouldReturnThreeRoutesWithExpectedMetadata()
    {
        var commands = EndpointRegistrar.CreateCommands();

        Assert.Collection(
            commands,
            command => AssertRoute(command, EndpointRoutes.UserInventory, Permissions.UserInventory),
            command => AssertRoute(command, EndpointRoutes.UserStats, Permissions.UserStats),
            command => AssertRoute(command, EndpointRoutes.WorldProgress, Permissions.WorldProgress));
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
