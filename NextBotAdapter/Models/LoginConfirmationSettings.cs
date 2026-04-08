using Newtonsoft.Json;

namespace NextBotAdapter.Models;

public sealed record LoginConfirmationSettings(
    [property: JsonProperty("enabled")] bool Enabled,
    [property: JsonProperty("detectUuid")] bool DetectUuid,
    [property: JsonProperty("detectIp")] bool DetectIp,
    [property: JsonProperty("autoLogin")] bool AutoLogin = false,
    [property: JsonProperty("emptyUuidMessage")] string EmptyUuidMessage = "无法获取你的 UUID，请联系管理员。",
    [property: JsonProperty("changeDetectedMessage")] string ChangeDetectedMessage = "你的 {changed} 发生变化，请在 QQ 群发送「允许登入」后重新连接。",
    [property: JsonProperty("deviceMismatchMessage")] string DeviceMismatchMessage = "该账号已通过登入确认，但当前设备与确认时不一致，请使用原设备登入。",
    [property: JsonProperty("pendingExistsMessage")] string PendingExistsMessage = "该账号已有待确认的登入请求，请等待其过期后再试。")
{
    public static LoginConfirmationSettings Default { get; } = new(true, true, true);
}
