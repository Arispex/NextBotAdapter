namespace NextBotAdapter.Services;

public interface IUserAccountLookup
{
    /// <summary>
    /// Resolve a TShock user account by name. Returns the canonical account name when found.
    /// </summary>
    bool TryGetAccountName(string user, out string accountName);
}
