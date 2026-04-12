using NextBotAdapter.Models;

namespace NextBotAdapter.Services;

public interface IBlacklistService
{
    BlacklistSettings Settings { get; }

    IReadOnlyList<BlacklistEntry> GetAll();

    bool IsBlacklisted(string user);

    bool TryAdd(string user, string reason, out string? error);

    bool TryRemove(string user, out string? error);

    bool TryValidateJoin(string user, out string? denialReason);
}
