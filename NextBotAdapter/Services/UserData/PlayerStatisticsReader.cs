using System.Collections.Concurrent;
using System.Reflection;

namespace NextBotAdapter.Services;

public static class PlayerStatisticsReader
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static readonly ConcurrentDictionary<(Type Type, string Name), PropertyInfo?> PropertyCache = new();
    private static readonly ConcurrentDictionary<(Type Type, string Name), FieldInfo?> FieldCache = new();

    public static int ReadDeaths(object? source, string fieldName)
    {
        if (source is null || string.IsNullOrWhiteSpace(fieldName))
        {
            return 0;
        }

        var type = source.GetType();
        var key = (type, fieldName);

        var property = PropertyCache.GetOrAdd(key, static k => k.Type.GetProperty(k.Name, Flags));
        if (property?.PropertyType == typeof(int) && property.GetValue(source) is int propertyValue)
        {
            return propertyValue;
        }

        var field = FieldCache.GetOrAdd(key, static k => k.Type.GetField(k.Name, Flags));
        if (field?.FieldType == typeof(int) && field.GetValue(source) is int fieldValue)
        {
            return fieldValue;
        }

        return 0;
    }
}
