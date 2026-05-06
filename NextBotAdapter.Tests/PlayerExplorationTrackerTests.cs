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

        // 141x87 box centered at (100, 100): x in [30, 170], y in [57, 143].
        Assert.True(IsSet(bitmap!, 100, 100, 8400));
        Assert.True(IsSet(bitmap, 30, 57, 8400));
        Assert.True(IsSet(bitmap, 170, 143, 8400));
        Assert.True(IsSet(bitmap, 30, 143, 8400));
        Assert.True(IsSet(bitmap, 170, 57, 8400));

        // Just outside the box should remain false.
        Assert.False(IsSet(bitmap, 29, 100, 8400));
        Assert.False(IsSet(bitmap, 171, 100, 8400));
        Assert.False(IsSet(bitmap, 100, 56, 8400));
        Assert.False(IsSet(bitmap, 100, 144, 8400));
    }

    [Fact]
    public void MarkArea_ShouldClipToWorldBoundsWithoutThrowing()
    {
        const int width = 200;
        const int height = 160;
        var tracker = CreateTracker(width, height);

        var exception = Record.Exception(() => tracker.MarkArea("uuid-edge", 0, 0));

        Assert.Null(exception);

        var bitmap = tracker.GetBitmap("uuid-edge");
        Assert.NotNull(bitmap);
        Assert.True(IsSet(bitmap!, 0, 0, width));
        // Within the half-extents (70 horizontally, 43 vertically) of the box around (0, 0).
        Assert.True(IsSet(bitmap, 70, 43, width));
        Assert.False(IsSet(bitmap, 71, 0, width));
        Assert.False(IsSet(bitmap, 0, 44, width));
    }

    [Fact]
    public void MarkArea_ShouldClipToWorldBoundsAtFarCornerWithoutThrowing()
    {
        const int width = 200;
        const int height = 160;
        var tracker = CreateTracker(width, height);

        var exception = Record.Exception(() => tracker.MarkArea("uuid-corner", width - 1, height - 1));

        Assert.Null(exception);

        var bitmap = tracker.GetBitmap("uuid-corner");
        Assert.NotNull(bitmap);
        Assert.True(IsSet(bitmap!, width - 1, height - 1, width));
        Assert.True(IsSet(bitmap, width - 71, height - 44, width));
    }

    [Fact]
    public void Bitmap_ShouldStartAllFalseBeforeMarkArea()
    {
        const int width = 300;
        const int height = 200;
        var tracker = CreateTracker(width, height);

        // Touch the dictionary by marking once outside-of-interest range, then read.
        tracker.MarkArea("uuid-start", 0, 0);
        var bitmap = tracker.GetBitmap("uuid-start")!;

        // Outside the marked box should still be false.
        Assert.False(IsSet(bitmap, width - 1, height - 1, width));
        // (200, 100) is well outside the box [0..70] x [0..43] around (0, 0).
        Assert.False(IsSet(bitmap, 200, 100, width));
    }

    [Fact]
    public void SaveAndLoad_ShouldRoundTripBitmapContents()
    {
        const int width = 400;
        const int height = 300;
        var storage = new InMemoryStorage();
        var tracker = new PlayerExplorationTracker(storage, () => (width, height));

        tracker.MarkArea("uuid-rt", 100, 100);
        tracker.Save("uuid-rt");

        var fresh = new PlayerExplorationTracker(storage, () => (width, height));
        fresh.Load("uuid-rt");
        var loaded = fresh.GetBitmap("uuid-rt");

        Assert.NotNull(loaded);
        // Inside the 141x87 box around (100, 100): x in [30, 170], y in [57, 143].
        Assert.True(IsSet(loaded!, 100, 100, width));
        Assert.True(IsSet(loaded, 50, 80, width));
        // Outside the box.
        Assert.False(IsSet(loaded, 300, 250, width));
    }

    [Fact]
    public void MarkAtPosition_FirstSample_StampsSingleBox()
    {
        const int width = 2400;
        const int height = 600;
        var tracker = CreateTracker(width, height);

        tracker.MarkAtPosition("uuid-first", 100, 100);

        var bitmap = tracker.GetBitmap("uuid-first");
        Assert.NotNull(bitmap);

        // 141x87 box centered at (100, 100): x in [30, 170], y in [57, 143].
        for (var y = 57; y <= 143; y++)
        {
            for (var x = 30; x <= 170; x++)
            {
                Assert.True(IsSet(bitmap!, x, y, width));
            }
        }

        // Just outside the box should remain false.
        Assert.False(IsSet(bitmap!, 29, 100, width));
        Assert.False(IsSet(bitmap!, 171, 100, width));
        Assert.False(IsSet(bitmap!, 100, 56, width));
        Assert.False(IsSet(bitmap!, 100, 144, width));
    }

    [Fact]
    public void MarkAtPosition_HorizontalLine_FillsContinuously()
    {
        const int width = 2400;
        const int height = 600;
        var tracker = CreateTracker(width, height);

        tracker.MarkAtPosition("uuid-line", 0, 100);
        tracker.MarkAtPosition("uuid-line", 200, 100);

        var bitmap = tracker.GetBitmap("uuid-line");
        Assert.NotNull(bitmap);

        // Path is from (0, 100) to (200, 100). With 141x87 reveal boxes
        // (half-extent X=70, Y=43), the line covers x in [-70, 270] -> clipped to [0, 270].
        // y=100 sits inside every box, so the entire row [0..270] must be true.
        for (var x = 0; x <= 270; x++)
        {
            Assert.True(IsSet(bitmap!, x, 100, width));
        }
    }

    [Fact]
    public void MarkAtPosition_DiagonalLine_FillsContinuously()
    {
        const int width = 2400;
        const int height = 600;
        var tracker = CreateTracker(width, height);

        // Pick a diagonal whose chord length is below the teleport threshold (200)
        // so we exercise the interpolation branch, not the teleport branch.
        // chord = sqrt(160^2 + 80^2) ≈ 178.9 < 200.
        tracker.MarkAtPosition("uuid-diag", 0, 0);
        tracker.MarkAtPosition("uuid-diag", 160, 80);

        var bitmap = tracker.GetBitmap("uuid-diag");
        Assert.NotNull(bitmap);

        // Sample several points along the diagonal at the interpolated steps.
        // Each sampled (x, y) is the center of a 141x87 reveal box, so the exact
        // center pixel is inside the stamped box.
        var checkpoints = new (int x, int y)[]
        {
            (0, 0),
            (40, 20),
            (80, 40),
            (120, 60),
            (160, 80),
        };

        foreach (var (x, y) in checkpoints)
        {
            Assert.True(IsSet(bitmap!, x, y, width));
        }
    }

    [Fact]
    public void MarkAtPosition_Teleport_OnlyStampsEndpoint()
    {
        const int width = 2400;
        const int height = 1500;
        var tracker = CreateTracker(width, height);

        // chord = sqrt(900^2 + 900^2) ≈ 1273 > 500 threshold, so this still
        // exercises the teleport branch (no interpolation between endpoints).
        tracker.MarkAtPosition("uuid-tp", 100, 100);
        tracker.MarkAtPosition("uuid-tp", 1000, 1000);

        var bitmap = tracker.GetBitmap("uuid-tp");
        Assert.NotNull(bitmap);

        // First sample's box is intact: x in [30, 170], y in [57, 143].
        Assert.True(IsSet(bitmap!, 100, 100, width));
        Assert.True(IsSet(bitmap!, 30, 57, width));
        Assert.True(IsSet(bitmap!, 170, 143, width));

        // Endpoint's box is stamped: x in [930, 1070], y in [957, 1043].
        Assert.True(IsSet(bitmap!, 1000, 1000, width));
        Assert.True(IsSet(bitmap!, 930, 957, width));
        Assert.True(IsSet(bitmap!, 1070, 1043, width));

        // The midpoint along the chord must NOT be stamped (no interpolation on teleport).
        // (500, 500) is far outside both endpoint boxes.
        Assert.False(IsSet(bitmap!, 500, 500, width));
    }

    [Fact]
    public void MarkAtPosition_LongJumpUnder500Threshold_StillInterpolates()
    {
        // Regression guard for the teleport threshold raise from 200 -> 500.
        // chord = 300 falls in the 200-500 band, which represents a network-batched
        // high-speed flight (wings/mount, single coalesced packet). It must be
        // bridged with interpolation, not treated as a teleport.
        const int width = 2400;
        const int height = 600;
        var tracker = CreateTracker(width, height);

        tracker.MarkAtPosition("uuid-longjump", 100, 100);
        tracker.MarkAtPosition("uuid-longjump", 400, 100);

        var bitmap = tracker.GetBitmap("uuid-longjump");
        Assert.NotNull(bitmap);

        // Path is from (100, 100) to (400, 100). With 141x87 reveal boxes
        // (half-extent X=70, Y=43), the line covers x in [30, 470].
        // y=100 sits inside every box (vertical half-extent 43 around y=100),
        // so the entire row [30..470] must be true with no gap.
        for (var x = 30; x <= 470; x++)
        {
            Assert.True(IsSet(bitmap!, x, 100, width), $"expected (x={x}, y=100) to be revealed by interpolation");
        }
    }

    [Fact]
    public void MarkAtPosition_ZeroDistance_NoOp()
    {
        const int width = 2400;
        const int height = 600;
        var trackerOnce = CreateTracker(width, height);
        var trackerTwice = CreateTracker(width, height);

        trackerOnce.MarkAtPosition("uuid-once", 100, 100);

        trackerTwice.MarkAtPosition("uuid-twice", 100, 100);
        trackerTwice.MarkAtPosition("uuid-twice", 100, 100);

        var bmpOnce = trackerOnce.GetBitmap("uuid-once");
        var bmpTwice = trackerTwice.GetBitmap("uuid-twice");
        Assert.NotNull(bmpOnce);
        Assert.NotNull(bmpTwice);

        // The two bitmaps must be identical: zero-distance second sample is a no-op.
        Assert.Equal(bmpOnce!.Length, bmpTwice!.Length);
        for (var i = 0; i < bmpOnce.Length; i++)
        {
            Assert.Equal(bmpOnce.Get(i), bmpTwice.Get(i));
        }
    }

    [Fact]
    public void ForgetLastSample_AllowsFreshFirstStamp()
    {
        const int width = 2400;
        const int height = 1500;
        var tracker = CreateTracker(width, height);

        tracker.MarkAtPosition("uuid-forget", 100, 100);
        tracker.ForgetLastSample("uuid-forget");
        tracker.MarkAtPosition("uuid-forget", 1000, 1000);

        var bitmap = tracker.GetBitmap("uuid-forget");
        Assert.NotNull(bitmap);

        // Both endpoints are stamped.
        Assert.True(IsSet(bitmap!, 100, 100, width));
        Assert.True(IsSet(bitmap!, 1000, 1000, width));

        // Forget cleared the last-sample state, so no interpolated line was drawn
        // between (100, 100) and (1000, 1000). (500, 500) is far outside both
        // endpoint reveal boxes (half-extent X=70, Y=43).
        Assert.False(IsSet(bitmap!, 500, 500, width));
    }

    private static PlayerExplorationTracker CreateTracker(int width, int height)
        => new(new InMemoryStorage(), () => (width, height));

    private static bool IsSet(BitArray bitmap, int x, int y, int width)
        => bitmap.Get((y * width) + x);

    private sealed class InMemoryStorage : IExplorationStorage
    {
        private readonly Dictionary<string, byte[]> _store = new(StringComparer.Ordinal);

        public BitArray? Load(string accountName, int expectedBitCount)
        {
            if (!_store.TryGetValue(accountName, out var bytes))
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

        public void Save(string accountName, BitArray bitmap)
        {
            var byteCount = (bitmap.Length + 7) / 8;
            var bytes = new byte[byteCount];
            bitmap.CopyTo(bytes, 0);
            _store[accountName] = bytes;
        }
    }
}
