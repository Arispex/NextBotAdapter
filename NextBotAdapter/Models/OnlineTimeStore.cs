using Newtonsoft.Json;

namespace NextBotAdapter.Models;

public sealed record OnlineTimeStore(
    [property: JsonProperty("records")] IReadOnlyDictionary<string, long> Records)
{
    public static OnlineTimeStore Empty { get; } = new(new Dictionary<string, long>());
}
