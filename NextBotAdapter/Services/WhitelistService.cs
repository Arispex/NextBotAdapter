using NextBotAdapter.Models;
using NextBotAdapter.Models.Responses;

namespace NextBotAdapter.Services;

public sealed class WhitelistService : IWhitelistService
{
    private readonly List<string> _users;
    public WhitelistSettings Settings { get; }

    public WhitelistService(WhitelistSettings settings, WhitelistStore store)
    {
        Settings = settings;
        _users = store.Users.ToList();
    }

    public IReadOnlyList<string> GetAll()
        => _users.ToArray();

    public bool IsWhitelisted(string user)
    {
        if (!Settings.Enabled)
        {
            return true;
        }

        var comparer = Settings.CaseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;
        return _users.Contains(user, comparer);
    }

    public bool TryAdd(string user, out UserLookupError? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(user))
        {
            error = new UserLookupError("Whitelist user is invalid.");
            return false;
        }

        var comparer = Settings.CaseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;
        if (_users.Contains(user, comparer))
        {
            error = new UserLookupError("User already exists in whitelist.");
            return false;
        }

        _users.Add(user);
        return true;
    }

    public bool TryRemove(string user, out UserLookupError? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(user))
        {
            error = new UserLookupError("Whitelist user is invalid.");
            return false;
        }

        var comparer = Settings.CaseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;
        var existing = _users.FirstOrDefault(item => comparer.Equals(item, user));
        if (existing is null)
        {
            error = new UserLookupError("User not found in whitelist.");
            return false;
        }

        _users.Remove(existing);
        return true;
    }

    public bool TryValidateJoin(string user, out string? denialReason)
    {
        denialReason = null;
        if (!Settings.Enabled)
        {
            return true;
        }

        if (IsWhitelisted(user))
        {
            return true;
        }

        denialReason = Settings.DenyMessage;
        return false;
    }
}
