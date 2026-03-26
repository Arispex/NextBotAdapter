using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace NextBotAdapter.Models.Responses;

public sealed record FishingQuestsLeaderboardEntryResponse(
    [property: JsonProperty("username"), JsonPropertyName("username")] string Username,
    [property: JsonProperty("questsCompleted"), JsonPropertyName("questsCompleted")] int QuestsCompleted);
