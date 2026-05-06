using System.Diagnostics.CodeAnalysis;
using TShockAPI;

namespace NextBotAdapter.Services;

[ExcludeFromCodeCoverage]
public sealed class TShockUserAccountLookup : IUserAccountLookup
{
    public static readonly IUserAccountLookup Default = new TShockUserAccountLookup();

    public bool TryGetAccountName(string user, out string accountName)
    {
        accountName = string.Empty;
        if (string.IsNullOrWhiteSpace(user))
        {
            return false;
        }

        var account = TShock.UserAccounts.GetUserAccountByName(user);
        if (account is null)
        {
            return false;
        }

        accountName = account.Name ?? string.Empty;
        return true;
    }
}
