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
    public void Error_ShouldDefaultTo400()
    {
        var result = EndpointResponseFactory.Error("something went wrong");

        Assert.Equal("400", result.Status);
        Assert.Equal("something went wrong", result.Error);
    }

    [Fact]
    public void Error_ShouldRespectExplicitStatusCode()
    {
        var result = EndpointResponseFactory.Error("internal error", "500");

        Assert.Equal("500", result.Status);
        Assert.Equal("internal error", result.Error);
    }
}
