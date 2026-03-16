using NextBotAdapter.Models;
using NextBotAdapter.Models.Responses;

namespace NextBotAdapter.Services;

public sealed class PersistedWhitelistService : IWhitelistService
{
    private readonly WhitelistConfigService _configService;
    private WhitelistService _inner;

    public PersistedWhitelistService(WhitelistConfigService configService)
    {
        _configService = configService;
        _inner = new WhitelistService(_configService.LoadSettings(), _configService.LoadWhitelist());
    }

    public WhitelistSettings Settings => _inner.Settings;

    public IReadOnlyList<string> GetAll() => _inner.GetAll();

    public bool IsWhitelisted(string user) => _inner.IsWhitelisted(user);

    public bool TryAdd(string user, out UserLookupError? error)
    {
        var added = _inner.TryAdd(user, out error);
        if (added)
        {
            Persist();
            PluginLogger.Info($"添加玩家 {user} 到白名单成功。");
        }
        else if (error is not null)
        {
            PluginLogger.Warn($"添加玩家 {user} 到白名单失败，原因：{error.Message}");
        }

        return added;
    }

    public bool TryRemove(string user, out UserLookupError? error)
    {
        var removed = _inner.TryRemove(user, out error);
        if (removed)
        {
            Persist();
            PluginLogger.Info($"将玩家 {user} 移出白名单成功。");
        }
        else if (error is not null)
        {
            PluginLogger.Warn($"将玩家 {user} 移出白名单失败，原因：{error.Message}");
        }

        return removed;
    }

    public bool TryValidateJoin(string user, out string? denialReason)
        => _inner.TryValidateJoin(user, out denialReason);

    public void Reload()
    {
        _inner = new WhitelistService(_configService.LoadSettings(), _configService.LoadWhitelist());
        PluginLogger.Info(
            $"重新加载白名单状态成功。当前启用状态为 {_inner.Settings.Enabled}，区分大小写为 {_inner.Settings.CaseSensitive}，当前共有 {_inner.GetAll().Count} 个条目。");
    }

    private void Persist()
    {
        _configService.SaveWhitelist(new WhitelistStore(_inner.GetAll()));
    }
}
