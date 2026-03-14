using NextBotAdapter.Models;
using NextBotAdapter.Models.Responses;

namespace NextBotAdapter.Tests;

public sealed class ResponseFactoryTests
{
    [Fact]
    public void Success_ShouldWrapPayloadInDataEnvelope()
    {
        var payload = new UserInfoResponse(100, 400, 20, 200, 15, 3, 1);

        var result = ApiResponse.Success(payload);

        Assert.Same(payload, result.Data);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Failure_ShouldWrapErrorDetailsInErrorEnvelope()
    {
        var result = ApiResponse.Failure("missing_user", "Missing required route parameter 'user'.");

        Assert.Null(result.Data);
        Assert.NotNull(result.Error);
        Assert.Equal("missing_user", result.Error!.Code);
        Assert.Equal("Missing required route parameter 'user'.", result.Error.Message);
    }
}
