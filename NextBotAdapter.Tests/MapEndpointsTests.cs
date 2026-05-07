using NextBotAdapter.Rest;
using NextBotAdapter.Services;

namespace NextBotAdapter.Tests;

public sealed class MapEndpointsTests
{
    [Fact]
    public void Image_ShouldReturnOkWhenGenerationSucceeds()
    {
        var service = new FakeMapImageService(("map-1.png", [1, 2, 3]));

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

    [Fact]
    public void ExploredImage_ShouldReturn500_WhenServiceNotConfigured()
    {
        var result = MapEndpoints.ExploredImage((IWorldExploredMapImageService?)null);

        Assert.Equal("500", result.Status);
        Assert.Equal("World explored map service is not configured.", result.Error);
    }

    [Fact]
    public void ExploredImage_ShouldReturn200_WithFileNameAndBase64_OnSuccess()
    {
        var service = new FakeWorldExploredMapImageService(("world-explored-1.png", [9, 8, 7]));

        var result = MapEndpoints.ExploredImage(service);

        Assert.Equal("200", result.Status);
        Assert.Equal("world-explored-1.png", result["fileName"]);
        Assert.Equal(Convert.ToBase64String([9, 8, 7]), result["base64"]);
    }

    [Fact]
    public void ExploredImage_ShouldReturn500_WhenGenerationThrows()
    {
        var service = new ThrowingWorldExploredMapImageService(new InvalidOperationException("explored map render failed"));

        var result = MapEndpoints.ExploredImage(service);

        Assert.Equal("500", result.Status);
        Assert.Equal("explored map render failed", result.Error);
    }

    private sealed class FakeMapImageService((string FileName, byte[] Content) result) : IMapImageService
    {
        public (string FileName, byte[] Content) Generate() => result;
    }

    private sealed class ThrowingMapImageService(Exception exception) : IMapImageService
    {
        public (string FileName, byte[] Content) Generate() => throw exception;
    }

    private sealed class FakeWorldExploredMapImageService((string FileName, byte[] Content) result) : IWorldExploredMapImageService
    {
        public (string FileName, byte[] Content) Generate() => result;
    }

    private sealed class ThrowingWorldExploredMapImageService(Exception exception) : IWorldExploredMapImageService
    {
        public (string FileName, byte[] Content) Generate() => throw exception;
    }
}
