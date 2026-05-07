using NextBotAdapter.Services;

namespace NextBotAdapter.Plugin;

/// <summary>
/// Second-pass blacklist / whitelist re-check that runs after TShock login,
/// keyed on the resolved <see cref="TShockAPI.DB.UserAccount.Name"/> rather
/// than the player display name. This closes the bypass where a player joins
/// with a display name that is not on either list, then runs
/// <c>/login &lt;banned account&gt; &lt;password&gt;</c> to authenticate as a
/// listed account that the <c>OnPlayerInfo</c> hook already let through.
/// </summary>
internal static class PostLoginAccountGuard
{
    /// <summary>
    /// Returns <c>(true, null)</c> when the account is allowed to proceed.
    /// Returns <c>(false, denialReason)</c> when the caller should
    /// <c>Disconnect(denialReason)</c>. Blacklist is checked before whitelist
    /// so that a banned account always sees the ban message, even when the
    /// account also happens to satisfy the whitelist.
    /// </summary>
    public static (bool Allowed, string? DenialReason) Validate(
        string accountName,
        IBlacklistService? blacklist,
        IWhitelistService? whitelist)
    {
        if (string.IsNullOrEmpty(accountName))
        {
            return (true, null);
        }

        if (blacklist is not null && !blacklist.TryValidateJoin(accountName, out var blacklistReason))
        {
            return (false, blacklistReason ?? "账号已被加入黑名单");
        }

        if (whitelist is not null && !whitelist.TryValidateJoin(accountName, out var whitelistReason))
        {
            return (false, whitelistReason ?? "你不在白名单中");
        }

        return (true, null);
    }
}
