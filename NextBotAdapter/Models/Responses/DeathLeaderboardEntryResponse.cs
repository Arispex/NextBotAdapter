using Newtonsoft.Json;

namespace NextBotAdapter.Models.Responses;

public sealed record DeathLeaderboardEntryResponse(
    [property: JsonProperty("username")] string Username,
    [property: JsonProperty("deaths")] int Deaths);
