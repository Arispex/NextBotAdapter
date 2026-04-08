using Newtonsoft.Json;

namespace NextBotAdapter.Models;

public sealed record NextBotSettings(
    [property: JsonProperty("baseUrl")] string BaseUrl,
    [property: JsonProperty("token")] string Token)
{
    public static NextBotSettings Default { get; } = new(string.Empty, string.Empty);
}
