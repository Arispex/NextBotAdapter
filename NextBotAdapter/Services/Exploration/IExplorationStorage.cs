using System.Collections;

namespace NextBotAdapter.Services;

public interface IExplorationStorage
{
    BitArray? Load(string accountUuid, int expectedBitCount);

    void Save(string accountUuid, BitArray bitmap);
}
