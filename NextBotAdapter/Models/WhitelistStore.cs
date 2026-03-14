using System.Text.Json.Serialization;

namespace NextBotAdapter.Models;

public sealed record WhitelistStore(
    [property: JsonPropertyName("users")] IReadOnlyList<string> Users)
{
    public static WhitelistStore Empty { get; } = new([]);
}
