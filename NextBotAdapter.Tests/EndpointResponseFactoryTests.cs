using NextBotAdapter.Infrastructure;
using NextBotAdapter.Models;
using NextBotAdapter.Models.Responses;
using Rests;

namespace NextBotAdapter.Tests;

public sealed class EndpointResponseFactoryTests
{
    [Fact]
    public void MissingUser_ShouldReturnBadRequestRestObject()
    {
        var result = EndpointResponseFactory.MissingUser();

        Assert.Equal("400", result.Status);
        var error = Assert.IsType<ApiError>(result["error"]);
        Assert.Equal("Missing required route parameter 'user'.", error.Message);
    }

    [Fact]
    public void UserNotFound_ShouldReturnNotFoundRestObject()
    {
        var result = EndpointResponseFactory.UserNotFound("User was not found.");

        Assert.Equal("404", result.Status);
        var error = Assert.IsType<ApiError>(result["error"]);
        Assert.Equal("User was not found.", error.Message);
    }

    [Fact]
    public void UserDataNotFound_ShouldReturnNotFoundRestObject()
    {
        var result = EndpointResponseFactory.UserDataNotFound("Player data was not found.");

        Assert.Equal("404", result.Status);
        var error = Assert.IsType<ApiError>(result["error"]);
        Assert.Equal("Player data was not found.", error.Message);
    }

    [Fact]
    public void Success_ShouldReturnOkRestObjectWithDataPayload()
    {
        var payload = new UserInfoResponse(100, 400, 20, 200, 15, 3, 1);

        var result = EndpointResponseFactory.Success(payload);

        Assert.Equal("200", result.Status);
        Assert.Same(payload, result["data"]);
    }
}
