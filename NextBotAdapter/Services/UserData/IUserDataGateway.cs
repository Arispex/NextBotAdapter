namespace NextBotAdapter.Services;

public interface IUserDataGateway
{
    bool TryGetUserAccountId(string user, out int accountId);

    bool TryGetPlayerData(int accountId, out object playerData);

    IReadOnlyList<(int AccountId, string Username)> GetAllUserAccounts();
}
