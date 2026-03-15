using NextBotAdapter.Services;

namespace NextBotAdapter.Tests;

public sealed class PluginLoggerTests
{
    [Fact]
    public void Format_ShouldIncludePluginPrefixAndCategory()
    {
        var formatted = PluginLogger.Format("Config", "Configuration reloaded.");

        Assert.Equal("[NextBotAdapter][Config] Configuration reloaded.", formatted);
    }
}
