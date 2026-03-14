namespace NextBotAdapter.Models.Responses;

public sealed record InventoryItemResponse(int Slot, int NetId, int Stack, int PrefixId);
