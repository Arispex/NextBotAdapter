using System.Text.Json.Serialization;

namespace NextBotAdapter.Models.Responses;

public sealed record WhitelistListResponse(
    [property: JsonPropertyName("users")] IReadOnlyList<string> Users);
