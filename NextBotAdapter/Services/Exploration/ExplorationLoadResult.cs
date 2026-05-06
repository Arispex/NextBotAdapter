using System.Collections;

namespace NextBotAdapter.Services;

public sealed record ExplorationLoadResult(BitArray? Bitmap, bool FileMissing);
