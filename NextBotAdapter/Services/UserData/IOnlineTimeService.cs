namespace NextBotAdapter.Services;

public interface IOnlineTimeService
{
    void StartSession(string username);

    void EndSession(string username);

    long GetTotalSeconds(string username);

    IReadOnlyList<(string Username, long OnlineSeconds)> GetAllRecords();

    void PersistAllSessions();
}
