using Newtonsoft.Json;

namespace NextBotAdapter.Models.Responses;

public sealed record InventoryItemResponse(
    [property: JsonProperty("slot")] int Slot,
    [property: JsonProperty("netId")] int NetId,
    [property: JsonProperty("stack")] int Stack,
    [property: JsonProperty("prefixId")] int PrefixId);
