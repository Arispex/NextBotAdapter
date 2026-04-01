using Newtonsoft.Json;

namespace NextBotAdapter.Models;

public sealed record WhitelistStore(
    [property: JsonProperty("users")] IReadOnlyList<string> Users)
{
    public static WhitelistStore Empty { get; } = new([]);
}
