namespace NextBotAdapter.Services;

public interface IUserAccountLookup
{
    /// <summary>
    /// Resolve a TShock user account by name. Returns the account UUID when found.
    /// </summary>
    bool TryGetAccountUuid(string user, out string accountUuid);
}
