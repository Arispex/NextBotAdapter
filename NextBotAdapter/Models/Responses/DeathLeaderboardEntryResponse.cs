using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace NextBotAdapter.Models.Responses;

public sealed record DeathLeaderboardEntryResponse(
    [property: JsonProperty("username"), JsonPropertyName("username")] string Username,
    [property: JsonProperty("deaths"), JsonPropertyName("deaths")] int Deaths);
