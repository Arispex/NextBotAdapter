using NextBotAdapter.Infrastructure;
using NextBotAdapter.Rest;
using NextBotAdapter.Services;

namespace NextBotAdapter.Tests;

public sealed class WorldMapFileEndpointsTests
{
    [Fact]
    public void MapFile_ShouldReturnOkWhenGenerationSucceeds()
    {
        var service = new FakeMapFileService(("12345.map", [1, 2, 3]));

        var result = WorldEndpoints.MapFile(service);

        Assert.Equal("200", result.Status);
        Assert.Equal("12345.map", result["fileName"]);
        Assert.Equal(Convert.ToBase64String([1, 2, 3]), result["base64"]);
    }

    [Fact]
    public void MapFile_ShouldReturnServerErrorWhenGenerationThrows()
    {
        var service = new ThrowingMapFileService(new InvalidOperationException("map generation failed"));

        var result = WorldEndpoints.MapFile(service);

        Assert.Equal("500", result.Status);
        Assert.Equal("map generation failed", result.Error);
    }

    private sealed class FakeMapFileService((string FileName, byte[] Content) result) : IMapFileService
    {
        public (string FileName, byte[] Content) GetMapFile() => result;
    }

    private sealed class ThrowingMapFileService(Exception exception) : IMapFileService
    {
        public (string FileName, byte[] Content) GetMapFile() => throw exception;
    }
}
