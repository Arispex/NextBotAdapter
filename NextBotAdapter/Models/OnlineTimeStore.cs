using System.Text.Json.Serialization;

namespace NextBotAdapter.Models;

public sealed record OnlineTimeStore(
    [property: JsonPropertyName("records")] IReadOnlyDictionary<string, long> Records)
{
    public static OnlineTimeStore Empty { get; } = new(new Dictionary<string, long>());
}
