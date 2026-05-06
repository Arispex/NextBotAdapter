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

        public int LoadCallCount { get; private set; }
        public int SaveCallCount { get; private set; }

        public ExplorationLoadResult Load(string accountName, int expectedBitCount)
        {
            LoadCallCount++;

            if (!_store.TryGetValue(accountName, out var bytes))
            {
                // Match FileExplorationStorage semantics: confirmed missing.
                return new ExplorationLoadResult(null, true);
            }

            var expectedByteCount = (expectedBitCount + 7) / 8;
            if (bytes.Length != expectedByteCount)
            {
                // Corrupt / partial: file exists but shape wrong — not negative-cacheable.
                return new ExplorationLoadResult(null, false);
            }

            var bitmap = new BitArray(bytes) { Length = expectedBitCount };
            return new ExplorationLoadResult(bitmap, false);
        }

        public bool Save(string accountName, BitArray bitmap)
        {
            SaveCallCount++;
            var byteCount = (bitmap.Length + 7) / 8;
            var bytes = new byte[byteCount];
            bitmap.CopyTo(bytes, 0);
            _store[accountName] = bytes;
            return true;
        }
    }

    /// <summary>
    /// Storage fake that simulates transient IO errors (e.g. NFS hiccup) by
    /// returning <see cref="ExplorationLoadResult"/> with <c>FileMissing=false</c>.
    /// Used to verify the tracker does NOT poison the negative cache on transient
    /// IO failures.
    /// </summary>
    private sealed class IoFailingStorage : IExplorationStorage
    {
        public int LoadCallCount { get; private set; }
        public int SaveCallCount { get; private set; }

        // When false, Load reports a transient IO failure: (null, FileMissing=false).
        // When true, Load returns a stamped bitmap successfully.
        public bool ReturnSuccess { get; set; }

        public ExplorationLoadResult Load(string accountName, int expectedBitCount)
        {
            LoadCallCount++;
            if (!ReturnSuccess)
            {
                // Transient IO error: must NOT be negative-cached by caller.
                return new ExplorationLoadResult(null, false);
            }

            var bitmap = new BitArray(expectedBitCount);
            // Stamp a sentinel bit so test can verify the bitmap was actually returned.
            if (expectedBitCount > 0) bitmap.Set(0, true);
            return new ExplorationLoadResult(bitmap, false);
        }

        public bool Save(string accountName, BitArray bitmap)
        {
            SaveCallCount++;
            return true;
        }
    }

    /// <summary>
    /// Storage fake that lets the test selectively configure which accounts'
    /// <see cref="Save"/> should fail. Used to verify SaveAll continues across
    /// per-account failures and reports both counts.
    /// </summary>
    private sealed class PartialFailureStorage : IExplorationStorage
    {
        public int LoadCallCount { get; private set; }
        public int SaveCallCount { get; private set; }
        public HashSet<string> FailingAccounts { get; } = new(StringComparer.Ordinal);
        public List<string> SaveCalls { get; } = new();

        public ExplorationLoadResult Load(string accountName, int expectedBitCount)
        {
            LoadCallCount++;
            return new ExplorationLoadResult(null, true);
        }

        public bool Save(string accountName, BitArray bitmap)
        {
            SaveCallCount++;
            SaveCalls.Add(accountName);
            return !FailingAccounts.Contains(accountName);
        }
    }

    [Fact]
    public void GetBitmap_ShouldLazyLoadFromStorage_WhenCacheMisses()
    {
        const int width = 400;
        const int height = 300;
        var storage = new InMemoryStorage();

        // Seed storage by saving a bitmap from a separate tracker (simulates a prior
        // session having persisted the file to disk).
        var seeder = new PlayerExplorationTracker(storage, () => (width, height));
        seeder.MarkArea("alice", 100, 100);
        seeder.Save("alice");

        // Fresh tracker: in-memory cache is empty, but storage already has alice.
        var tracker = new PlayerExplorationTracker(storage, () => (width, height));

        var bitmap = tracker.GetBitmap("alice");

        Assert.NotNull(bitmap);
        // Inside the 141x87 box around (100, 100): x in [30, 170], y in [57, 143].
        Assert.True(IsSet(bitmap!, 100, 100, width));
        Assert.True(IsSet(bitmap, 30, 57, width));
        Assert.True(IsSet(bitmap, 170, 143, width));
        // Outside the box.
        Assert.False(IsSet(bitmap, 300, 250, width));
    }

    [Fact]
    public void GetBitmap_ShouldCacheLoadedBitmap_AfterFirstLazyLoad()
    {
        const int width = 400;
        const int height = 300;
        var storage = new InMemoryStorage();

        // Seed storage and reset the call counter so we only measure tracker behavior.
        var seeder = new PlayerExplorationTracker(storage, () => (width, height));
        seeder.MarkArea("bob", 50, 50);
        seeder.Save("bob");

        var tracker = new PlayerExplorationTracker(storage, () => (width, height));
        var beforeFirst = storage.LoadCallCount;

        var firstBitmap = tracker.GetBitmap("bob");
        var afterFirst = storage.LoadCallCount;

        var secondBitmap = tracker.GetBitmap("bob");
        var afterSecond = storage.LoadCallCount;

        Assert.NotNull(firstBitmap);
        Assert.NotNull(secondBitmap);
        // First call performs exactly one Load against storage.
        Assert.Equal(beforeFirst + 1, afterFirst);
        // Second call must hit the in-memory cache without re-reading storage.
        Assert.Equal(afterFirst, afterSecond);
    }

    [Fact]
    public void GetBitmap_ShouldReturnNull_WhenStorageHasNoData()
    {
        const int width = 400;
        const int height = 300;
        var storage = new InMemoryStorage();
        var tracker = new PlayerExplorationTracker(storage, () => (width, height));

        var bitmap = tracker.GetBitmap("never-seen");

        Assert.Null(bitmap);
        // The lazy-load path was attempted exactly once on the first miss; subsequent
        // calls hit the negative cache (see GetBitmap_ShouldUseNegativeCache_AfterFirstStorageMiss).
        Assert.Equal(1, storage.LoadCallCount);
    }

    [Fact]
    public void Load_ShouldNotOverwrite_WhenInMemoryBitmapAlreadyExists()
    {
        const int width = 400;
        const int height = 300;
        var storage = new InMemoryStorage();

        // Storage holds an unrelated bitmap stamped at (300, 250). If Load were to
        // overwrite the in-memory state, we'd see (300, 250) revealed and (100, 100)
        // gone after the Load call.
        var seeder = new PlayerExplorationTracker(storage, () => (width, height));
        seeder.MarkArea("alice", 300, 250);
        seeder.Save("alice");

        var tracker = new PlayerExplorationTracker(storage, () => (width, height));
        // Stamp in-memory at (100, 100) without touching storage — this populates
        // _bitmaps["alice"] before Load runs.
        tracker.MarkArea("alice", 100, 100);

        // Load must preserve the in-memory bitmap (which has (100, 100) stamped) and
        // must NOT replace it with the storage bitmap (which has only (300, 250)).
        tracker.Load("alice");

        var bitmap = tracker.GetBitmap("alice");
        Assert.NotNull(bitmap);
        // In-memory stamp is preserved.
        Assert.True(IsSet(bitmap!, 100, 100, width));
        // Storage's stamp at (300, 250) was NOT pulled in (Load skipped because
        // the in-memory entry already existed).
        Assert.False(IsSet(bitmap, 300, 250, width));
    }

    [Fact]
    public void MarkArea_AndGetBitmap_ShouldShareSameStampedState()
    {
        // Atomic stamp contract (#1): the bitmap MarkArea writes to is the same
        // instance later returned (as a snapshot) by GetBitmap. If MarkArea ever
        // wrote to an orphan BitArray due to a two-phase lock, the read path
        // would see all-zero output.
        const int width = 400;
        const int height = 300;
        var tracker = CreateTracker(width, height);

        tracker.MarkArea("uuid-atomic", 100, 100);

        // Stamp must be visible via GetBitmap snapshot.
        var first = tracker.GetBitmap("uuid-atomic");
        Assert.NotNull(first);
        Assert.True(IsSet(first!, 100, 100, width));

        // A second stamp at a different coord must accumulate on the same instance.
        tracker.MarkArea("uuid-atomic", 200, 100);
        var second = tracker.GetBitmap("uuid-atomic");
        Assert.NotNull(second);
        Assert.True(IsSet(second!, 100, 100, width), "first stamp must still be visible after second MarkArea");
        Assert.True(IsSet(second, 200, 100, width), "second stamp must be visible — proves MarkArea wrote to the live bitmap, not an orphan");
    }

    [Fact]
    public void GetBitmap_ShouldUseNegativeCache_AfterFirstStorageMiss()
    {
        // Negative cache (#3): subsequent GetBitmap calls for an account with no
        // bitmap file must not re-probe storage. This makes leaderboard fan-out
        // across many accounts O(1) IO instead of O(N).
        const int width = 400;
        const int height = 300;
        var storage = new InMemoryStorage();
        var tracker = new PlayerExplorationTracker(storage, () => (width, height));

        var first = tracker.GetBitmap("alice");
        var afterFirst = storage.LoadCallCount;

        var second = tracker.GetBitmap("alice");
        var afterSecond = storage.LoadCallCount;

        Assert.Null(first);
        Assert.Null(second);
        Assert.Equal(1, afterFirst);
        // Negative-cache hit: no second IO probe.
        Assert.Equal(afterFirst, afterSecond);
    }

    [Fact]
    public void GetBitmap_ShouldHitInMemoryAfterStamp_NotNegativeCache()
    {
        // #3 side-effect regression: a player who arrives with no bitmap file
        // (registered into the negative cache) and then logs in and walks must
        // see their in-memory bitmap returned, not be blocked by the negative
        // cache. GetBitmap checks _bitmaps before _missingFiles; MarkArea also
        // clears the negative cache entry as a defensive belt-and-suspenders.
        const int width = 400;
        const int height = 300;
        var storage = new InMemoryStorage();
        var tracker = new PlayerExplorationTracker(storage, () => (width, height));

        // First GetBitmap registers alice into _missingFiles (no file on disk).
        Assert.Null(tracker.GetBitmap("alice"));

        // Player logs in + moves: an in-memory bitmap is created.
        tracker.MarkAtPosition("alice", 100, 100);

        // GetBitmap must now return the live in-memory bitmap, not be short-circuited
        // by the prior negative-cache entry.
        var bitmap = tracker.GetBitmap("alice");
        Assert.NotNull(bitmap);
        Assert.True(IsSet(bitmap!, 100, 100, width));
    }

    [Fact]
    public void MarkAtPosition_AfterForgetLastSample_DoesNotInterpolateOldShortJump()
    {
        // #5 contract: after ForgetLastSample, the next MarkAtPosition is treated
        // as a fresh first sample even if the previous lastSample is well within
        // the teleport threshold. Without ForgetLastSample, (100,100) -> (200,200)
        // is a 141-tile chord (under 500 threshold) and would be interpolated,
        // stamping the midpoint (150, 150). With ForgetLastSample, only (200,200)
        // gets stamped, and (150, 150) (which lies outside the (200,200) reveal
        // box of half-extent 70/43) must remain unrevealed.
        const int width = 2400;
        const int height = 600;
        var tracker = CreateTracker(width, height);

        tracker.MarkAtPosition("uuid-sess", 100, 100);
        tracker.ForgetLastSample("uuid-sess");
        tracker.MarkAtPosition("uuid-sess", 300, 300);

        var bitmap = tracker.GetBitmap("uuid-sess");
        Assert.NotNull(bitmap);

        // First endpoint reveal box [30..170] x [57..143]: (100, 100) is inside.
        Assert.True(IsSet(bitmap!, 100, 100, width));
        // Second endpoint reveal box [230..370] x [257..343]: (300, 300) is inside.
        Assert.True(IsSet(bitmap!, 300, 300, width));

        // The midpoint (200, 200) lies outside both endpoint boxes (vertical
        // half-extent 43, so the (100,100) box ends at y=143 and the (300,300)
        // box starts at y=257). If interpolation had run, (200, 200) would have
        // been stamped by an intermediate step. ForgetLastSample must prevent
        // that.
        Assert.False(IsSet(bitmap!, 200, 200, width));
    }

    [Fact]
    public void GetExplorationPercent_ShouldReturnZero_WhenBitmapMissing()
    {
        const int width = 400;
        const int height = 300;
        var storage = new InMemoryStorage();
        var tracker = new PlayerExplorationTracker(storage, () => (width, height));

        var percent = tracker.GetExplorationPercent("never-seen");

        Assert.Equal(0.0, percent);
    }

    [Fact]
    public void GetExplorationPercent_ShouldReturnZero_ForAllZeroBitmap()
    {
        const int width = 400;
        const int height = 300;

        // Pre-seed an all-zero bitmap via storage round-trip so the tracker reaches it via lazy-load.
        var storage = new InMemoryStorage();
        var seed = new BitArray(width * height); // all false
        storage.Save("zero", seed);
        var tracker = new PlayerExplorationTracker(storage, () => (width, height));

        var percent = tracker.GetExplorationPercent("zero");

        Assert.Equal(0.0, percent);
    }

    [Fact]
    public void GetExplorationPercent_ShouldReturn100_ForAllOneBitmap()
    {
        const int width = 400;
        const int height = 300;
        var storage = new InMemoryStorage();

        // Persist an all-1 bitmap. Note: BitArray(byte[]) followed by setting
        // Length back to expectedBitCount zeros out any tail bits beyond Length,
        // so PopCount over the int[] view stays consistent with bitmap.Length.
        var seed = new BitArray(width * height, defaultValue: true);
        storage.Save("full", seed);

        var tracker = new PlayerExplorationTracker(storage, () => (width, height));

        var percent = tracker.GetExplorationPercent("full");

        Assert.Equal(100.0, percent);
    }

    [Fact]
    public void GetExplorationPercent_ShouldReturnPartial_ForKnownPattern()
    {
        const int width = 400;
        const int height = 300;
        var storage = new InMemoryStorage();

        // Set exactly half of the bits to true.
        var totalBits = width * height;
        var seed = new BitArray(totalBits);
        for (var i = 0; i < totalBits; i += 2)
        {
            seed.Set(i, true);
        }
        storage.Save("half", seed);

        var tracker = new PlayerExplorationTracker(storage, () => (width, height));

        var percent = tracker.GetExplorationPercent("half");

        Assert.InRange(percent, 50.0 - 0.01, 50.0 + 0.01);
    }

    [Fact]
    public void GetExplorationPercent_ShouldLazyLoadFromStorage_WhenCacheMisses()
    {
        const int width = 400;
        const int height = 300;
        var storage = new InMemoryStorage();

        // Seed via an isolated tracker so storage holds a real bitmap on disk.
        var seeder = new PlayerExplorationTracker(storage, () => (width, height));
        seeder.MarkArea("alice", 100, 100);
        seeder.Save("alice");

        // Fresh tracker: in-memory cache is empty; GetExplorationPercent must
        // still see the persisted bitmap via the lazy-load path.
        var tracker = new PlayerExplorationTracker(storage, () => (width, height));

        var percent = tracker.GetExplorationPercent("alice");

        Assert.True(percent > 0.0, $"expected percent > 0 from lazy-load, got {percent}");
    }

    [Fact]
    public void GetExplorationPercent_ShouldIgnoreTailBits_WhenLengthIsNotMultipleOf32()
    {
        // 100 bits fits in 4 ints (128 bits) — the last 28 bits beyond Length must
        // not be counted by PopCount. We verify by saving an all-1 BitArray with
        // a length that's not a multiple of 32 and asserting we still get 100% (not
        // a polluted value > 100% caused by tail garbage).
        const int width = 10;
        const int height = 10; // 100 bits, not a multiple of 32

        var storage = new InMemoryStorage();
        var seed = new BitArray(width * height, defaultValue: true);
        storage.Save("tail", seed);

        var tracker = new PlayerExplorationTracker(storage, () => (width, height));

        var percent = tracker.GetExplorationPercent("tail");

        Assert.Equal(100.0, percent);
    }

    [Fact]
    public void GetBitmap_ShouldNotCacheMissOnIoFailure_AndRetryOnNextCall()
    {
        // Fix C: a transient IO error (NFS hiccup, momentary permission loss) must
        // not poison the negative cache. Otherwise the affected account is locked
        // out of exploration data until the process restarts.
        const int width = 400;
        const int height = 300;
        var storage = new IoFailingStorage { ReturnSuccess = false };
        var tracker = new PlayerExplorationTracker(storage, () => (width, height));

        // First call: storage reports IO failure (null, FileMissing=false).
        var first = tracker.GetBitmap("alice");
        Assert.Null(first);
        Assert.Equal(1, storage.LoadCallCount);

        // Second call MUST retry IO (negative cache was not populated for IO error).
        var second = tracker.GetBitmap("alice");
        Assert.Null(second);
        Assert.Equal(2, storage.LoadCallCount);

        // Recovery: storage starts succeeding. Tracker must pick the bitmap up.
        storage.ReturnSuccess = true;
        var third = tracker.GetBitmap("alice");
        Assert.NotNull(third);
        Assert.Equal(3, storage.LoadCallCount);
        // Sentinel bit set by the fake at index 0 confirms the loaded bitmap surfaced.
        Assert.True(third!.Get(0));
    }

    [Fact]
    public void Load_ShouldRecordNegativeCache_WhenStorageReportsFileMissing()
    {
        // Fix B: Load(name) must seed the negative cache when storage reports a
        // confirmed missing file, mirroring GetBitmap's policy. Otherwise a later
        // GetBitmap for the same account would do a redundant IO probe.
        const int width = 400;
        const int height = 300;
        var storage = new InMemoryStorage();
        var tracker = new PlayerExplorationTracker(storage, () => (width, height));

        // Load probes storage once and finds nothing (FileMissing=true).
        tracker.Load("never-seen");
        Assert.Equal(1, storage.LoadCallCount);

        // GetBitmap must hit the negative cache and skip the storage probe.
        var bitmap = tracker.GetBitmap("never-seen");
        Assert.Null(bitmap);
        Assert.Equal(1, storage.LoadCallCount);
    }

    [Fact]
    public void Load_ShouldNotRecordNegativeCache_OnTransientIoFailure()
    {
        // Fix B + C consistency: when Load encounters a transient IO failure
        // (FileMissing=false, Bitmap=null), it must NOT seed the negative cache.
        // A subsequent GetBitmap should re-probe storage so the lookup can recover
        // when IO becomes healthy again.
        const int width = 400;
        const int height = 300;
        var storage = new IoFailingStorage { ReturnSuccess = false };
        var tracker = new PlayerExplorationTracker(storage, () => (width, height));

        tracker.Load("alice");
        Assert.Equal(1, storage.LoadCallCount);

        // No negative cache seeded → next GetBitmap must retry IO.
        Assert.Null(tracker.GetBitmap("alice"));
        Assert.Equal(2, storage.LoadCallCount);
    }

    [Fact]
    public void SaveAll_ShouldCallStorageSaveForEveryInMemoryBitmap()
    {
        // Fix D: SaveAll iterates every in-memory bitmap and calls storage.Save
        // for each one without short-circuiting.
        const int width = 400;
        const int height = 300;
        var storage = new PartialFailureStorage();
        var tracker = new PlayerExplorationTracker(storage, () => (width, height));

        tracker.MarkArea("alice", 100, 100);
        tracker.MarkArea("bob", 200, 200);
        tracker.MarkArea("carol", 300, 250);

        tracker.SaveAll();

        Assert.Equal(3, storage.SaveCallCount);
        Assert.Contains("alice", storage.SaveCalls);
        Assert.Contains("bob", storage.SaveCalls);
        Assert.Contains("carol", storage.SaveCalls);
    }

    [Fact]
    public void Save_ShouldSkipUnchangedAccount_AfterFirstSave()
    {
        // Dirty tracking: a Save with no intervening MarkArea/MarkAtPosition
        // since the previous Save must short-circuit and not hit storage.
        const int width = 400;
        const int height = 300;
        var storage = new InMemoryStorage();
        var tracker = new PlayerExplorationTracker(storage, () => (width, height));

        tracker.MarkArea("alice", 100, 100);
        tracker.Save("alice");
        Assert.Equal(1, storage.SaveCallCount);

        // No new stamps — second Save must short-circuit (account is no longer dirty).
        tracker.Save("alice");
        Assert.Equal(1, storage.SaveCallCount);
    }

    [Fact]
    public void SaveAll_ShouldOnlyPersistDirtyAccounts()
    {
        // Dirty tracking: SaveAll must skip clean accounts even when they are
        // present in the in-memory dictionary. This is the core IO-saving win.
        const int width = 400;
        const int height = 300;
        var storage = new PartialFailureStorage();
        var tracker = new PlayerExplorationTracker(storage, () => (width, height));

        // Stamp three accounts, then SaveAll commits and clears all dirty.
        tracker.MarkArea("alice", 100, 100);
        tracker.MarkArea("bob", 150, 150);
        tracker.MarkArea("carol", 200, 200);
        tracker.SaveAll();
        Assert.Equal(3, storage.SaveCallCount);

        // Re-stamp only one account and call SaveAll again. Only the dirty
        // account should be saved.
        storage.SaveCalls.Clear();
        tracker.MarkArea("bob", 160, 160);
        tracker.SaveAll();

        Assert.Equal(4, storage.SaveCallCount);
        Assert.Single(storage.SaveCalls);
        Assert.Equal("bob", storage.SaveCalls[0]);
    }

    [Fact]
    public void SaveAll_ShouldRetainDirtyOnFailure_AndRetrySuccessfullyNextTime()
    {
        // Dirty tracking: when storage.Save returns false, the account stays
        // dirty so the next flush retries it. After fixing the failure, the
        // retry must succeed without re-stamping.
        const int width = 400;
        const int height = 300;
        var storage = new PartialFailureStorage();
        storage.FailingAccounts.Add("bob");
        var tracker = new PlayerExplorationTracker(storage, () => (width, height));

        tracker.MarkArea("alice", 100, 100);
        tracker.MarkArea("bob", 200, 200);
        tracker.SaveAll();

        Assert.Equal(2, storage.SaveCallCount);

        // bob's save failed → stays dirty. alice succeeded → clean.
        storage.FailingAccounts.Clear();
        storage.SaveCalls.Clear();
        tracker.SaveAll();

        // Only bob is retried. alice is not dirty.
        Assert.Equal(3, storage.SaveCallCount);
        Assert.Single(storage.SaveCalls);
        Assert.Equal("bob", storage.SaveCalls[0]);

        // bob is now clean — a third SaveAll is a no-op.
        storage.SaveCalls.Clear();
        tracker.SaveAll();
        Assert.Equal(3, storage.SaveCallCount);
    }

    [Fact]
    public void Save_ShouldRetainDirtyOnSingleAccountFailure_AndRetryNextCall()
    {
        // Single-account Save: if storage.Save returns false, the account must
        // stay dirty so the next Save (or SaveAll) retries it.
        const int width = 400;
        const int height = 300;
        var storage = new PartialFailureStorage();
        storage.FailingAccounts.Add("alice");
        var tracker = new PlayerExplorationTracker(storage, () => (width, height));

        tracker.MarkArea("alice", 100, 100);
        tracker.Save("alice");
        Assert.Equal(1, storage.SaveCallCount);

        // First Save failed → alice still dirty.
        storage.FailingAccounts.Clear();
        tracker.Save("alice");
        Assert.Equal(2, storage.SaveCallCount);

        // Second Save succeeded → alice clean.
        tracker.Save("alice");
        Assert.Equal(2, storage.SaveCallCount);
    }

    [Fact]
    public void LazyLoad_ShouldNotMarkAccountDirty()
    {
        // Lazy-loaded bitmaps mirror disk and must NOT be considered dirty.
        // A subsequent Save without any stamp must short-circuit.
        const int width = 400;
        const int height = 300;
        var storage = new InMemoryStorage();

        // Pre-seed via a separate tracker so storage holds a real bitmap.
        var seeder = new PlayerExplorationTracker(storage, () => (width, height));
        seeder.MarkArea("alice", 100, 100);
        seeder.Save("alice");
        var seederSaves = storage.SaveCallCount;

        var tracker = new PlayerExplorationTracker(storage, () => (width, height));

        // Lazy-load via GetBitmap.
        Assert.NotNull(tracker.GetBitmap("alice"));

        // Save must short-circuit because lazy-load did not mark dirty.
        tracker.Save("alice");
        Assert.Equal(seederSaves, storage.SaveCallCount);
    }

    [Fact]
    public void Load_ShouldNotMarkAccountDirty()
    {
        // Load() pulls from disk and must NOT mark dirty. A subsequent Save
        // without any stamp must short-circuit.
        const int width = 400;
        const int height = 300;
        var storage = new InMemoryStorage();

        var seeder = new PlayerExplorationTracker(storage, () => (width, height));
        seeder.MarkArea("alice", 100, 100);
        seeder.Save("alice");
        var seederSaves = storage.SaveCallCount;

        var tracker = new PlayerExplorationTracker(storage, () => (width, height));
        tracker.Load("alice");

        // No stamp happened — Save must short-circuit.
        tracker.Save("alice");
        Assert.Equal(seederSaves, storage.SaveCallCount);
    }

    [Fact]
    public void MarkAtPosition_ShouldMarkAccountDirty()
    {
        // MarkAtPosition is the OnPlayerUpdate stamp path; it must also drive
        // the dirty mark so periodic flush actually persists active session
        // data.
        const int width = 2400;
        const int height = 600;
        var storage = new PartialFailureStorage();
        var tracker = new PlayerExplorationTracker(storage, () => (width, height));

        tracker.MarkAtPosition("alice", 100, 100);
        tracker.SaveAll();

        Assert.Equal(1, storage.SaveCallCount);
        Assert.Equal("alice", storage.SaveCalls[0]);

        // Without further stamps, SaveAll should be a no-op.
        tracker.SaveAll();
        Assert.Equal(1, storage.SaveCallCount);
    }

    [Fact]
    public void SaveAll_ShouldNotInterrupt_WhenSomeSaveFails()
    {
        // Fix D: a single account's Save returning false must not stop SaveAll
        // from attempting the rest. This protects against losing data for healthy
        // accounts when one account's path is on a failing disk.
        const int width = 400;
        const int height = 300;
        var storage = new PartialFailureStorage();
        storage.FailingAccounts.Add("bob");
        var tracker = new PlayerExplorationTracker(storage, () => (width, height));

        tracker.MarkArea("alice", 100, 100);
        tracker.MarkArea("bob", 200, 200);
        tracker.MarkArea("carol", 300, 250);

        var exception = Record.Exception(() => tracker.SaveAll());

        Assert.Null(exception);
        // Every account was attempted, even though "bob" failed.
        Assert.Equal(3, storage.SaveCallCount);
        Assert.Contains("alice", storage.SaveCalls);
        Assert.Contains("bob", storage.SaveCalls);
        Assert.Contains("carol", storage.SaveCalls);
    }
}
