namespace NextBotAdapter.Models.Responses;

public sealed record UserInfoResponse(
    int Health,
    int MaxHealth,
    int Mana,
    int MaxMana,
    int QuestsCompleted,
    int DeathsPve,
    int DeathsPvp);
