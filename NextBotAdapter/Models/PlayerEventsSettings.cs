using Newtonsoft.Json;

namespace NextBotAdapter.Models;

public sealed record PlayerEventsSettings(
    [property: JsonProperty("enabled")] bool Enabled,
    [property: JsonProperty("online")] bool Online,
    [property: JsonProperty("offline")] bool Offline,
    [property: JsonProperty("message")] bool Message)
{
    public static PlayerEventsSettings Default { get; } = new(false, false, false, false);
}
