using System.Collections;

namespace NextBotAdapter.Services;

public interface IPlayerExplorationTracker
{
    void MarkArea(string accountName, int tileX, int tileY);

    void MarkAtPosition(string accountName, int tileX, int tileY);

    void ForgetLastSample(string accountName);

    BitArray? GetBitmap(string accountName);

    double GetExplorationPercent(string accountName);

    void Load(string accountName);

    void Save(string accountName);

    void SaveAll();
}
