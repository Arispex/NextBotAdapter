using NextBotAdapter.Models;
using NextBotAdapter.Models.Responses;

namespace NextBotAdapter.Services;

public sealed class PersistedWhitelistService : IWhitelistService
{
    private readonly PluginConfigService _configService;
    private readonly WhitelistFileService _fileService;
    private WhitelistService _inner;

    public PersistedWhitelistService(PluginConfigService configService, WhitelistFileService fileService)
    {
        _configService = configService;
        _fileService = fileService;
        _inner = new WhitelistService(_configService.LoadWhitelistSettings(), _fileService.LoadWhitelist());
    }

    public WhitelistSettings Settings => _inner.Settings;

    public IReadOnlyList<string> GetAll() => _inner.GetAll();

    public bool IsWhitelisted(string user) => _inner.IsWhitelisted(user);

    public bool TryAdd(string user, out string? error)
    {
        var added = _inner.TryAdd(user, out error);
        if (added)
        {
            Persist();
            PluginLogger.Info($"玩家 {user} 已加入白名单。");
        }
        else if (error is not null)
        {
            PluginLogger.Warn($"玩家 {user} 加入白名单失败，原因：{error}");
        }

        return added;
    }

    public bool TryRemove(string user, out string? error)
    {
        var removed = _inner.TryRemove(user, out error);
        if (removed)
        {
            Persist();
            PluginLogger.Info($"玩家 {user} 已从白名单移除。");
        }
        else if (error is not null)
        {
            PluginLogger.Warn($"玩家 {user} 移出白名单失败，原因：{error}");
        }

        return removed;
    }

    public bool TryValidateJoin(string user, out string? denialReason)
        => _inner.TryValidateJoin(user, out denialReason);

    public void Reload()
    {
        _inner = new WhitelistService(_configService.LoadWhitelistSettings(), _fileService.LoadWhitelist());
    }

    private void Persist()
    {
        _fileService.SaveWhitelist(new WhitelistStore(_inner.GetAll()));
    }
}
