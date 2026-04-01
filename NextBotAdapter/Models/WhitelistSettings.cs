using Newtonsoft.Json;

namespace NextBotAdapter.Models;

public sealed record WhitelistSettings(
    [property: JsonProperty("enabled")] bool Enabled,
    [property: JsonProperty("denyMessage")] string DenyMessage,
    [property: JsonProperty("caseSensitive")] bool CaseSensitive)
{
    public static WhitelistSettings Default { get; } = new(true, "You are not on the whitelist.", true);
}
