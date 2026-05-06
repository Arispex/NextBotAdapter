using Newtonsoft.Json;

namespace NextBotAdapter.Models.Responses;

public sealed record MapExplorationLeaderboardEntryResponse(
    [property: JsonProperty("username")] string Username,
    [property: JsonProperty("mapExplorationPercent")] double MapExplorationPercent);
