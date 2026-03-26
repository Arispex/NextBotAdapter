using NextBotAdapter.Models;

namespace NextBotAdapter.Services;

public interface IOnlineTimeFileService
{
    OnlineTimeStore Load();

    void Save(OnlineTimeStore store);
}
