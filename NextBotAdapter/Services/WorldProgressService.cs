using NextBotAdapter.Models.Responses;

namespace NextBotAdapter.Services;

public static class WorldProgressService
{
    public static IWorldProgressSource DefaultSource { get; } = new WorldProgressSourceAdapter();

    public static WorldProgressSnapshot GetProgress()
        => GetProgress(DefaultSource);

    public static WorldProgressSnapshot GetProgress(IWorldProgressSource source)
        => source.GetSnapshot();
}
