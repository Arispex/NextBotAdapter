using System.Collections;
using System.Numerics;

namespace NextBotAdapter.Services;

public sealed class PlayerExplorationTracker : IPlayerExplorationTracker
{
    /// <summary>
    /// Horizontal half-extent (tiles) of the simulated reveal box.
    /// Sized for 1080p default zoom: full screen ~120 tile wide; half-extent 70 + ~10% margin.
    /// </summary>
    public const int RevealHalfExtentX = 70;

    /// <summary>
    /// Vertical half-extent (tiles) of the simulated reveal box.
    /// Sized so the box aspect (2*HalfX+1) / (2*HalfY+1) ≈ 1.62, matching the
    /// width:height ratio observed empirically from a 1080p client screenshot.
    /// </summary>
    public const int RevealHalfExtentY = 43;

    /// <summary>
    /// Distance (in tiles) above which two consecutive samples are treated as a
    /// teleport (mirror / magic conch / recall / hellevator) instead of continuous
    /// movement; only the endpoint is stamped in that case. 500 covers
    /// network-batched high-speed flight (a single coalesced packet under heavy
    /// lag) while still catching real teleports, which are typically 1000+ tile.
    /// </summary>
    public const int TeleportThresholdTiles = 500;

    /// <summary>
    /// Step (in tiles) used when bridging two consecutive samples with intermediate
    /// stamps. Must be &lt;= min(<see cref="RevealHalfExtentX"/>, <see cref="RevealHalfExtentY"/>)
    /// so adjacent reveal boxes overlap and leave no visible gap along the line.
    /// </summary>
    public const int InterpolationStepTiles = 20;

    private readonly Dictionary<string, BitArray> _bitmaps = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (int X, int Y)> _lastSamples = new(StringComparer.Ordinal);
    private readonly HashSet<string> _missingFiles = new(StringComparer.Ordinal);
    // Accounts whose in-memory bitmap has been mutated since the last successful
    // persist. Both Save(name) and SaveAll() consult and clear this set, then
    // re-add on storage failure so the next flush retries. Lazy-load / Load do
    // NOT mark dirty (data already mirrors disk).
    private readonly HashSet<string> _dirty = new(StringComparer.Ordinal);
    private readonly object _lock = new();
    private readonly IExplorationStorage _storage;
    private readonly Func<(int Width, int Height)> _worldSizeProvider;

    public PlayerExplorationTracker(IExplorationStorage storage, Func<(int Width, int Height)> worldSizeProvider)
    {
        _storage = storage;
        _worldSizeProvider = worldSizeProvider;
    }

    public void MarkArea(string accountName, int tileX, int tileY)
    {
        if (string.IsNullOrWhiteSpace(accountName))
        {
            return;
        }

        var (width, height) = _worldSizeProvider();
        if (width <= 0 || height <= 0)
        {
            return;
        }

        lock (_lock)
        {
            var bitmap = GetOrCreateBitmapLocked(accountName, width, height);
            MarkBox(bitmap, width, height, tileX, tileY);
            _dirty.Add(accountName);
        }
    }

    /// <summary>
    /// Sample at world tile coord. Internally bridges from the previous sample to
    /// avoid gaps under low-packet-rate or fast movement; resets to a single stamp
    /// when the jump exceeds <see cref="TeleportThresholdTiles"/>.
    /// </summary>
    public void MarkAtPosition(string accountName, int tileX, int tileY)
    {
        if (string.IsNullOrWhiteSpace(accountName))
        {
            return;
        }

        var (width, height) = _worldSizeProvider();
        if (width <= 0 || height <= 0)
        {
            return;
        }

        lock (_lock)
        {
            var bitmap = GetOrCreateBitmapLocked(accountName, width, height);

            if (!_lastSamples.TryGetValue(accountName, out var prev))
            {
                MarkBox(bitmap, width, height, tileX, tileY);
                _lastSamples[accountName] = (tileX, tileY);
                _dirty.Add(accountName);
                return;
            }

            var dx = tileX - prev.X;
            var dy = tileY - prev.Y;
            var distance = Math.Sqrt((double)dx * dx + (double)dy * dy);

            if (distance == 0d)
            {
                return;
            }

            if (distance > TeleportThresholdTiles)
            {
                MarkBox(bitmap, width, height, tileX, tileY);
                _lastSamples[accountName] = (tileX, tileY);
                _dirty.Add(accountName);
                return;
            }

            var steps = Math.Max(1, (int)Math.Ceiling(distance / InterpolationStepTiles));
            for (var i = 0; i <= steps; i++)
            {
                var t = (double)i / steps;
                var ix = prev.X + (int)Math.Round(dx * t);
                var iy = prev.Y + (int)Math.Round(dy * t);
                MarkBox(bitmap, width, height, ix, iy);
            }

            _lastSamples[accountName] = (tileX, tileY);
            _dirty.Add(accountName);
        }
    }

    /// <summary>
    /// Forget the last-sampled position for this player so the next sample starts
    /// fresh (e.g. on logout / leave; prevents a phantom line from old position to
    /// next login spawn).
    /// </summary>
    public void ForgetLastSample(string accountName)
    {
        if (string.IsNullOrWhiteSpace(accountName))
        {
            return;
        }

        lock (_lock)
        {
            _lastSamples.Remove(accountName);
        }
    }

    public BitArray? GetBitmap(string accountName)
    {
        if (string.IsNullOrWhiteSpace(accountName))
        {
            return null;
        }

        lock (_lock)
        {
            // Return a snapshot copy so renderers can iterate safely while PlayerUpdate
            // continues to mutate the live bitmap on a different thread.
            if (_bitmaps.TryGetValue(accountName, out var bitmap))
            {
                return new BitArray(bitmap);
            }

            // Negative cache hit: a previous lazy-load probed storage and found
            // nothing. Skip the redundant disk probe — leaderboard fan-out across
            // many accounts without bitmap files would otherwise re-probe each one.
            if (_missingFiles.Contains(accountName))
            {
                return null;
            }
        }

        // Cache miss: try lazy-load from disk so REST queries can return the latest
        // persisted bitmap even before the player has logged in this session.
        // IO is intentionally outside _lock to avoid blocking concurrent stamp calls.
        var (width, height) = _worldSizeProvider();
        if (width <= 0 || height <= 0)
        {
            return null;
        }

        var result = _storage.Load(accountName, width * height);

        lock (_lock)
        {
            // Double-check: another thread (e.g. OnPlayerPostLogin) may have loaded
            // the bitmap between our two locks. Honor whichever instance is already
            // in the dictionary so stamps and snapshots see the same BitArray.
            if (_bitmaps.TryGetValue(accountName, out var existing))
            {
                return new BitArray(existing);
            }
            if (result.Bitmap is null)
            {
                // Only register a confirmed missing-file as a negative cache hit.
                // Transient IO failures (e.g. NFS hiccup) and corrupt/partial files
                // must NOT poison the cache — next call will retry the IO.
                if (result.FileMissing)
                {
                    _missingFiles.Add(accountName);
                }
                return null;
            }
            _bitmaps[accountName] = result.Bitmap;
            _missingFiles.Remove(accountName);
            return new BitArray(result.Bitmap);
        }
    }

    public double GetExplorationPercent(string accountName)
    {
        var bitmap = GetBitmap(accountName);
        if (bitmap is null || bitmap.Length == 0)
        {
            return 0.0;
        }

        var ints = new int[(bitmap.Length + 31) / 32];
        bitmap.CopyTo(ints, 0);

        var explored = 0;
        foreach (var v in ints)
        {
            explored += BitOperations.PopCount((uint)v);
        }

        return Math.Round(100.0 * explored / bitmap.Length, 2);
    }

    public void Load(string accountName)
    {
        if (string.IsNullOrWhiteSpace(accountName))
        {
            return;
        }

        var (width, height) = _worldSizeProvider();
        if (width <= 0 || height <= 0)
        {
            return;
        }

        var expectedBitCount = width * height;
        var result = _storage.Load(accountName, expectedBitCount);

        bool inserted = false;
        lock (_lock)
        {
            // Preserve any in-memory bitmap already populated via lazy-load or stamp
            // path so accumulated stamps from this session aren't silently erased.
            if (_bitmaps.ContainsKey(accountName))
            {
                return;
            }

            if (result.Bitmap is null)
            {
                // Mirror GetBitmap's negative-cache policy: only confirmed-missing
                // files seed _missingFiles. Transient IO errors / corrupt files are
                // not negative-cached so the next call retries IO.
                if (result.FileMissing)
                {
                    _missingFiles.Add(accountName);
                }
                return;
            }

            _bitmaps[accountName] = result.Bitmap;
            _missingFiles.Remove(accountName);
            inserted = true;
        }

        // Log outside _lock — only when a bitmap was actually pulled in this call.
        if (inserted)
        {
            PluginLogger.Info($"加载玩家探索数据成功，accountName={accountName}");
        }
    }

    public void Save(string accountName)
    {
        if (string.IsNullOrWhiteSpace(accountName))
        {
            return;
        }

        BitArray snapshot;
        lock (_lock)
        {
            // Dirty short-circuit: bitmap unchanged since last persist — skip IO.
            if (!_dirty.Contains(accountName))
            {
                return;
            }
            if (!_bitmaps.TryGetValue(accountName, out var bitmap))
            {
                // Dirty entry without a backing bitmap is unexpected; drop the
                // dirty mark so we don't spin retrying.
                _dirty.Remove(accountName);
                return;
            }
            snapshot = new BitArray(bitmap);
            _dirty.Remove(accountName);
        }

        if (!_storage.Save(accountName, snapshot))
        {
            // Storage failed: re-mark dirty so the next flush retries.
            lock (_lock)
            {
                _dirty.Add(accountName);
            }
        }
    }

    public void SaveAll(string contextLabel = "保存")
    {
        Dictionary<string, BitArray> snapshot;
        lock (_lock)
        {
            // Snapshot only dirty entries that still have a live bitmap. Clearing
            // _dirty here is the commit point — any concurrent stamp after this
            // point re-marks the account dirty for the next flush.
            snapshot = new Dictionary<string, BitArray>(_dirty.Count, StringComparer.Ordinal);
            foreach (var name in _dirty)
            {
                if (_bitmaps.TryGetValue(name, out var bitmap))
                {
                    snapshot[name] = new BitArray(bitmap);
                }
            }
            _dirty.Clear();
        }

        var success = 0;
        var failure = 0;
        var failedNames = new List<string>();
        foreach (var (name, bitmap) in snapshot)
        {
            if (_storage.Save(name, bitmap))
            {
                success++;
            }
            else
            {
                failure++;
                failedNames.Add(name);
            }
        }

        if (failedNames.Count > 0)
        {
            // Re-mark failed accounts so the next flush retries them.
            lock (_lock)
            {
                foreach (var name in failedNames)
                {
                    _dirty.Add(name);
                }
            }
        }

        if (failure > 0)
        {
            PluginLogger.Warn($"{contextLabel}完成，成功={success}，失败={failure}");
        }
        else if (success > 0)
        {
            PluginLogger.Info($"{contextLabel}完成，成功={success}");
        }
        // success == 0 && failure == 0：无脏数据，不打日志
    }

    private static void MarkBox(BitArray bitmap, int width, int height, int tileX, int tileY)
    {
        var minX = Math.Max(0, tileX - RevealHalfExtentX);
        var maxX = Math.Min(width - 1, tileX + RevealHalfExtentX);
        var minY = Math.Max(0, tileY - RevealHalfExtentY);
        var maxY = Math.Min(height - 1, tileY + RevealHalfExtentY);

        if (minX > maxX || minY > maxY)
        {
            return;
        }

        for (var y = minY; y <= maxY; y++)
        {
            var rowOffset = y * width;
            for (var x = minX; x <= maxX; x++)
            {
                bitmap.Set(rowOffset + x, true);
            }
        }
    }

    /// <summary>
    /// Caller must already hold <see cref="_lock"/>. Returns the current
    /// in-memory bitmap, creating a fresh empty one on first encounter so the
    /// stamp callsite can mutate atomically without releasing the lock.
    /// </summary>
    private BitArray GetOrCreateBitmapLocked(string accountName, int width, int height)
    {
        if (_bitmaps.TryGetValue(accountName, out var existing))
        {
            return existing;
        }

        var created = new BitArray(width * height);
        _bitmaps[accountName] = created;
        // Defensive: if a prior GetBitmap had registered this account in the
        // negative cache (missing-file probe), clear it now that we have a
        // live in-memory bitmap. GetBitmap also checks _bitmaps first, so this
        // is belt-and-suspenders.
        _missingFiles.Remove(accountName);
        return created;
    }
}
