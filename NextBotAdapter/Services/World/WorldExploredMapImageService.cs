using System.Collections;

namespace NextBotAdapter.Services;

public sealed class WorldExploredMapImageService : IWorldExploredMapImageService
{
    private readonly IUserDataGateway _gateway;
    private readonly IPlayerExplorationTracker _tracker;
    private readonly IPlayerMapImageService _renderer;
    private readonly Func<(int Width, int Height)> _worldSizeProvider;

    public WorldExploredMapImageService(
        IUserDataGateway gateway,
        IPlayerExplorationTracker tracker,
        IPlayerMapImageService renderer,
        Func<(int Width, int Height)> worldSizeProvider)
    {
        _gateway = gateway;
        _tracker = tracker;
        _renderer = renderer;
        _worldSizeProvider = worldSizeProvider;
    }

    public (string FileName, byte[] Content) Generate()
    {
        var (width, height) = _worldSizeProvider();
        var union = new BitArray(Math.Max(0, width * height));

        foreach (var (_, username) in _gateway.GetAllUserAccounts())
        {
            if (string.IsNullOrEmpty(username))
            {
                continue;
            }

            var bitmap = _tracker.GetBitmap(username);
            if (bitmap is not null && bitmap.Length == union.Length)
            {
                union.Or(bitmap);
            }
        }

        return _renderer.Generate("world-explored", union);
    }
}
