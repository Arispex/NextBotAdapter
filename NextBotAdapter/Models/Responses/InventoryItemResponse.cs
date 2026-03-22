using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace NextBotAdapter.Models.Responses;

public sealed record InventoryItemResponse(
    [property: JsonProperty("slot"), JsonPropertyName("slot")] int Slot,
    [property: JsonProperty("netId"), JsonPropertyName("netId")] int NetId,
    [property: JsonProperty("stack"), JsonPropertyName("stack")] int Stack,
    [property: JsonProperty("prefixId"), JsonPropertyName("prefixId")] int PrefixId);
