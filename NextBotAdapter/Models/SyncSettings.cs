using Newtonsoft.Json;

namespace NextBotAdapter.Models;

public sealed record SyncSettings(
    [property: JsonProperty("whitelist")] bool Whitelist,
    [property: JsonProperty("blacklist")] bool Blacklist)
{
    public static SyncSettings Default { get; } = new(true, true);
}
