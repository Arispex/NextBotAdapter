using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using NextBotAdapter.Models.Responses;
using TShockAPI;

namespace NextBotAdapter.Services;

public static class PlayerInventoryMapper
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private const string InventoryMemberName = "inventory";
    private static readonly ConcurrentDictionary<Type, PropertyInfo?> InventoryPropertyCache = new();
    private static readonly ConcurrentDictionary<Type, FieldInfo?> InventoryFieldCache = new();

    public static UserInventoryResponse CreateResponse(object playerData)
    {
        var inventory = ReadInventory(playerData);
        var items = inventory
            .Select((item, index) => MapItem(item, index))
            .ToArray();

        return new UserInventoryResponse(items);
    }

    private static IEnumerable<object> ReadInventory(object playerData)
    {
        var type = playerData.GetType();

        var property = InventoryPropertyCache.GetOrAdd(type, static t => t.GetProperty(InventoryMemberName, Flags));
        if (property?.GetValue(playerData) is IEnumerable propertyItems)
        {
            return propertyItems.Cast<object>();
        }

        var field = InventoryFieldCache.GetOrAdd(type, static t => t.GetField(InventoryMemberName, Flags));
        if (field?.GetValue(playerData) is IEnumerable fieldItems)
        {
            return fieldItems.Cast<object>();
        }

        return Array.Empty<object>();
    }

    private static InventoryItemResponse MapItem(object item, int index)
    {
        if (item is InventoryItemResponse inventoryItem)
        {
            return new InventoryItemResponse(index, inventoryItem.NetId, inventoryItem.Stack, inventoryItem.PrefixId);
        }

        if (item is NetItem netItem)
        {
            return new InventoryItemResponse(index, netItem.NetId, netItem.Stack, netItem.PrefixId);
        }

        return new InventoryItemResponse(
            index,
            PlayerStatisticsReader.ReadDeaths(item, "NetId"),
            PlayerStatisticsReader.ReadDeaths(item, "Stack"),
            PlayerStatisticsReader.ReadDeaths(item, "PrefixId"));
    }
}
