using System.Collections;
using NextBotAdapter.Infrastructure;
using NextBotAdapter.Rest;
using NextBotAdapter.Services;
using Rests;

namespace NextBotAdapter.Tests;

public sealed class MapEndpointsTests
{
    [Fact]
    public void Image_ShouldReturnOkWhenGenerationSucceeds()
    {
        var service = new FakeMapImageService(("map-1.png", [1, 2, 3]));

        var result = MapEndpoints.Image(service);

        Assert.Equal("200", result.Status);
        Assert.Equal("map-1.png", result["fileName"]);
        Assert.Equal(Convert.ToBase64String([1, 2, 3]), result["base64"]);
    }

    [Fact]
    public void Image_ShouldReturnServerErrorWhenGenerationThrows()
    {
        var service = new ThrowingMapImageService(new InvalidOperationException("map generation failed"));

        var result = MapEndpoints.Image(service);

        Assert.Equal("500", result.Status);
        Assert.Equal("map generation failed", result.Error);
    }

    [Fact]
    public void ImageForPlayer_ShouldReturn400WhenUserNotFound()
    {
        var lookup = new FakeAccountLookup(); // no accounts
        var tracker = new FakeExplorationTracker(null);
        var playerService = new FakePlayerMapImageService();

        var result = MapEndpoints.ImageForPlayer("ghost", playerService, tracker, lookup);

        Assert.Equal("400", result.Status);
        Assert.Equal("User was not found.", result.Error);
        Assert.False(playerService.GenerateCalled);
        Assert.False(playerService.GenerateBlankCalled);
    }

    [Fact]
    public void ImageForPlayer_ShouldReturnBlankWhenAccountExistsButNoBitmap()
    {
        var lookup = new FakeAccountLookup(("alice", "uuid-1"));
        var tracker = new FakeExplorationTracker(null);
        var playerService = new FakePlayerMapImageService(blank: ("map-alice-blank.png", [9, 9]));

        var result = MapEndpoints.ImageForPlayer("alice", playerService, tracker, lookup);

        Assert.Equal("200", result.Status);
        Assert.Equal("map-alice-blank.png", result["fileName"]);
        Assert.Equal(Convert.ToBase64String([9, 9]), result["base64"]);
        Assert.True(playerService.GenerateBlankCalled);
        Assert.False(playerService.GenerateCalled);
    }

    [Fact]
    public void ImageForPlayer_ShouldReturnGeneratedWhenBitmapPresent()
    {
        var bitmap = new BitArray(8);
        bitmap.Set(0, true);
        var lookup = new FakeAccountLookup(("alice", "uuid-1"));
        var tracker = new FakeExplorationTracker(bitmap);
        var playerService = new FakePlayerMapImageService(masked: ("map-alice.png", [1, 2, 3]));

        var result = MapEndpoints.ImageForPlayer("alice", playerService, tracker, lookup);

        Assert.Equal("200", result.Status);
        Assert.Equal("map-alice.png", result["fileName"]);
        Assert.Equal(Convert.ToBase64String([1, 2, 3]), result["base64"]);
        Assert.True(playerService.GenerateCalled);
        Assert.False(playerService.GenerateBlankCalled);
    }

    [Fact]
    public void ImageForPlayer_ShouldReturn400WhenPlayerIsBlank()
    {
        var result = MapEndpoints.ImageForPlayer(" ", new FakePlayerMapImageService(), new FakeExplorationTracker(null), new FakeAccountLookup());

        Assert.Equal("400", result.Status);
    }

    [Fact]
    public void ImageForPlayer_ShouldReturn500WhenDependenciesUnconfigured()
    {
        var result = MapEndpoints.ImageForPlayer("alice", null, null, null);

        Assert.Equal("500", result.Status);
    }

    [Fact]
    public void ImageForPlayer_ShouldReturn500WhenServiceThrows()
    {
        var bitmap = new BitArray(8);
        var lookup = new FakeAccountLookup(("alice", "uuid-1"));
        var tracker = new FakeExplorationTracker(bitmap);
        var playerService = new FakePlayerMapImageService(generateException: new InvalidOperationException("render failure"));

        var result = MapEndpoints.ImageForPlayer("alice", playerService, tracker, lookup);

        Assert.Equal("500", result.Status);
        Assert.Equal("render failure", result.Error);
    }

    private sealed class FakeMapImageService((string FileName, byte[] Content) result) : IMapImageService
    {
        public (string FileName, byte[] Content) Generate() => result;
    }

    private sealed class ThrowingMapImageService(Exception exception) : IMapImageService
    {
        public (string FileName, byte[] Content) Generate() => throw exception;
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
