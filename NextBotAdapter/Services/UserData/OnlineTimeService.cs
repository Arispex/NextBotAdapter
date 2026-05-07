using System.IO;
using Newtonsoft.Json;
using NextBotAdapter.Models;

namespace NextBotAdapter.Services;

public sealed class OnlineTimeService : IOnlineTimeService
{
    private static readonly JsonSerializerSettings JsonSettings = new() { Formatting = Formatting.Indented };
    private readonly string _filePath;
    private readonly Dictionary<string, DateTime> _activeSessions = new();
    private Dictionary<string, long> _records;
    private readonly object _lock = new();
    // Serializes disk IO so concurrent EndSession / Flush / PersistAllSessions
    // calls cannot interleave their File.WriteAllText writes and corrupt the
    // store. Acquired strictly outside _lock — never under it.
    private readonly object _ioLock = new();

    public OnlineTimeService()
        : this(Path.Combine(TShockAPI.TShock.SavePath, "NextBotAdapter", "Data", "OnlineTime.json"))
    {
    }

    public OnlineTimeService(string filePath)
    {
        _filePath = filePath;
        _records = new Dictionary<string, long>(Load().Records);
    }

    public string FilePath => _filePath;

    public OnlineTimeStore Load()
    {
        EnsureDirectory();

        if (!File.Exists(_filePath))
        {
            SaveStore(OnlineTimeStore.Empty);
            PluginLogger.Info("在线时长数据文件已创建。");
            return OnlineTimeStore.Empty;
        }

        try
        {
            var store = JsonConvert.DeserializeObject<OnlineTimeStore>(File.ReadAllText(_filePath), JsonSettings);
            var result = store ?? OnlineTimeStore.Empty;
            PluginLogger.Info($"在线时长数据加载完成，共 {result.Records.Count} 条记录。");
            return result;
        }
        catch (Exception ex)
        {
            PluginLogger.Warn($"在线时长数据加载失败，已回退为空记录，原因：{ex.Message}");
            return OnlineTimeStore.Empty;
        }
    }

    public OnlineTimeStore Reload()
    {
        // IO outside the lock — keep the existing convention. The disk snapshot
        // here may already be older than the in-memory _records by the time
        // we acquire the lock, because Flush / EndSession on other threads can
        // run during the IO window.
        var store = Load();

        lock (_lock)
        {
            // Merge instead of replace: _records is monotonically increasing
            // (StartSession / Flush / EndSession only ever add elapsed time),
            // so per-account max(in-memory, disk) preserves the freshest value.
            // A wholesale replacement here would silently erase any Flush
            // delta that landed in memory while Load was reading the file.
            foreach (var (username, seconds) in store.Records)
            {
                if (!_records.TryGetValue(username, out var current) || seconds > current)
                {
                    _records[username] = seconds;
                }
            }
        }

        return store;
    }

    public void Save()
    {
        // Take the records snapshot UNDER _ioLock so concurrent EndSession /
        // Flush / PersistAllSessions calls cannot interleave their snapshots
        // and let an older snapshot overwrite a newer write. _ioLock serializes
        // both the snapshot and the WriteAllText call.
        lock (_ioLock)
        {
            OnlineTimeStore snapshot;
            lock (_lock)
            {
                snapshot = new OnlineTimeStore(new Dictionary<string, long>(_records));
            }
            SaveStore(snapshot);
        }
    }

    public void StartSession(string username)
    {
        lock (_lock)
        {
            _activeSessions[username] = DateTime.UtcNow;
        }

        PluginLogger.Info($"玩家 {username} 开始计时在线时长。");
    }

    public void EndSession(string username)
    {
        long elapsed;
        bool ended;

        lock (_lock)
        {
            if (!_activeSessions.TryGetValue(username, out var start))
            {
                return;
            }

            _activeSessions.Remove(username);
            elapsed = (long)(DateTime.UtcNow - start).TotalSeconds;
            _records[username] = _records.TryGetValue(username, out var existing) ? existing + elapsed : elapsed;
            ended = true;
        }

        if (ended)
        {
            // IO outside _lock so concurrent stamp / read paths are not blocked
            // by disk latency. Snapshot is taken UNDER _ioLock so concurrent
            // EndSession / Flush calls cannot let an older snapshot overwrite a
            // newer write — both snapshot and WriteAllText are serialized.
            lock (_ioLock)
            {
                OnlineTimeStore snapshot;
                lock (_lock)
                {
                    snapshot = new OnlineTimeStore(new Dictionary<string, long>(_records));
                }
                SaveStore(snapshot);
            }
        }

        PluginLogger.Info($"玩家 {username} 本次在线 {elapsed} 秒，已累计保存。");
    }

    public long GetTotalSeconds(string username)
    {
        lock (_lock)
        {
            var persisted = _records.TryGetValue(username, out var seconds) ? seconds : 0;
            if (_activeSessions.TryGetValue(username, out var start))
            {
                persisted += (long)(DateTime.UtcNow - start).TotalSeconds;
            }

            return persisted;
        }
    }

    public IReadOnlyList<(string Username, long OnlineSeconds)> GetAllRecords()
    {
        lock (_lock)
        {
            var result = new Dictionary<string, long>(_records);
            foreach (var (username, start) in _activeSessions)
            {
                var elapsed = (long)(DateTime.UtcNow - start).TotalSeconds;
                result[username] = (result.TryGetValue(username, out var existing) ? existing : 0) + elapsed;
            }

            return result
                .Select(kv => (kv.Key, kv.Value))
                .OrderByDescending(x => x.Item2)
                .ToList();
        }
    }

    public void PersistAllSessions()
    {
        lock (_ioLock)
        {
            OnlineTimeStore snapshot;
            lock (_lock)
            {
                foreach (var (username, start) in _activeSessions)
                {
                    var elapsed = (long)(DateTime.UtcNow - start).TotalSeconds;
                    _records[username] = (_records.TryGetValue(username, out var existing) ? existing : 0) + elapsed;
                }

                _activeSessions.Clear();
                snapshot = new OnlineTimeStore(new Dictionary<string, long>(_records));
            }
            SaveStore(snapshot);
        }
    }

    /// <summary>
    /// Periodic flush path used by the background persistence timer. Accumulates
    /// elapsed seconds for every active session into <see cref="_records"/> and
    /// resets each session's start to UtcNow so the next flush / EndSession only
    /// counts the new delta. Active sessions are intentionally NOT cleared —
    /// players are still online and will be ended via <see cref="EndSession"/>
    /// or <see cref="PersistAllSessions"/>.
    /// </summary>
    public void Flush()
    {
        lock (_ioLock)
        {
            OnlineTimeStore snapshot;
            lock (_lock)
            {
                var now = DateTime.UtcNow;
                // Snapshot session entries via ToArray so we are not iterating
                // a dictionary while mutating its values (the value
                // reassignment below is technically allowed by Dictionary, but
                // the ToArray copy keeps the loop body explicit and harder to
                // misread).
                foreach (var (name, start) in _activeSessions.ToArray())
                {
                    var elapsed = (long)(now - start).TotalSeconds;
                    _records[name] = (_records.TryGetValue(name, out var existing) ? existing : 0) + elapsed;
                    _activeSessions[name] = now;
                }

                snapshot = new OnlineTimeStore(new Dictionary<string, long>(_records));
            }
            SaveStore(snapshot);
        }
    }

    private void SaveStore(OnlineTimeStore store)
    {
        EnsureDirectory();
        File.WriteAllText(_filePath, JsonConvert.SerializeObject(store, JsonSettings));
    }

    private void EnsureDirectory()
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (directory is not null)
        {
            Directory.CreateDirectory(directory);
        }
    }
}
