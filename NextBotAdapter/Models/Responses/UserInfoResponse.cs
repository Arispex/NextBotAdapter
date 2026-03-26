using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace NextBotAdapter.Models.Responses;

public sealed record UserInfoResponse(
    [property: JsonProperty("health"), JsonPropertyName("health")] int Health,
    [property: JsonProperty("maxHealth"), JsonPropertyName("maxHealth")] int MaxHealth,
    [property: JsonProperty("mana"), JsonPropertyName("mana")] int Mana,
    [property: JsonProperty("maxMana"), JsonPropertyName("maxMana")] int MaxMana,
    [property: JsonProperty("questsCompleted"), JsonPropertyName("questsCompleted")] int QuestsCompleted,
    [property: JsonProperty("deathsPve"), JsonPropertyName("deathsPve")] int DeathsPve,
    [property: JsonProperty("deathsPvp"), JsonPropertyName("deathsPvp")] int DeathsPvp,
    [property: JsonProperty("onlineSeconds"), JsonPropertyName("onlineSeconds")] long OnlineSeconds = 0);
