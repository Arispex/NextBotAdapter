using Newtonsoft.Json;

namespace NextBotAdapter.Models;

public sealed record BlacklistEntry(
    [property: JsonProperty("username")] string Username,
    [property: JsonProperty("reason")] string Reason);

public sealed record BlacklistStore(
    [property: JsonProperty("entries")] IReadOnlyList<BlacklistEntry> Entries)
{
    public static BlacklistStore Empty { get; } = new([]);
}
