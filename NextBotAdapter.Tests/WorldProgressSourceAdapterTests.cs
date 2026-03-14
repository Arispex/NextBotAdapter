using NextBotAdapter.Services;

namespace NextBotAdapter.Tests;

public sealed class WorldProgressSourceAdapterTests
{
    [Fact]
    public void DefaultSource_ShouldBeWorldProgressSourceAdapter()
    {
        Assert.IsType<WorldProgressSourceAdapter>(WorldProgressService.DefaultSource);
    }
}
