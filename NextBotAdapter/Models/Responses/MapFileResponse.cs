using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace NextBotAdapter.Models.Responses;

public sealed record MapFileResponse(
    [property: JsonProperty("fileName"), JsonPropertyName("fileName")] string FileName,
    [property: JsonProperty("base64"), JsonPropertyName("base64")] string Base64);
