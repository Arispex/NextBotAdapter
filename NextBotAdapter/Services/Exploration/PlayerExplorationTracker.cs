using System.Collections;

namespace NextBotAdapter.Services;

public sealed class PlayerExplorationTracker : IPlayerExplorationTracker
{
    /// <summary>
    /// Vanilla Terraria reveals roughly a 41x41 tile rectangle around the player.
    /// Half-extent (radius) of 20 yields 41 tiles per side including the center.
    /// </summary>
    public const int RevealHalfExtent = 20;

    private readonly Dictionary<string, BitArray> _bitmaps = new(StringComparer.Ordinal);
    private readonly object _lock = new();
    private readonly IExplorationStorage _storage;
    private readonly Func<(int Width, int Height)> _worldSizeProvider;

    public PlayerExplorationTracker(IExplorationStorage storage, Func<(int Width, int Height)> worldSizeProvider)
    {
        _storage = storage;
        _worldSizeProvider = worldSizeProvider;
    }

    public void MarkArea(string accountUuid, int tileX, int tileY)
    {
        if (string.IsNullOrWhiteSpace(accountUuid))
        {
            return;
        }

        var (width, height) = _worldSizeProvider();
        if (width <= 0 || height <= 0)
        {
            return;
        }

        var bitmap = GetOrCreateBitmap(accountUuid, width, height);

        var minX = Math.Max(0, tileX - RevealHalfExtent);
        var maxX = Math.Min(width - 1, tileX + RevealHalfExtent);
        var minY = Math.Max(0, tileY - RevealHalfExtent);
        var maxY = Math.Min(height - 1, tileY + RevealHalfExtent);

        if (minX > maxX || minY > maxY)
        {
            return;
        }

        lock (_lock)
        {
            for (var y = minY; y <= maxY; y++)
            {
                var rowOffset = y * width;
                for (var x = minX; x <= maxX; x++)
                {
                    bitmap.Set(rowOffset + x, true);
                }
            }
        }
    }

    public BitArray? GetBitmap(string accountUuid)
    {
        if (string.IsNullOrWhiteSpace(accountUuid))
        {
            return null;
        }

        lock (_lock)
        {
            // Return a snapshot copy so renderers can iterate safely while PlayerUpdate
            // continues to mutate the live bitmap on a different thread.
            return _bitmaps.TryGetValue(accountUuid, out var bitmap) ? new BitArray(bitmap) : null;
        }
    }

    public void Load(string accountUuid)
    {
        if (string.IsNullOrWhiteSpace(accountUuid))
        {
            return;
        }

        var (width, height) = _worldSizeProvider();
        if (width <= 0 || height <= 0)
        {
            return;
        }

        var expectedBitCount = width * height;
        var bitmap = _storage.Load(accountUuid, expectedBitCount);
        if (bitmap is null)
        {
            return;
        }

        lock (_lock)
        {
            _bitmaps[accountUuid] = bitmap;
        }

        PluginLogger.Info($"加载玩家探索数据成功，accountUuid={accountUuid}");
    }

    public void Save(string accountUuid)
    {
        if (string.IsNullOrWhiteSpace(accountUuid))
        {
            return;
        }

        BitArray? snapshot;
        lock (_lock)
        {
            if (!_bitmaps.TryGetValue(accountUuid, out var bitmap))
            {
                return;
            }
            snapshot = new BitArray(bitmap);
        }

        _storage.Save(accountUuid, snapshot);
    }

    public void SaveAll()
    {
        Dictionary<string, BitArray> snapshot;
        lock (_lock)
        {
            snapshot = new Dictionary<string, BitArray>(_bitmaps.Count, StringComparer.Ordinal);
            foreach (var (uuid, bitmap) in _bitmaps)
            {
                snapshot[uuid] = new BitArray(bitmap);
            }
        }

        foreach (var (uuid, bitmap) in snapshot)
        {
            _storage.Save(uuid, bitmap);
        }
    }

    private BitArray GetOrCreateBitmap(string accountUuid, int width, int height)
    {
        lock (_lock)
        {
            if (_bitmaps.TryGetValue(accountUuid, out var bitmap))
            {
                return bitmap;
            }

            var created = new BitArray(width * height);
            _bitmaps[accountUuid] = created;
            return created;
        }
    }
}
