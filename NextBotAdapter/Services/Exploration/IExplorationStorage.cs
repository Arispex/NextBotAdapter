using System.Collections;

namespace NextBotAdapter.Services;

public interface IExplorationStorage
{
    BitArray? Load(string accountName, int expectedBitCount);

    void Save(string accountName, BitArray bitmap);
}
