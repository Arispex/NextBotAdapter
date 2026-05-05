using System.Diagnostics.CodeAnalysis;
using TShockAPI;

namespace NextBotAdapter.Services;

[ExcludeFromCodeCoverage]
public sealed class TShockUserAccountLookup : IUserAccountLookup
{
    public static readonly IUserAccountLookup Default = new TShockUserAccountLookup();

    public bool TryGetAccountUuid(string user, out string accountUuid)
    {
        accountUuid = string.Empty;
        if (string.IsNullOrWhiteSpace(user))
        {
            return false;
        }

        var account = TShock.UserAccounts.GetUserAccountByName(user);
        if (account is null)
        {
            return false;
        }

        accountUuid = account.UUID ?? string.Empty;
        return true;
    }
}
