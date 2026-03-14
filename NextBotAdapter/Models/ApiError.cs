using System.Text.Json.Serialization;

namespace NextBotAdapter.Models;

public sealed record ApiError(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("message")] string Message);
