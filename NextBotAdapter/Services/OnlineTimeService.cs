using NextBotAdapter.Models;

namespace NextBotAdapter.Services;

public sealed class OnlineTimeService : IOnlineTimeService
{
    private readonly IOnlineTimeFileService _fileService;
    private readonly Dictionary<string, DateTime> _activeSessions = new();
    private Dictionary<string, long> _records;
    private readonly object _lock = new();

    public OnlineTimeService(IOnlineTimeFileService fileService)
    {
        _fileService = fileService;
        var store = fileService.Load();
        _records = new Dictionary<string, long>(store.Records);
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

        lock (_lock)
        {
            if (!_activeSessions.TryGetValue(username, out var start))
            {
                return;
            }

            _activeSessions.Remove(username);
            elapsed = (long)(DateTime.UtcNow - start).TotalSeconds;
            _records[username] = _records.TryGetValue(username, out var existing) ? existing + elapsed : elapsed;
            Persist();
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
        lock (_lock)
        {
            foreach (var (username, start) in _activeSessions)
            {
                var elapsed = (long)(DateTime.UtcNow - start).TotalSeconds;
                _records[username] = (_records.TryGetValue(username, out var existing) ? existing : 0) + elapsed;
            }

            _activeSessions.Clear();
            Persist();
        }

        PluginLogger.Info("所有在线玩家的时长已持久化。");
    }

    private void Persist()
    {
        _fileService.Save(new OnlineTimeStore(_records));
    }
}
