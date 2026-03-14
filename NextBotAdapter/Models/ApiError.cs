using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace NextBotAdapter.Models;

public sealed record ApiError(
    [property: JsonProperty("code"), JsonPropertyName("code")] string Code,
    [property: JsonProperty("message"), JsonPropertyName("message")] string Message);
