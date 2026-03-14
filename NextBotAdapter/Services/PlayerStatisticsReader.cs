using System.Reflection;

namespace NextBotAdapter.Services;

public static class PlayerStatisticsReader
{
    public static int ReadDeaths(object? source, string fieldName)
    {
        if (source is null || string.IsNullOrWhiteSpace(fieldName))
        {
            return 0;
        }

        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        var property = source.GetType().GetProperty(fieldName, flags);
        if (property?.PropertyType == typeof(int) && property.GetValue(source) is int propertyValue)
        {
            return propertyValue;
        }

        var field = source.GetType().GetField(fieldName, flags);
        if (field?.FieldType == typeof(int) && field.GetValue(source) is int fieldValue)
        {
            return fieldValue;
        }

        return 0;
    }
}
