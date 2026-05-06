using System.Collections;

namespace NextBotAdapter.Services;

public interface IExplorationStorage
{
    ExplorationLoadResult Load(string accountName, int expectedBitCount);

    bool Save(string accountName, BitArray bitmap);
}
