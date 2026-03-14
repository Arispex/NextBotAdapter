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
        }
        return added;
    }

    public bool TryRemove(string user, out UserLookupError? error)
    {
        var removed = _inner.TryRemove(user, out error);
        if (removed)
        {
            Persist();
        }
        return removed;
    }

    public bool TryValidateJoin(string user, out string? denialReason)
        => _inner.TryValidateJoin(user, out denialReason);

    public void Reload()
    {
        _inner = new WhitelistService(_configService.LoadSettings(), _configService.LoadWhitelist());
    }

    private void Persist()
    {
        _configService.SaveWhitelist(new WhitelistStore(_inner.GetAll()));
    }
}
