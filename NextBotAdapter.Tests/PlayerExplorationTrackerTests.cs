using System.Collections;
using NextBotAdapter.Services;

namespace NextBotAdapter.Tests;

public sealed class PlayerExplorationTrackerTests
{
    [Fact]
    public void GetBitmap_ShouldReturnNullWhenAccountUnknown()
    {
        var tracker = CreateTracker(8400, 2400);

        Assert.Null(tracker.GetBitmap("unknown"));
    }

    [Fact]
    public void MarkArea_ShouldRevealCenterAndExpectedRadius()
    {
        var tracker = CreateTracker(8400, 2400);

        tracker.MarkArea("uuid-1", 100, 100);

        var bitmap = tracker.GetBitmap("uuid-1");
        Assert.NotNull(bitmap);

        // 41x41 box centered at (100, 100), so x in [80,120], y in [80,120].
        Assert.True(IsSet(bitmap!, 100, 100, 8400));
        Assert.True(IsSet(bitmap, 80, 80, 8400));
        Assert.True(IsSet(bitmap, 120, 120, 8400));
        Assert.True(IsSet(bitmap, 80, 120, 8400));
        Assert.True(IsSet(bitmap, 120, 80, 8400));

        // Just outside the radius should remain false.
        Assert.False(IsSet(bitmap, 79, 100, 8400));
        Assert.False(IsSet(bitmap, 121, 100, 8400));
        Assert.False(IsSet(bitmap, 100, 79, 8400));
        Assert.False(IsSet(bitmap, 100, 121, 8400));
    }

    [Fact]
    public void MarkArea_ShouldClipToWorldBoundsWithoutThrowing()
    {
        const int width = 100;
        const int height = 80;
        var tracker = CreateTracker(width, height);

        var exception = Record.Exception(() => tracker.MarkArea("uuid-edge", 0, 0));

        Assert.Null(exception);

        var bitmap = tracker.GetBitmap("uuid-edge");
        Assert.NotNull(bitmap);
        Assert.True(IsSet(bitmap!, 0, 0, width));
        Assert.True(IsSet(bitmap, 20, 20, width));
        Assert.False(IsSet(bitmap, 21, 0, width));
        Assert.False(IsSet(bitmap, 0, 21, width));
    }

    [Fact]
    public void MarkArea_ShouldClipToWorldBoundsAtFarCornerWithoutThrowing()
    {
        const int width = 100;
        const int height = 80;
        var tracker = CreateTracker(width, height);

        var exception = Record.Exception(() => tracker.MarkArea("uuid-corner", width - 1, height - 1));

        Assert.Null(exception);

        var bitmap = tracker.GetBitmap("uuid-corner");
        Assert.NotNull(bitmap);
        Assert.True(IsSet(bitmap!, width - 1, height - 1, width));
        Assert.True(IsSet(bitmap, width - 21, height - 21, width));
    }

    [Fact]
    public void Bitmap_ShouldStartAllFalseBeforeMarkArea()
    {
        var tracker = CreateTracker(50, 50);

        // Touch the dictionary by marking once outside-of-interest range, then read.
        tracker.MarkArea("uuid-start", 0, 0);
        var bitmap = tracker.GetBitmap("uuid-start")!;

        // Outside the marked radius should still be false.
        Assert.False(IsSet(bitmap, 49, 49, 50));
        Assert.False(IsSet(bitmap, 30, 30, 50));
    }

    [Fact]
    public void SaveAndLoad_ShouldRoundTripBitmapContents()
    {
        const int width = 200;
        const int height = 150;
        var storage = new InMemoryStorage();
        var tracker = new PlayerExplorationTracker(storage, () => (width, height));

        tracker.MarkArea("uuid-rt", 50, 50);
        tracker.Save("uuid-rt");

        var fresh = new PlayerExplorationTracker(storage, () => (width, height));
        fresh.Load("uuid-rt");
        var loaded = fresh.GetBitmap("uuid-rt");

        Assert.NotNull(loaded);
        Assert.True(IsSet(loaded!, 50, 50, width));
        Assert.True(IsSet(loaded, 30, 30, width));
        Assert.False(IsSet(loaded, 100, 100, width));
    }

    private static PlayerExplorationTracker CreateTracker(int width, int height)
        => new(new InMemoryStorage(), () => (width, height));

    private static bool IsSet(BitArray bitmap, int x, int y, int width)
        => bitmap.Get((y * width) + x);

    private sealed class InMemoryStorage : IExplorationStorage
    {
        private readonly Dictionary<string, byte[]> _store = new(StringComparer.Ordinal);

        public BitArray? Load(string accountUuid, int expectedBitCount)
        {
            if (!_store.TryGetValue(accountUuid, out var bytes))
            {
                return null;
            }

            var expectedByteCount = (expectedBitCount + 7) / 8;
            if (bytes.Length != expectedByteCount)
            {
                return null;
            }

            return new BitArray(bytes) { Length = expectedBitCount };
        }

        public void Save(string accountUuid, BitArray bitmap)
        {
            var byteCount = (bitmap.Length + 7) / 8;
            var bytes = new byte[byteCount];
            bitmap.CopyTo(bytes, 0);
            _store[accountUuid] = bytes;
        }
    }
}
