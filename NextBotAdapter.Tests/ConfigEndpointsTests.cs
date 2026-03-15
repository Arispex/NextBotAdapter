using NextBotAdapter.Infrastructure;
using NextBotAdapter.Models;
using NextBotAdapter.Rest;
using NextBotAdapter.Services;
using Rests;

namespace NextBotAdapter.Tests;

public sealed class ConfigEndpointsTests
{
    [Fact]
    public void Reload_ShouldReturnSuccessWhenReloadCompletes()
    {
        var service = new FakeReloadService();

        var result = Assert.IsType<RestObject>(ConfigEndpoints.Reload(service));

        Assert.Equal("200", result.Status);
        Assert.True(service.ReloadCalled);
        var payload = Assert.IsAssignableFrom<IDictionary<string, object?>>(result["data"]);
        Assert.Equal(true, payload["reloaded"]);
    }

    [Fact]
    public void Reload_ShouldReturnServerErrorWhenReloadThrows()
    {
        var service = new FakeReloadService(throwOnReload: true);

        var result = Assert.IsType<RestObject>(ConfigEndpoints.Reload(service));

        Assert.Equal("500", result.Status);
        var error = Assert.IsType<ApiError>(result["error"]);
        Assert.Equal(ErrorCodes.ConfigReloadFailed, error.Code);
    }

    private sealed class FakeReloadService(bool throwOnReload = false) : IConfigurationReloadService
    {
        public bool ReloadCalled { get; private set; }

        public void ReloadAll()
        {
            ReloadCalled = true;
            if (throwOnReload)
            {
                throw new InvalidOperationException("reload failed");
            }
        }
    }
}
