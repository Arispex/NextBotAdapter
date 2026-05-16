# decode url-encoded route segments in all REST endpoints

## Goal

TShock REST 老框架不会自动 URL-decode 路径段（`args.Verbs[...]`）。客户端按 RFC 3986 把含非 ASCII 字符
的路径段编码（如 `千亦` → `%E5%8D%83%E4%BA%A6`）后，整个值原样落到业务层，导致白名单 / 黑名单 / 用户
查询 / 登录确认等所有走路径段读 `{user}` 的 endpoint 全部翻车。本任务统一在 verbs 来源 decode，覆盖
全部受影响 endpoint。

## What I already know

### 当前受影响的代码（已 grep 全审计）

`ReadRouteUser` 三段式 fallback (`Verbs → Parameters → Request.Parameters`) 在四个文件里完全重复：

| 文件 | 行号 | 覆盖 endpoints |
|---|---|---|
| `Rest/WhitelistEndpoints.cs` | 59-60 | `whitelist/add/{user}`, `whitelist/remove/{user}` |
| `Rest/BlacklistEndpoints.cs` | 105-106 | `blacklist/add/{user}`, `blacklist/remove/{user}` |
| `Rest/UserEndpoints.cs` | 122-123 | `users/{user}/inventory`, `users/{user}/stats`, `users/{user}/map-image` |
| `Rest/SecurityEndpoints.cs` | 57-58 | `security/confirm-login/{user}`, `security/reject-login/{user}` |

共 **9 个 endpoint** 受影响。

### 路径段参数审计

`Infrastructure/EndpointRoutes.cs` 全表 25 条 endpoint，**只有 `{user}` 这一个路径段参数**。没有
`{account}` / `{world}` / `{slot}` 之类的其他参数。Scope 干净。

### 现有约束

- `RequestParameters.User = "user"`（[Infrastructure/RequestParameters.cs:5](NextBotAdapter/Infrastructure/RequestParameters.cs)）
- 当前代码全仓**完全没有** `Uri.UnescapeDataString` / `UrlDecode` 调用 → 不存在历史 decode 路径要兼容
- 现有 `WhitelistEndpointsTests` 直接调 `WhitelistEndpoints.Add(user, service)`，**绕过** `ReadRouteUser`
  → 既有测试不会因新 helper 失败，但 helper 自身的测试需要从零写

### 已确认的 bug 现场

```
[2026-05-17T00:10:56.550+08:00] [INFO] [NextBotAdapter] 玩家 %E5%8D%83%E4%BA%A6 已加入白名单。
```

后续玩家 `千亦` 入服时，白名单文件里是 `%E5%8D%83%E4%BA%A6` → `WhitelistService.TryValidateJoin("千亦")`
匹配失败 → 玩家被拒入服。

## Open Questions

（无 —— 全部已收敛）

## Requirements

1. 新增共享 helper `Infrastructure/RouteParameters.cs`（命名待定，trellis-implement 可调）：
   ```csharp
   public static class RouteParameters
   {
       public static string? ReadDecodedRouteParam(RestRequestArgs args, string key);
   }
   ```
   语义：
   - verbs 来源：`Uri.UnescapeDataString` 解码后返回
   - 解码抛 `UriFormatException` → 返回原始 verbs 字符串（让上层 validation 拒掉）
   - verbs 空 / null → fall-through 到 `args.Parameters?[key]`
   - 仍空 → fall-through 到 `args.Request?.Parameters?[key]`
   - **不对** parameters / request.parameters 来源做 decode（服务端已 decode 过）
2. 四个 endpoint 文件的 `ReadRouteUser` 改为调 `RouteParameters.ReadDecodedRouteParam(args, RequestParameters.User)`
3. 新增专属测试文件覆盖 helper 行为（见 AC）
4. 数据迁移：**不做**（user 拍板：运维手动修。受影响数据量小、运维可控）

## Acceptance Criteria

- [ ] `RouteParameters.ReadDecodedRouteParam` 存在，签名匹配 Requirement 1
- [ ] verbs 含 `%E5%8D%83%E4%BA%A6` → 返回 `千亦`
- [ ] verbs 含 ASCII `Steve` → 返回 `Steve`（无副作用）
- [ ] verbs 含已解码的 `千亦`（理论上不应发生但要兜底）→ 返回 `千亦`，不破坏
- [ ] verbs 含 invalid 编码（如 `%Z%Z`）→ 不抛异常，返回原值
- [ ] verbs 为 null / empty → 走 `Parameters` 来源（无 decode）
- [ ] `Parameters` 含 `%E5%8D%83%E4%BA%A6` 时 → 不被 decode，原样返回（**与现状一致，证明没动 query 路径**）
- [ ] 四个 endpoint 文件的 `ReadRouteUser` 已迁移到新 helper
- [ ] 既有 `WhitelistEndpointsTests` / `BlacklistEndpointsTests` / `UserEndpointsTests` / `SecurityEndpointsTests` 全部通过
- [ ] `dotnet build` 0 warning 0 error
- [ ] `dotnet test` 全绿

## Definition of Done

- 代码合规 `backend/quality-guidelines.md` / `backend/logging-guidelines.md` / `backend/error-handling.md`
- helper 单测覆盖 5 条以上分支
- 既有 endpoint 测试不退化

## Technical Approach

```csharp
// Infrastructure/RouteParameters.cs (new file)
public static class RouteParameters
{
    public static string? ReadDecodedRouteParam(RestRequestArgs args, string key)
    {
        var fromVerb = args.Verbs?[key];
        if (!string.IsNullOrEmpty(fromVerb))
        {
            try
            {
                return Uri.UnescapeDataString(fromVerb);
            }
            catch (UriFormatException)
            {
                return fromVerb; // 让 upstream validation 拒掉
            }
        }
        return args.Parameters?[key] ?? args.Request?.Parameters?[key];
    }
}
```

四个 endpoint 改成：

```csharp
private static string? ReadRouteUser(RestRequestArgs args)
    => RouteParameters.ReadDecodedRouteParam(args, RequestParameters.User);
```

（保留 `ReadRouteUser` 私有壳，避免散弹式 import 改动）

## Decision (ADR-lite)

**Context**: 路径段不 decode 导致中文用户名翻车。User 要求"所有 rest api 都应该支持"，scope 限定为 verbs 来源的 `{user}` 段（全表唯一路径参数）。

**Decision A — helper 抽取**：在 `Infrastructure/RouteParameters.cs` 加共享 helper，四个 endpoint 调它。理由：一处修、grep 友好、后续新 endpoint 不会再忘。

**Decision B — 只 decode verbs**：query 路径 / request.parameters 来源服务端已 decode，重复 decode 会把合法 `%25` 错误转回 `%`。仅对 verbs 这一步加 decode。

**Decision C — invalid 编码兜底**：`UriFormatException` 时返回原始 verbs，**不抛**异常给上层，让现有 username validation 自然拒掉。

**Decision D — 数据迁移**：不做。User 选"运维手动修"。受影响数据量小、运维可控、零代码复杂度。

**Consequences**:
- 优点：单点修复 + 测试可控；既有 query path 完全不动；既有测试不退化；零数据风险
- 取舍：helper 提到 Infrastructure 后 endpoint 文件多一个依赖（轻微）

## Out of Scope

- 不改 TShock 的 REST 框架本身
- 不改 endpoint 路由模板 / 路径
- 不改 query string 解码路径（已 work）
- 不动其他非 `{user}` 路径参数（全表确认无其他）
- 不重构 endpoint 文件结构 / 不重命名
- 不引入新 REST endpoint
- **不做存量数据迁移**（user 决定运维手动修）

## Technical Notes

- 受影响文件：
  - 新增 `NextBotAdapter/Infrastructure/RouteParameters.cs`
  - 改 `Rest/WhitelistEndpoints.cs`、`Rest/BlacklistEndpoints.cs`、`Rest/UserEndpoints.cs`、`Rest/SecurityEndpoints.cs`（各改 1-2 行）
  - 新增 `NextBotAdapter.Tests/RouteParametersTests.cs`
- 参考 spec：`backend/quality-guidelines.md`、`backend/error-handling.md`、`backend/logging-guidelines.md`
