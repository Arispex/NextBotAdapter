using NextBotAdapter.Models.Responses;
using NextBotAdapter.Services;
using Rests;

namespace NextBotAdapter.Rest;

public static class WorldEndpoints
{
    public static object Progress(RestRequestArgs _)
        => Progress(WorldProgressService.DefaultSource);

    public static RestObject Progress(IWorldProgressSource source)
    {
        var response = WorldProgressService.GetProgress(source);
        return Infrastructure.EndpointResponseFactory.Success(response);
    }
}
