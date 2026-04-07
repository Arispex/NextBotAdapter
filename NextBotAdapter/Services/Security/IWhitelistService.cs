using NextBotAdapter.Models;

namespace NextBotAdapter.Services;

public interface IWhitelistService
{
    WhitelistSettings Settings { get; }

    IReadOnlyList<string> GetAll();

    bool IsWhitelisted(string user);

    bool TryAdd(string user, out string? error);

    bool TryRemove(string user, out string? error);

    bool TryValidateJoin(string user, out string? denialReason);
}
