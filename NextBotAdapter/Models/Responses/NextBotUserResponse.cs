using Newtonsoft.Json;

namespace NextBotAdapter.Models.Responses;

public sealed record NextBotUserEntry(
    [property: JsonProperty("name")] string Name,
    [property: JsonProperty("is_banned")] bool IsBanned,
    [property: JsonProperty("ban_reason")] string BanReason);

public sealed record NextBotUsersResponse(
    [property: JsonProperty("data")] IReadOnlyList<NextBotUserEntry> Data);
