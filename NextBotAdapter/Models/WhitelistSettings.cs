using Newtonsoft.Json;

namespace NextBotAdapter.Models;

public sealed record WhitelistSettings(
    [property: JsonProperty("enabled")] bool Enabled,
    [property: JsonProperty("denyMessage")] string DenyMessage,
    [property: JsonProperty("caseSensitive")] bool CaseSensitive)
{
    public static WhitelistSettings Default { get; } = new(true, "你不在白名单中，请在 QQ 群发送「注册账号 {playerName}」后重新连接", true);
}
