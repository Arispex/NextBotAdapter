using System.Collections;
using NextBotAdapter.Rest;
using NextBotAdapter.Services;

namespace NextBotAdapter.Tests;

public sealed class UserEndpointsTests
{
    [Fact]
    public void MapImage_ReturnsOkWhenAccountExistsAndBitmapPresent()
    {
        var bitmap = new BitArray(8);
        bitmap.Set(0, true);
        var lookup = new FakeAccountLookup(("alice", "alice"));
        var tracker = new FakeExplorationTracker(bitmap);
        var playerService = new FakePlayerMapImageService(masked: ("map-alice.png", [1, 2, 3]));

        var result = UserEndpoints.MapImage("alice", playerService, tracker, lookup);

        Assert.Equal("200", result.Status);
        Assert.Equal("map-alice.png", result["fileName"]);
        Assert.Equal(Convert.ToBase64String([1, 2, 3]), result["base64"]);
        Assert.True(playerService.GenerateCalled);
        Assert.False(playerService.GenerateBlankCalled);
    }

    [Fact]
    public void MapImage_ReturnsBlackWhenAccountExistsButNoBitmap()
    {
        var lookup = new FakeAccountLookup(("alice", "alice"));
        var tracker = new FakeExplorationTracker(null);
        var playerService = new FakePlayerMapImageService(blank: ("map-alice-blank.png", [9, 9]));

        var result = UserEndpoints.MapImage("alice", playerService, tracker, lookup);

        Assert.Equal("200", result.Status);
        Assert.Equal("map-alice-blank.png", result["fileName"]);
        Assert.Equal(Convert.ToBase64String([9, 9]), result["base64"]);
        Assert.True(playerService.GenerateBlankCalled);
        Assert.False(playerService.GenerateCalled);
    }

    [Fact]
    public void MapImage_Returns400WhenUserNotFound()
    {
        var lookup = new FakeAccountLookup();
        var tracker = new FakeExplorationTracker(null);
        var playerService = new FakePlayerMapImageService();

        var result = UserEndpoints.MapImage("ghost", playerService, tracker, lookup);

        Assert.Equal("400", result.Status);
        Assert.Equal("User was not found.", result.Error);
        Assert.False(playerService.GenerateCalled);
        Assert.False(playerService.GenerateBlankCalled);
    }

    [Fact]
    public void MapImage_Returns400WhenUserIsBlank()
    {
        var result = UserEndpoints.MapImage(
            " ",
            new FakePlayerMapImageService(),
            new FakeExplorationTracker(null),
            new FakeAccountLookup());

        Assert.Equal("400", result.Status);
        Assert.Equal("Missing required route parameter 'user'.", result.Error);
    }

    [Fact]
    public void MapImage_Returns500WhenServiceThrows()
    {
        var bitmap = new BitArray(8);
        var lookup = new FakeAccountLookup(("alice", "alice"));
        var tracker = new FakeExplorationTracker(bitmap);
        var playerService = new FakePlayerMapImageService(generateException: new InvalidOperationException("render failure"));

        var result = UserEndpoints.MapImage("alice", playerService, tracker, lookup);

        Assert.Equal("500", result.Status);
        Assert.Equal("render failure", result.Error);
    }

    [Fact]
    public void MapImage_ReturnsErrorWhenDependenciesNotConfigured()
    {
        var result = UserEndpoints.MapImage("alice", null, null, null);

        Assert.Equal("500", result.Status);
        Assert.Equal("Player exploration service is not configured.", result.Error);
    }

    [Fact]
    public void MapImage_LooksUpBitmapByAccountNameNotByLoginUser()
    {
        // Regression guard: bitmap key must be the canonical account name returned
        // by IUserAccountLookup, not the raw route value.
        var bitmap = new BitArray(8);
        bitmap.Set(0, true);
        var lookup = new FakeAccountLookup((Name: "Alice", ResolvedName: "alice"));
        var tracker = new FakeExplorationTracker(bitmap);
        var playerService = new FakePlayerMapImageService(masked: ("map-alice.png", [1, 2, 3]));

        var result = UserEndpoints.MapImage("Alice", playerService, tracker, lookup);

        Assert.Equal("200", result.Status);
        Assert.Equal("alice", tracker.LastGetBitmapKey);
        Assert.True(playerService.GenerateCalled);
    }

    private sealed class FakeAccountLookup : IUserAccountLookup
    {
        private readonly Dictionary<string, string> _accounts;

        public FakeAccountLookup(params (string Name, string ResolvedName)[] accounts)
        {
            _accounts = accounts.ToDictionary(a => a.Name, a => a.ResolvedName, StringComparer.Ordinal);
        }

        public bool TryGetAccountName(string user, out string accountName)
        {
            if (_accounts.TryGetValue(user, out var resolved))
            {
                accountName = resolved;
                return true;
            }

            accountName = string.Empty;
            return false;
        }
    }

    private sealed class FakeExplorationTracker(BitArray? bitmap) : IPlayerExplorationTracker
    {
        public string? LastGetBitmapKey { get; private set; }

        public void MarkArea(string accountName, int tileX, int tileY) { }
        public void MarkAtPosition(string accountName, int tileX, int tileY) { }
        public void ForgetLastSample(string accountName) { }
        public BitArray? GetBitmap(string accountName)
        {
            LastGetBitmapKey = accountName;
            return bitmap;
        }
        public void Load(string accountName) { }
        public void Save(string accountName) { }
        public void SaveAll() { }
    }

    private sealed class FakePlayerMapImageService : IPlayerMapImageService
    {
        private readonly (string FileName, byte[] Content) _masked;
        private readonly (string FileName, byte[] Content) _blank;
        private readonly Exception? _generateException;

        public bool GenerateCalled { get; private set; }
        public bool GenerateBlankCalled { get; private set; }

        public FakePlayerMapImageService(
            (string FileName, byte[] Content)? masked = null,
            (string FileName, byte[] Content)? blank = null,
            Exception? generateException = null)
        {
            _masked = masked ?? ("map.png", []);
            _blank = blank ?? ("map-blank.png", []);
            _generateException = generateException;
        }

        public (string FileName, byte[] Content) Generate(string accountName, BitArray bitmap)
        {
            GenerateCalled = true;
            if (_generateException is not null) throw _generateException;
            return _masked;
        }

        public (string FileName, byte[] Content) GenerateBlank(string accountName)
        {
            GenerateBlankCalled = true;
            return _blank;
        }
    }
}
