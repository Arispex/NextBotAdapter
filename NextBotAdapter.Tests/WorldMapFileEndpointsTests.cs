using NextBotAdapter.Infrastructure;
using NextBotAdapter.Models;
using NextBotAdapter.Models.Responses;
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
        var response = Assert.IsType<MapFileResponse>(result["data"]);
        Assert.Equal("12345.map", response.FileName);
        Assert.Equal(Convert.ToBase64String([1, 2, 3]), response.Base64);
    }

    [Fact]
    public void MapFile_ShouldReturnServerErrorWhenGenerationThrows()
    {
        var service = new ThrowingMapFileService(new InvalidOperationException("map generation failed"));

        var result = WorldEndpoints.MapFile(service);

        Assert.Equal("500", result.Status);
        var error = Assert.IsType<ApiError>(result["error"]);
        Assert.Equal(ErrorCodes.MapFileReadFailed, error.Code);
        Assert.Equal("map generation failed", error.Message);
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
