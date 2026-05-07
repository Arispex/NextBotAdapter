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

    /// <summary>
    /// Merge this account's bitmap into <paramref name="target"/> via in-place
    /// <see cref="BitArray.Or(BitArray)"/> without allocating a snapshot copy.
    /// Returns true when bitmap data was found (in-memory or via lazy-load) and
    /// merged into <paramref name="target"/>; false on confirmed missing data.
    /// Lengths must match; mismatched bitmaps are treated as found but skipped.
    /// </summary>
    bool TryOrInto(string accountName, BitArray target);
}
