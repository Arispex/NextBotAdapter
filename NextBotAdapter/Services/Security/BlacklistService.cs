using System.IO;
using Newtonsoft.Json;
using NextBotAdapter.Models;

namespace NextBotAdapter.Services;

public sealed class BlacklistService : IBlacklistService
{
    private static readonly JsonSerializerSettings JsonSettings = new()
    {
        Formatting = Formatting.Indented
    };
    private readonly PluginConfigService? _configService;
    private readonly string? _filePath;
    private readonly object _lock = new();
    private List<BlacklistEntry> _entries;
    private BlacklistSettings _settings;

    public BlacklistService(PluginConfigService configService)
        : this(configService, Path.Combine(configService.DataDirectoryPath, "Blacklist.json"))
    {
    }

    public BlacklistService(PluginConfigService configService, string filePath)
    {
        _configService = configService;
        _filePath = filePath;
        _entries = [];
        _settings = BlacklistSettings.Default;
        Reload();
    }

    public BlacklistService(BlacklistSettings settings, BlacklistStore store)
    {
        _settings = settings;
        _entries = store.Entries.ToList();
    }

    public string? FilePath => _filePath;

    public BlacklistSettings Settings
    {
        get
        {
            lock (_lock)
            {
                return _settings;
            }
        }
    }

    public BlacklistStore Load()
    {
        if (_filePath is null)
        {
            lock (_lock)
            {
                return new BlacklistStore(_entries.ToArray());
            }
        }

        EnsureDirectory();
        if (!File.Exists(_filePath))
        {
            WriteStore(BlacklistStore.Empty);
            PluginLogger.Info("黑名单数据文件已创建。");
            return BlacklistStore.Empty;
        }

        try
        {
            var store = JsonConvert.DeserializeObject<BlacklistStore>(File.ReadAllText(_filePath), JsonSettings);
            var blacklist = store ?? BlacklistStore.Empty;
            PluginLogger.Info($"黑名单数据加载完成，共 {blacklist.Entries.Count} 个条目。");
            return blacklist;
        }
        catch (Exception ex)
        {
            PluginLogger.Warn($"黑名单数据加载失败，已回退为空黑名单，原因：{ex.Message}");
            return BlacklistStore.Empty;
        }
    }

    public BlacklistStore Reload()
    {
        var settings = _configService?.LoadBlacklistSettings() ?? Settings;
        var store = Load();

        lock (_lock)
        {
            _settings = settings;
            _entries = store.Entries.ToList();
        }

        return store;
    }

    public void Save()
    {
        if (_filePath is null)
        {
            return;
        }

        BlacklistStore store;
        lock (_lock)
        {
            store = new BlacklistStore(_entries.ToArray());
        }

        WriteStore(store);
        PluginLogger.Info($"黑名单数据保存完成，共 {store.Entries.Count} 个条目。");
    }

    public IReadOnlyList<BlacklistEntry> GetAll()
    {
        lock (_lock)
        {
            return _entries.ToArray();
        }
    }

    public bool IsBlacklisted(string user)
    {
        lock (_lock)
        {
            if (!_settings.Enabled)
            {
                return false;
            }

            return _entries.Any(e => string.Equals(e.Username, user, StringComparison.OrdinalIgnoreCase));
        }
    }

    public bool TryAdd(string user, string reason, out string? error)
    {
        if (string.IsNullOrWhiteSpace(user))
        {
            error = "Blacklist user is invalid.";
            return false;
        }

        lock (_lock)
        {
            if (_entries.Any(e => string.Equals(e.Username, user, StringComparison.OrdinalIgnoreCase)))
            {
                error = "User already exists in blacklist.";
                if (_filePath is not null)
                {
                    PluginLogger.Warn($"玩家 {user} 加入黑名单失败，原因：{error}");
                }

                return false;
            }

            _entries.Add(new BlacklistEntry(user, reason));
        }

        error = null;
        if (_filePath is not null)
        {
            Save();
            PluginLogger.Info($"玩家 {user} 已加入黑名单，原因：{reason}");
        }

        return true;
    }

    public bool TryRemove(string user, out string? error)
    {
        if (string.IsNullOrWhiteSpace(user))
        {
            error = "Blacklist user is invalid.";
            return false;
        }

        lock (_lock)
        {
            var existing = _entries.FirstOrDefault(e => string.Equals(e.Username, user, StringComparison.OrdinalIgnoreCase));
            if (existing is null)
            {
                error = "User not found in blacklist.";
                if (_filePath is not null)
                {
                    PluginLogger.Warn($"玩家 {user} 移出黑名单失败，原因：{error}");
                }

                return false;
            }

            _entries.Remove(existing);
        }

        error = null;
        if (_filePath is not null)
        {
            Save();
            PluginLogger.Info($"玩家 {user} 已从黑名单移除。");
        }

        return true;
    }

    public bool TryValidateJoin(string user, out string? denialReason)
    {
        lock (_lock)
        {
            denialReason = null;
            if (!_settings.Enabled)
            {
                return true;
            }

            var entry = _entries.FirstOrDefault(e => string.Equals(e.Username, user, StringComparison.OrdinalIgnoreCase));
            if (entry is null)
            {
                return true;
            }

            denialReason = _settings.DenyMessage.Replace("{reason}", entry.Reason);
            return false;
        }
    }

    private void EnsureDirectory()
    {
        if (_filePath is null)
        {
            return;
        }

        var directory = Path.GetDirectoryName(_filePath);
        if (directory is not null)
        {
            Directory.CreateDirectory(directory);
        }
    }

    private void WriteStore(BlacklistStore store)
    {
        if (_filePath is null)
        {
            return;
        }

        EnsureDirectory();
        File.WriteAllText(_filePath, JsonConvert.SerializeObject(store, JsonSettings));
    }
}
