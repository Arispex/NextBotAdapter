using NextBotAdapter.Models.Responses;

namespace NextBotAdapter.Services;

public static class UserInventoryService
{
    public static bool TryGetInventory(string user, out UserInventoryResponse inventory, out NextBotAdapter.Models.UserLookupError? error)
        => TryGetInventory(user, UserDataService.Default, out inventory, out error);

    public static bool TryGetInventory(string user, IPlayerDataAccessor accessor, out UserInventoryResponse inventory, out NextBotAdapter.Models.UserLookupError? error)
    {
        inventory = new UserInventoryResponse(Array.Empty<InventoryItemResponse>());
        error = null;

        if (!accessor.TryGetPlayerData(user, out var data, out error))
        {
            return false;
        }

        inventory = PlayerInventoryMapper.CreateResponse(data);
        return true;
    }
}
