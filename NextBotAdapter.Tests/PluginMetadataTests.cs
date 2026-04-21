using NextBotAdapter.Plugin;

namespace NextBotAdapter.Tests;

public sealed class PluginMetadataTests
{
    [Fact]
    public void PluginMetadata_ShouldExposeExpectedValues()
    {
        var plugin = new NextBotAdapterPlugin(null!);

        Assert.Equal("Arispex", plugin.Author);
        Assert.Equal("Provides NextBot with TShock server information.", plugin.Description);
        Assert.Equal("NextBotAdapter", plugin.Name);
        Assert.Equal(new Version(1, 2, 0), plugin.Version);
    }
}
