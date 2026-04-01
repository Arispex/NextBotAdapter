using Newtonsoft.Json;

namespace NextBotAdapter.Models.Responses;

public sealed record OnlineTimeLeaderboardEntryResponse(
    [property: JsonProperty("username")] string Username,
    [property: JsonProperty("onlineSeconds")] long OnlineSeconds);
