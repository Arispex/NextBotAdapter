using Newtonsoft.Json;

namespace NextBotAdapter.Models.Responses;

public sealed record UserInventoryResponse(
    [property: JsonProperty("items")] IReadOnlyList<InventoryItemResponse> Items);
