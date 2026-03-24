using NextBotAdapter.Infrastructure;
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
        Assert.Equal("map-1.png", result["fileName"]);
        Assert.Equal(Convert.ToBase64String([1, 2, 3]), result["base64"]);
    }

    [Fact]
    public void Image_ShouldReturnServerErrorWhenGenerationThrows()
    {
        var service = new ThrowingMapImageService(new InvalidOperationException("map generation failed"));

        var result = MapEndpoints.Image(service);

        Assert.Equal("500", result.Status);
        Assert.Equal("map generation failed", result.Error);
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
