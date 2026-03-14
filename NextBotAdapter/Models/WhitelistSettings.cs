using System.Text.Json.Serialization;

namespace NextBotAdapter.Models;

public sealed record WhitelistSettings(
    [property: JsonPropertyName("enabled")] bool Enabled,
    [property: JsonPropertyName("denyMessage")] string DenyMessage,
    [property: JsonPropertyName("caseSensitive")] bool CaseSensitive)
{
    public static WhitelistSettings Default { get; } = new(true, "You are not on the whitelist.", true);
}
