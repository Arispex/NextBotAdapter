using System.Text.Json.Serialization;

namespace NextBotAdapter.Models;

public sealed record LoginConfirmationSettings(
    [property: JsonPropertyName("enabled")] bool Enabled,
    [property: JsonPropertyName("detectUuid")] bool DetectUuid,
    [property: JsonPropertyName("detectIp")] bool DetectIp,
    [property: JsonPropertyName("emptyUuidMessage")] string EmptyUuidMessage = "无法获取你的 UUID，请联系管理员。",
    [property: JsonPropertyName("changeDetectedMessage")] string ChangeDetectedMessage = "你的 {changed} 发生变化，请在 QQ 群发送「登入」后重新连接。",
    [property: JsonPropertyName("deviceMismatchMessage")] string DeviceMismatchMessage = "该账号已通过登入确认，但当前设备与确认时不一致，请使用原设备登入。",
    [property: JsonPropertyName("pendingExistsMessage")] string PendingExistsMessage = "该账号已有待确认的登入请求，请等待其过期后再试。")
{
    public static LoginConfirmationSettings Default { get; } = new(true, true, true);
}
