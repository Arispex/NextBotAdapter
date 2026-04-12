using Newtonsoft.Json;

namespace NextBotAdapter.Models;

public sealed record BlacklistSettings(
    [property: JsonProperty("enabled")] bool Enabled,
    [property: JsonProperty("denyMessage")] string DenyMessage)
{
    public static BlacklistSettings Default { get; } = new(true, "你已被封禁，原因：{reason}。如有疑问，请联系管理员。");
}
