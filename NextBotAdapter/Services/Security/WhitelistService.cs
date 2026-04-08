using System.IO;
using Newtonsoft.Json;
using NextBotAdapter.Models;

namespace NextBotAdapter.Services;

public sealed class WhitelistService : IWhitelistService
{
    private static readonly JsonSerializerSettings JsonSettings = new()
    {
        Formatting = Formatting.Indented
    };
    private readonly PluginConfigService? _configService;
    private readonly string? _filePath;
    private readonly object _lock = new();
    private List<string> _users;
    private WhitelistSettings _settings;

    public WhitelistService(PluginConfigService configService)
        : this(configService, Path.Combine(configService.DataDirectoryPath, "Whitelist.json"))
    {
    }

    public WhitelistService(PluginConfigService configService, string filePath)
    {
        _configService = configService;
        _filePath = filePath;
        _users = [];
        _settings = WhitelistSettings.Default;
        Reload();
    }

    public WhitelistService(WhitelistSettings settings, WhitelistStore store)
    {
        _settings = settings;
        _users = store.Users.ToList();
    }

    public string? FilePath => _filePath;

    public WhitelistSettings Settings
    {
        get
        {
            lock (_lock)
            {
                return _settings;
            }
        }
    }

    public WhitelistStore Load()
    {
        if (_filePath is null)
        {
            lock (_lock)
            {
                return new WhitelistStore(_users.ToArray());
            }
        }

        EnsureDirectory();
        if (!File.Exists(_filePath))
        {
            WriteStore(WhitelistStore.Empty);
            PluginLogger.Info("白名单数据文件已创建。");
            return WhitelistStore.Empty;
        }

        try
        {
            var store = JsonConvert.DeserializeObject<WhitelistStore>(File.ReadAllText(_filePath), JsonSettings);
            var whitelist = store ?? WhitelistStore.Empty;
            PluginLogger.Info($"白名单数据加载完成，共 {whitelist.Users.Count} 个条目。");
            return whitelist;
        }
        catch (Exception ex)
        {
            PluginLogger.Warn($"白名单数据加载失败，已回退为空白名单，原因：{ex.Message}");
            return WhitelistStore.Empty;
        }
    }

    public WhitelistStore Reload()
    {
        var settings = _configService?.LoadWhitelistSettings() ?? Settings;
        var store = Load();

        lock (_lock)
        {
            _settings = settings;
            _users = store.Users.ToList();
        }

        return store;
    }

    public void Save()
    {
        if (_filePath is null)
        {
            return;
        }

        WhitelistStore store;
        lock (_lock)
        {
            store = new WhitelistStore(_users.ToArray());
        }

        WriteStore(store);
        PluginLogger.Info($"白名单数据保存完成，共 {store.Users.Count} 个条目。");
    }

    public IReadOnlyList<string> GetAll()
    {
        lock (_lock)
        {
            return _users.ToArray();
        }
    }

    public bool IsWhitelisted(string user)
    {
        lock (_lock)
        {
            if (!_settings.Enabled)
            {
                return true;
            }

            var comparer = CreateComparer(_settings.CaseSensitive);
            return _users.Contains(user, comparer);
        }
    }

    public bool TryAdd(string user, out string? error)
    {
        if (string.IsNullOrWhiteSpace(user))
        {
            error = "Whitelist user is invalid.";
            return false;
        }

        lock (_lock)
        {
            var comparer = CreateComparer(_settings.CaseSensitive);
            if (_users.Contains(user, comparer))
            {
                error = "User already exists in whitelist.";
                if (_filePath is not null)
                {
                    PluginLogger.Warn($"玩家 {user} 加入白名单失败，原因：{error}");
                }

                return false;
            }

            _users.Add(user);
        }

        error = null;
        if (_filePath is not null)
        {
            Save();
            PluginLogger.Info($"玩家 {user} 已加入白名单。");
        }

        return true;
    }

    public bool TryRemove(string user, out string? error)
    {
        if (string.IsNullOrWhiteSpace(user))
        {
            error = "Whitelist user is invalid.";
            return false;
        }

        lock (_lock)
        {
            var comparer = CreateComparer(_settings.CaseSensitive);
            var existing = _users.FirstOrDefault(item => comparer.Equals(item, user));
            if (existing is null)
            {
                error = "User not found in whitelist.";
                if (_filePath is not null)
                {
                    PluginLogger.Warn($"玩家 {user} 移出白名单失败，原因：{error}");
                }

                return false;
            }

            _users.Remove(existing);
        }

        error = null;
        if (_filePath is not null)
        {
            Save();
            PluginLogger.Info($"玩家 {user} 已从白名单移除。");
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

            var comparer = CreateComparer(_settings.CaseSensitive);
            if (_users.Contains(user, comparer))
            {
                return true;
            }

            denialReason = _settings.DenyMessage.Replace("{playerName}", user);
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

    private void WriteStore(WhitelistStore store)
    {
        if (_filePath is null)
        {
            return;
        }

        EnsureDirectory();
        File.WriteAllText(_filePath, JsonConvert.SerializeObject(store, JsonSettings));
    }

    private static IEqualityComparer<string> CreateComparer(bool caseSensitive)
        => caseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;
}
