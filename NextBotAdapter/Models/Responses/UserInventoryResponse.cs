namespace NextBotAdapter.Models.Responses;

public sealed record UserInventoryResponse(IReadOnlyList<InventoryItemResponse> Items);
