using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace NextBotAdapter.Models.Responses;

public sealed record WhitelistListResponse(
    [property: JsonProperty("users"), JsonPropertyName("users")] IReadOnlyList<string> Users);
