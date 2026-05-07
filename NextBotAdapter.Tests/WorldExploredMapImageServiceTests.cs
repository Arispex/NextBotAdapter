using System.Collections;
using NextBotAdapter.Services;

namespace NextBotAdapter.Tests;

public sealed class WorldExploredMapImageServiceTests
{
    [Fact]
    public void Generate_ShouldUnionAllPlayerBitmaps()
    {
        const int width = 4;
        const int height = 2;
        const int length = width * height;

        var aliceBitmap = MakeBitmap(length, [0, 1]);
        var bobBitmap = MakeBitmap(length, [2, 3]);
        var carolBitmap = MakeBitmap(length, [4, 7]);

        var gateway = new FakeUserDataGateway([(1, "alice"), (2, "bob"), (3, "carol")]);
        var tracker = new FakeExplorationTracker(new Dictionary<string, BitArray?>
        {
            ["alice"] = aliceBitmap,
            ["bob"] = bobBitmap,
            ["carol"] = carolBitmap
        });
        var renderer = new CapturingRenderer();
        var service = new WorldExploredMapImageService(gateway, tracker, renderer, () => (width, height));

        service.Generate();

        Assert.NotNull(renderer.LastBitmap);
        Assert.Equal(length, renderer.LastBitmap!.Length);
        for (var i = 0; i < length; i++)
        {
            var expected = i is 0 or 1 or 2 or 3 or 4 or 7;
            Assert.Equal(expected, renderer.LastBitmap.Get(i));
        }
    }

    [Fact]
    public void Generate_ShouldSkipAccountsWithNullBitmap()
    {
        const int width = 4;
        const int height = 2;
        const int length = width * height;

        var aliceBitmap = MakeBitmap(length, [0, 5]);

        var gateway = new FakeUserDataGateway([(1, "alice"), (2, "bob"), (3, "carol")]);
        var tracker = new FakeExplorationTracker(new Dictionary<string, BitArray?>
        {
            ["alice"] = aliceBitmap,
            ["bob"] = null,
            ["carol"] = null
        });
        var renderer = new CapturingRenderer();
        var service = new WorldExploredMapImageService(gateway, tracker, renderer, () => (width, height));

        service.Generate();

        Assert.NotNull(renderer.LastBitmap);
        Assert.Equal(length, renderer.LastBitmap!.Length);
        Assert.True(renderer.LastBitmap.Get(0));
        Assert.True(renderer.LastBitmap.Get(5));
        for (var i = 0; i < length; i++)
        {
            if (i == 0 || i == 5) continue;
            Assert.False(renderer.LastBitmap.Get(i));
        }
    }

    [Fact]
    public void Generate_ShouldUseWorldExploredAsFileNameSeed()
    {
        var gateway = new FakeUserDataGateway([(1, "alice")]);
        var tracker = new FakeExplorationTracker(new Dictionary<string, BitArray?>
        {
            ["alice"] = MakeBitmap(8, [0])
        });
        var renderer = new CapturingRenderer();
        var service = new WorldExploredMapImageService(gateway, tracker, renderer, () => (4, 2));

        service.Generate();

        Assert.Equal("world-explored", renderer.LastAccountName);
    }

    [Fact]
    public void Generate_ShouldRenderAllZero_WhenNoAccountsHaveData()
    {
        const int width = 4;
        const int height = 2;
        const int length = width * height;

        var gateway = new FakeUserDataGateway([(1, "alice"), (2, "bob")]);
        var tracker = new FakeExplorationTracker(new Dictionary<string, BitArray?>
        {
            ["alice"] = null,
            ["bob"] = null
        });
        var renderer = new CapturingRenderer();
        var service = new WorldExploredMapImageService(gateway, tracker, renderer, () => (width, height));

        service.Generate();

        Assert.NotNull(renderer.LastBitmap);
        Assert.Equal(length, renderer.LastBitmap!.Length);
        for (var i = 0; i < length; i++)
        {
            Assert.False(renderer.LastBitmap.Get(i));
        }
        Assert.Equal(1, renderer.CallCount);
    }

    [Fact]
    public void Generate_ShouldSkipBitmaps_WhenLengthMismatchesUnion()
    {
        const int width = 4;
        const int height = 2;
        const int length = width * height;

        var goodBitmap = MakeBitmap(length, [3]);
        var staleBitmap = MakeBitmap(length * 2, [0, length, length + 1]);

        var gateway = new FakeUserDataGateway([(1, "alice"), (2, "bob")]);
        var tracker = new FakeExplorationTracker(new Dictionary<string, BitArray?>
        {
            ["alice"] = staleBitmap,
            ["bob"] = goodBitmap
        });
        var renderer = new CapturingRenderer();
        var service = new WorldExploredMapImageService(gateway, tracker, renderer, () => (width, height));

        service.Generate();

        Assert.NotNull(renderer.LastBitmap);
        Assert.Equal(length, renderer.LastBitmap!.Length);
        Assert.True(renderer.LastBitmap.Get(3));
        for (var i = 0; i < length; i++)
        {
            if (i == 3) continue;
            Assert.False(renderer.LastBitmap.Get(i));
        }
    }

    private static BitArray MakeBitmap(int length, IReadOnlyCollection<int> setBits)
    {
        var bitmap = new BitArray(length);
        foreach (var index in setBits)
        {
            bitmap.Set(index, true);
        }
        return bitmap;
    }

    private sealed class FakeUserDataGateway(IReadOnlyList<(int AccountId, string Username)> accounts) : IUserDataGateway
    {
        public bool TryGetUserAccountId(string user, out int accountId)
        {
            accountId = default;
            return false;
        }

        public bool TryGetPlayerData(int accountId, out object playerData)
        {
            playerData = null!;
            return false;
        }

        public IReadOnlyList<(int AccountId, string Username)> GetAllUserAccounts() => accounts;
    }

    private sealed class FakeExplorationTracker(IReadOnlyDictionary<string, BitArray?> bitmaps) : IPlayerExplorationTracker
    {
        public void MarkArea(string accountName, int tileX, int tileY) { }
        public void MarkAtPosition(string accountName, int tileX, int tileY) { }
        public void ForgetLastSample(string accountName) { }
        public BitArray? GetBitmap(string accountName)
            => bitmaps.TryGetValue(accountName, out var b) ? b : null;
        public double GetExplorationPercent(string accountName) => 0.0;
        public void Load(string accountName) { }
        public void Save(string accountName) { }
        public void SaveAll() { }
    }

    private sealed class CapturingRenderer : IPlayerMapImageService
    {
        public string? LastAccountName { get; private set; }
        public BitArray? LastBitmap { get; private set; }
        public int CallCount { get; private set; }

        public (string FileName, byte[] Content) Generate(string accountName, BitArray bitmap)
        {
            LastAccountName = accountName;
            LastBitmap = bitmap;
            CallCount++;
            return ($"map-{accountName}-stub.png", [0x89, 0x50, 0x4E, 0x47]);
        }

        public (string FileName, byte[] Content) GenerateBlank(string accountName)
        {
            LastAccountName = accountName;
            LastBitmap = null;
            CallCount++;
            return ($"map-{accountName}-blank.png", [0x89, 0x50, 0x4E, 0x47]);
        }
    }
}
