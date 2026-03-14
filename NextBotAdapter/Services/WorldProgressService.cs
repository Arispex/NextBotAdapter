using NextBotAdapter.Models.Responses;

namespace NextBotAdapter.Services;

public static class WorldProgressService
{
    public static IWorldProgressSource DefaultSource { get; } = new WorldProgressSourceAdapter();

    public static WorldProgressResponse GetProgress()
        => GetProgress(DefaultSource);

    public static WorldProgressResponse GetProgress(IWorldProgressSource source)
    {
        var snapshot = source.GetSnapshot();
        return WorldProgressMapper.CreateResponse(snapshot);
    }
}
