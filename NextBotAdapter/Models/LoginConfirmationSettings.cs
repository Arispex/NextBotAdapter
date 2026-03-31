using System.Text.Json.Serialization;

namespace NextBotAdapter.Models;

public sealed record LoginConfirmationSettings(
    [property: JsonPropertyName("enabled")] bool Enabled,
    [property: JsonPropertyName("detectUuid")] bool DetectUuid,
    [property: JsonPropertyName("detectIp")] bool DetectIp)
{
    public static LoginConfirmationSettings Default { get; } = new(true, true, true);
}
