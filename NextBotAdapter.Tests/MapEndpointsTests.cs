using NextBotAdapter.Infrastructure;
using NextBotAdapter.Models;
using NextBotAdapter.Models.Responses;
using NextBotAdapter.Rest;
using NextBotAdapter.Services;
using Rests;

namespace NextBotAdapter.Tests;

public sealed class MapEndpointsTests
{
    [Fact]
    public void Image_ShouldReturnOkWhenGenerationSucceeds()
    {
        var service = new FakeMapImageService(("map-1.png", "/tmp/map-1.png", [1, 2, 3]));

        var result = MapEndpoints.Image(service);

        Assert.Equal("200", result.Status);
        var response = Assert.IsType<MapImageResponse>(result["data"]);
        Assert.Equal("map-1.png", response.FileName);
        Assert.Equal(Convert.ToBase64String([1, 2, 3]), response.Base64);
    }

    [Fact]
    public void Image_ShouldReturnServerErrorWhenGenerationThrows()
    {
        var service = new ThrowingMapImageService(new InvalidOperationException("map generation failed"));

        var result = MapEndpoints.Image(service);

        Assert.Equal("500", result.Status);
        var error = Assert.IsType<ApiError>(result["error"]);
        Assert.Equal(ErrorCodes.MapImageGenerationFailed, error.Code);
        Assert.Equal("map generation failed", error.Message);
    }

    private sealed class FakeMapImageService((string FileName, string FilePath, byte[] Content) result) : IMapImageService
    {
        public (string FileName, string FilePath, byte[] Content) GenerateAndCache() => result;
    }

    private sealed class ThrowingMapImageService(Exception exception) : IMapImageService
    {
        public (string FileName, string FilePath, byte[] Content) GenerateAndCache() => throw exception;
    }
}
