using Newtonsoft.Json;

namespace NextBotAdapter.Models.Responses;

public sealed record FishingQuestsLeaderboardEntryResponse(
    [property: JsonProperty("username")] string Username,
    [property: JsonProperty("questsCompleted")] int QuestsCompleted);
