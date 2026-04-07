using System.Diagnostics.CodeAnalysis;
using TShockAPI;

namespace NextBotAdapter.Services;

public sealed class UserDataService : IPlayerDataAccessor
{
    public static IUserDataGateway DefaultGateway { get; } = new TShockUserDataGateway();

    public static IPlayerDataAccessor Default { get; } = new UserDataService(DefaultGateway);

    private readonly IUserDataGateway _gateway;

    public UserDataService(IUserDataGateway gateway)
    {
        _gateway = gateway;
    }

    public bool TryGetPlayerData(string user, out object data, out string? error)
    {
        data = null!;
        error = null;

        if (string.IsNullOrWhiteSpace(user))
        {
            error = "User cannot be empty.";
            return false;
        }

        if (!_gateway.TryGetUserAccountId(user, out var accountId))
        {
            error = "User was not found.";
            return false;
        }

        if (!_gateway.TryGetPlayerData(accountId, out data))
        {
            error = "Player data was not found.";
            return false;
        }

        return true;
    }

    [ExcludeFromCodeCoverage]
    private sealed class TShockUserDataGateway : IUserDataGateway
    {
        public bool TryGetUserAccountId(string user, out int accountId)
        {
            var account = TShock.UserAccounts.GetUserAccountByName(user);
            if (account is null)
            {
                accountId = default;
                return false;
            }

            accountId = account.ID;
            return true;
        }

        public bool TryGetPlayerData(int accountId, out object playerData)
        {
            var data = TShock.CharacterDB.GetPlayerData(null, accountId);
            if (data is null)
            {
                playerData = null!;
                return false;
            }

            playerData = data;
            return true;
        }

        public IReadOnlyList<(int AccountId, string Username)> GetAllUserAccounts()
        {
            var accounts = TShock.UserAccounts.GetUserAccounts();
            if (accounts is null)
            {
                return [];
            }

            return accounts.Select(a => (a.ID, a.Name)).ToList();
        }
    }
}
