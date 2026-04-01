using Newtonsoft.Json;

namespace NextBotAdapter.Models.Responses;

public sealed record UserInfoResponse(
    [property: JsonProperty("health")] int Health,
    [property: JsonProperty("maxHealth")] int MaxHealth,
    [property: JsonProperty("mana")] int Mana,
    [property: JsonProperty("maxMana")] int MaxMana,
    [property: JsonProperty("questsCompleted")] int QuestsCompleted,
    [property: JsonProperty("deathsPve")] int DeathsPve,
    [property: JsonProperty("deathsPvp")] int DeathsPvp,
    [property: JsonProperty("onlineSeconds")] long OnlineSeconds = 0);
