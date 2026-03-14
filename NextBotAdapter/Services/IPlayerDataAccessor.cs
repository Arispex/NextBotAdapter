namespace NextBotAdapter.Services;

public interface IPlayerDataAccessor
{
    bool TryGetPlayerData(string user, out object data, out NextBotAdapter.Models.UserLookupError? error);
}
