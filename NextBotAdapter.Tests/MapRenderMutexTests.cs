using NextBotAdapter.Services;

namespace NextBotAdapter.Tests;

public class MapRenderMutexTests
{
    [Fact]
    public void Lock_IsNonNull()
    {
        Assert.NotNull(MapRenderMutex.Lock);
    }

    [Fact]
    public void Lock_IsSingleSharedInstance()
    {
        var first = MapRenderMutex.Lock;
        var second = MapRenderMutex.Lock;

        // Guard against future regressions where someone replaces the
        // static readonly field with a getter that allocates a new object
        // every access — that would silently defeat the cross-service
        // serialization MapRenderMutex is meant to provide.
        Assert.Same(first, second);
    }
}
