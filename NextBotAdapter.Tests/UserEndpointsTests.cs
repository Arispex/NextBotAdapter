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
        var lookup = new FakeAccountLookup(("alice", "uuid-1"));
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
        var lookup = new FakeAccountLookup(("alice", "uuid-1"));
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
        var lookup = new FakeAccountLookup(("alice", "uuid-1"));
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

    private sealed class FakeAccountLookup : IUserAccountLookup
    {
        private readonly Dictionary<string, string> _accounts;

        public FakeAccountLookup(params (string Name, string Uuid)[] accounts)
        {
            _accounts = accounts.ToDictionary(a => a.Name, a => a.Uuid, StringComparer.Ordinal);
        }

        public bool TryGetAccountUuid(string user, out string accountUuid)
        {
            if (_accounts.TryGetValue(user, out var uuid))
            {
                accountUuid = uuid;
                return true;
            }

            accountUuid = string.Empty;
            return false;
        }
    }

    private sealed class FakeExplorationTracker(BitArray? bitmap) : IPlayerExplorationTracker
    {
        public void MarkArea(string accountUuid, int tileX, int tileY) { }
        public void MarkAtPosition(string accountUuid, int tileX, int tileY) { }
        public void ForgetLastSample(string accountUuid) { }
        public BitArray? GetBitmap(string accountUuid) => bitmap;
        public void Load(string accountUuid) { }
        public void Save(string accountUuid) { }
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
