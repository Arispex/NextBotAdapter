# handle 429 rate limit in NextBot session probe

## Goal

服务端 `POST /webui/api/session` 新增 HTTP 429 速率限制（per-IP，登录连失败 5 次左右触发，响应带
`Retry-After`）。当前 `NextBotSessionProbeService.ProbeAsync` 把 429 吞进默认
`_ → NextBotProbeStatus.Unreachable` 分支，造成"被限流"被识别成"网络不可达"，日志误导排障，且未来若任何
调用方加重试逻辑会无视 `Retry-After` 越撞越久。本任务把 429 单独识别出来 + 暴露 `Retry-After`，并顺手补两
个文档建议的请求 header（`X-Requested-With` / `Accept`）。

## What I already know

- 出问题的代码：`NextBotAdapter/Services/NextBot/NextBotSessionProbeService.cs:72-135`（`ProbeAsync`）
- 现有 status 枚举：`Skipped` / `Ok` / `Unauthorized` / `InvalidToken` / `Unreachable`
- 现有 result 形状：`NextBotProbeResult(Status, HttpStatus, Message)`
- **ProbeAsync 调用方共 2 处，均无重试循环**：
  1. `Plugin/NextBotAdapterPlugin.cs:670-700` — `VerifyNextBotConnectionAsync`，启动期一次性探活。switch
     `result.Status`：`Ok` → Info + 同步，`Skipped` → Info，`default` → Warn。无 retry。
  2. `Rest/ConfigEndpoints.cs:96-125` — REST `/v1/config/verify-nextbot` endpoint。`result.Status.ToString()`
     作为 `probeStatus` 字段返回，`HttpStatus` 作为 `httpStatus` 返回，由调用方（运维）决定后续动作。无 retry。
- 现有测试位置：`NextBotAdapter.Tests/NextBotSessionProbeServiceTests.cs`，`FakeHandler(Func<HttpRequestMessage, HttpResponseMessage>)`
  模式，每个 status 单独 `[Fact]`。
- 现有用例覆盖：`Skipped` (BaseUrl/Token 空)、`Ok` (201)、`Unauthorized` (401)、`InvalidToken` (422)、
  `Unreachable` (5xx / `HttpRequestException` / 非法 URL)。
- 现有 `DefaultClient` 是 `new HttpClient() { Timeout = 5s }`，无 CookieContainer。后续业务请求走 query token，不依赖 cookie。

## Requirements

1. `NextBotProbeStatus` 枚举新增 `RateLimited`（排在 `Unreachable` 之前，保持现有顺序）。
2. `NextBotProbeResult` 加 `TimeSpan? RetryAfter` 字段，仅 `Status == RateLimited` 时非 null。其他 status 路径
   传 `null`。
3. `ProbeAsync` 在 status 分支表加 `HttpStatusCode.TooManyRequests`：
   - 解析 `response.Headers.RetryAfter`：优先 `Delta`（delta-seconds），其次 `Date - DateTimeOffset.UtcNow`
   - 解析失败 / 没带 header → 兜底 60 秒
   - 返回 `new NextBotProbeResult(RateLimited, 429, $"上游限流（HTTP 429），建议 {seconds} 秒后重试", TimeSpan.FromSeconds(...))`
4. 调用方更新：
   - `VerifyNextBotConnectionAsync`（`Plugin/NextBotAdapterPlugin.cs`）：在 switch 加 `case NextBotProbeStatus.RateLimited`
     分支，WARN 日志，把 `RetryAfter` 秒数写进消息（自然语言风格，符合 `logging-guidelines.md` human-readable）。
   - `VerifyNextBot`（`Rest/ConfigEndpoints.cs`）：若 `RetryAfter` 非 null，在返回的 RestObject 加 `retryAfterSeconds`
     字段（int 秒）。其他字段（probeStatus / message / baseUrl / httpStatus）保持现状。
5. 请求 header 增强（顺手做）：
   - `Accept: application/json`
   - `X-Requested-With: NextBotWebUI`
   - 加在 ProbeAsync 的 `HttpRequestMessage` 上（其他 endpoint 暂不动）
6. 测试新增：
   - `Probe_ReturnsRateLimited_On429_WithRetryAfterDeltaSeconds`：返回 429 + `Retry-After: 30`，断言 status / httpStatus / RetryAfter == 30s
   - `Probe_ReturnsRateLimited_On429_WithoutRetryAfter`：返回 429 无 header，断言 RetryAfter == 60s (兜底)
   - `Probe_SendsExpectedHeaders`：拦截 request，断言 `Accept` / `X-Requested-With` header 存在

## Acceptance Criteria

- [ ] `NextBotProbeStatus.RateLimited` 枚举值存在
- [ ] `NextBotProbeResult` 包含 `TimeSpan? RetryAfter` 属性，默认值 `null`
- [ ] ProbeAsync 收到 HTTP 429 时返回 `Status == RateLimited` 且 `RetryAfter != null`
- [ ] `Retry-After: 30`（delta-seconds）正确解析为 `TimeSpan.FromSeconds(30)`
- [ ] 缺失或非法 `Retry-After` 兜底为 `TimeSpan.FromSeconds(60)`
- [ ] `VerifyNextBotConnectionAsync` 在 `RateLimited` 时 WARN 日志包含 RetryAfter 秒数
- [ ] `VerifyNextBot` REST endpoint 在 `RateLimited` 时返回 `retryAfterSeconds` 字段
- [ ] ProbeAsync 请求带 `Accept: application/json` 和 `X-Requested-With: NextBotWebUI` header
- [ ] 既有 8 个 Probe 测试用例全部通过
- [ ] 新增 3 个测试用例通过
- [ ] `dotnet build` 0 warning 0 error
- [ ] `dotnet test` 全绿

## Definition of Done

- 代码合规 `quality-guidelines.md` / `logging-guidelines.md` / `error-handling.md`
- 单测覆盖三个新行为（429 with Retry-After / 429 without Retry-After / new headers）
- 不破坏现有 ProbeAsync 8 个用例
- `NextBotProbeResult` 新增字段为 `default null` 不破坏 `FakeProbeService` 在 ConfigEndpointsTests 里的构造调用（看
  `ConfigEndpointsTests.cs:159` 是否需要补默认值，trellis-implement 实操时确认）

## Technical Approach

### NextBotProbeResult 形状变更

```csharp
public sealed record NextBotProbeResult(
    NextBotProbeStatus Status,
    int? HttpStatus,
    string Message,
    TimeSpan? RetryAfter = null);  // ← 默认 null，仅 RateLimited 路径填值
```

加默认参数能保持现有构造点不破坏（`FakeProbeService` / 其他 `new NextBotProbeResult(...)` 调用点不需要改）。

### Retry-After 解析逻辑

```csharp
private static TimeSpan ParseRetryAfter(HttpResponseHeaders headers)
{
    var ra = headers.RetryAfter;
    if (ra is null) return TimeSpan.FromSeconds(60);
    if (ra.Delta is { } delta && delta > TimeSpan.Zero) return delta;
    if (ra.Date is { } date)
    {
        var remaining = date - DateTimeOffset.UtcNow;
        if (remaining > TimeSpan.Zero) return remaining;
    }
    return TimeSpan.FromSeconds(60);  // 兜底
}
```

### 调用方日志风格

按 `logging-guidelines.md` human-readable 风格（实施前必读 spec）。例：

```
PluginLogger.Warn($"连接 NextBot 触发上游限流（HTTP 429），建议 {(int)result.RetryAfter!.Value.TotalSeconds} 秒后重试");
```

## Decision (ADR-lite)

**Context**: 服务端新增 429 速率限制，老代码把 429 误判为 "Unreachable" 网络问题。
**Decision**: 新增 `RateLimited` 枚举 + `RetryAfter` 字段；不引入重试库，调用方只调整日志 / REST 响应字段。
**Consequences**:
- 优点：最小改动覆盖问题；与现有结构一致；REST API 向前兼容（新字段只在限流路径出现）
- 取舍：不自动按 `RetryAfter` 重试（当前两个调用方都无重试循环，不必要）；若未来加重试需先决定全局策略

## Out of Scope

- 不引入 `CookieContainer` / `AllowAutoRedirect = false`（文档里那段是给 WebUI 浏览器登录链路用的，插件走 query token 不消费 cookie）
- 不为业务请求（`login-requests` / `users` / `player-events`）加 401 自动重探活
- 不改其他 endpoint 的错误信封处理逻辑（按 code 判定的迁移建议只针对探活；业务 endpoint 已有 `TryParseErrorBody` 把 code 拼到 reason）
- 不引入 Polly / 通用重试库
- 不重构 `NextBotProbeResult` 为继承结构 / 多态
- 不给其他 endpoint 加 `Accept` / `X-Requested-With` header（本次只动 ProbeAsync）
- 不调整 `NextBotSettings` 配置形状

## Technical Notes

- 受影响文件：
  - `NextBotAdapter/Services/NextBot/NextBotSessionProbeService.cs`（核心改动）
  - `NextBotAdapter/Plugin/NextBotAdapterPlugin.cs:670-700`（switch 加分支 + 日志）
  - `NextBotAdapter/Rest/ConfigEndpoints.cs:96-125`（RestObject 加 retryAfterSeconds）
  - `NextBotAdapter.Tests/NextBotSessionProbeServiceTests.cs`（3 新用例）
- 参考 spec：`backend/quality-guidelines.md`、`backend/logging-guidelines.md`、`backend/error-handling.md`
- 上一轮分析（429 设计取舍）：本任务上游 brainstorm 输入里已固化
