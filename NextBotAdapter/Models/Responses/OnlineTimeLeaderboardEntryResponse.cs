using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace NextBotAdapter.Models.Responses;

public sealed record OnlineTimeLeaderboardEntryResponse(
    [property: JsonProperty("username"), JsonPropertyName("username")] string Username,
    [property: JsonProperty("onlineSeconds"), JsonPropertyName("onlineSeconds")] long OnlineSeconds);
