using NextBotAdapter.Infrastructure;
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
        Assert.Equal("Configuration reloaded successfully.", result["response"]);
    }

    [Fact]
    public void Reload_ShouldReturnServerErrorWhenReloadThrows()
    {
        var service = new FakeReloadService(throwOnReload: true);

        var result = Assert.IsType<RestObject>(ConfigEndpoints.Reload(service));

        Assert.Equal("500", result.Status);
        Assert.Equal("reload failed", result.Error);
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
