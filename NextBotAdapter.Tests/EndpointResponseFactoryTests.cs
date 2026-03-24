using NextBotAdapter.Infrastructure;
using NextBotAdapter.Models;
using Rests;

namespace NextBotAdapter.Tests;

public sealed class EndpointResponseFactoryTests
{
    [Fact]
    public void MissingUser_ShouldReturnBadRequestRestObject()
    {
        var result = EndpointResponseFactory.MissingUser();

        Assert.Equal("400", result.Status);
        Assert.Equal("Missing required route parameter 'user'.", result.Error);
    }

    [Fact]
    public void UserNotFound_ShouldReturnBadRequestRestObject()
    {
        var result = EndpointResponseFactory.UserNotFound("User was not found.");

        Assert.Equal("400", result.Status);
        Assert.Equal("User was not found.", result.Error);
    }

    [Fact]
    public void UserDataNotFound_ShouldReturnBadRequestRestObject()
    {
        var result = EndpointResponseFactory.UserDataNotFound("Player data was not found.");

        Assert.Equal("400", result.Status);
        Assert.Equal("Player data was not found.", result.Error);
    }
}
