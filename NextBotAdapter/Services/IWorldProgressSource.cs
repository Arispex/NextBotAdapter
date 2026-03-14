using NextBotAdapter.Models.Responses;

namespace NextBotAdapter.Services;

public interface IWorldProgressSource
{
    WorldProgressSnapshot GetSnapshot();
}
