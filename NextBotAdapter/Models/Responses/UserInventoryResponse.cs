using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace NextBotAdapter.Models.Responses;

public sealed record UserInventoryResponse(
    [property: JsonProperty("items"), JsonPropertyName("items")] IReadOnlyList<InventoryItemResponse> Items);
