using Newtonsoft.Json;

namespace NextBotAdapter.Models;

public sealed record PlayerEventsSettings(
    [property: JsonProperty("enabled")] bool Enabled,
    [property: JsonProperty("online")] bool Online,
    [property: JsonProperty("offline")] bool Offline)
{
    public static PlayerEventsSettings Default { get; } = new(false, false, false);
}
