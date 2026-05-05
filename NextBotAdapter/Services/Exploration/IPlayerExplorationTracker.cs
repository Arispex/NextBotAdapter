using System.Collections;

namespace NextBotAdapter.Services;

public interface IPlayerExplorationTracker
{
    void MarkArea(string accountUuid, int tileX, int tileY);

    BitArray? GetBitmap(string accountUuid);

    void Load(string accountUuid);

    void Save(string accountUuid);

    void SaveAll();
}
