# feat: 地图探索率排行榜 REST API

## Goal

新增 REST `GET /nextbot/leaderboards/map-exploration` 端点，按"地图探索度百分比"降序返回所有 TShock 账号的排名。设计风格与现有 deaths / fishing-quests / online-time 三个排行榜端点完全对齐：URL kebab-case、permission snake_case、响应是 `{ entries: [...] }`、entry 是 `(username, <metric>)` record。

## What I already know

### 现有 leaderboard 风格（参考）

**Endpoint 三件套**（`Rest/LeaderboardEndpoints.cs`）：
```csharp
public static object Deaths(RestRequestArgs args) => Deaths(UserDataService.DefaultGateway);
public static object Deaths(IUserDataGateway gateway)
{
    var entries = DeathLeaderboardService.GetLeaderboard(gateway);
    return new RestObject("200") { { "entries", entries } };
}
```

**OnlineTime 的容错风格**（service 是 nullable，注入 null → 空 entries）：
```csharp
public static object OnlineTime(IOnlineTimeService? onlineTimeService)
{
    if (onlineTimeService is null)
        return new RestObject("200") { { "entries", Array.Empty<OnlineTimeLeaderboardEntryResponse>() } };
    ...
}
```

**Service 风格**（`Services/Leaderboards/DeathLeaderboardService.cs`）：
```csharp
public static IReadOnlyList<DeathLeaderboardEntryResponse> GetLeaderboard(IUserDataGateway gateway)
{
    var accounts = gateway.GetAllUserAccounts();
    var entries = new List<DeathLeaderboardEntryResponse>(accounts.Count);
    foreach (var (accountId, username) in accounts) { ... }
    return entries.OrderByDescending(e => e.Deaths).ToList();
}
```

**Entry record 风格**（`Models/Responses/DeathLeaderboardEntryResponse.cs`）：
```csharp
public sealed record DeathLeaderboardEntryResponse(
    [property: JsonProperty("username")] string Username,
    [property: JsonProperty("deaths")] int Deaths);
```

**常量 + 注册**：
- `Infrastructure/EndpointRoutes.cs`: `LeaderboardDeaths = "/nextbot/leaderboards/deaths"`、`LeaderboardOnlineTime = "/nextbot/leaderboards/online-time"`
- `Infrastructure/Permissions.cs`: `LeaderboardDeaths = "nextbot.leaderboards.deaths"`、`LeaderboardOnlineTime = "nextbot.leaderboards.online_time"`
- `Rest/EndpointRegistrar.cs`: `new SecureRestCommand(EndpointRoutes.LeaderboardOnlineTime, LeaderboardEndpoints.OnlineTime, Permissions.LeaderboardOnlineTime)`
- `NextBotAdapterPlugin.Initialize`: 给 `LeaderboardEndpoints.OnlineTimeService` 注入服务实例

### 现有的 GetExplorationPercent

上一任务（279bea0）刚加了 `IPlayerExplorationTracker.GetExplorationPercent(string accountName)`，复用 `GetBitmap` 的 lazy-load + 防御性拷贝路径：
- 输入：账号名
- 输出：double，0–100，2 位小数；缺数据 / 失败 → 0.0

完美适合在 leaderboard service 里循环调用。

### IUserDataGateway

`IUserDataGateway.GetAllUserAccounts()` 返回 `IReadOnlyList<(int AccountId, string Username)>`——所有 TShock 账号；`UserDataService.DefaultGateway` 是默认实例。

## Requirements

- 新端点 `GET /nextbot/leaderboards/map-exploration` 返回 `{ "entries": [ { "username": "alice", "mapExplorationPercent": 42.5 }, ... ] }`，按 `mapExplorationPercent` 降序
- 遍历 `gateway.GetAllUserAccounts()` 所有账号；每个账号调 `tracker.GetExplorationPercent(username)`；entry 字段名 / 风格与 stats 端点对齐（`mapExplorationPercent`，double，0–100，2 位小数）
- **包含所有账号**（与 deaths / fishing-quests 风格一致，0% 的也列在末尾）；不做 `limit` / 分页（与现有三个 leaderboard 一致，本期不引入）
- Tracker 为 null（注入异常 / 启动顺序）→ 返回空 entries（与 `OnlineTime` 风格一致）
- 响应 status 始终 `"200"`，不引入 4xx 错误路径
- 路由 / 权限 / 注册三件套与现有 leaderboard 完全对齐：
  - 路由常量 `EndpointRoutes.LeaderboardMapExploration = "/nextbot/leaderboards/map-exploration"`
  - 权限常量 `Permissions.LeaderboardMapExploration = "nextbot.leaderboards.map_exploration"`
  - 在 `EndpointRegistrar` 加一行 `SecureRestCommand`
- `LeaderboardEndpoints` 加静态 `IPlayerExplorationTracker? ExplorationTracker { get; set; }`，`Initialize()` 中赋值（与 `LeaderboardEndpoints.OnlineTimeService` 同模式）
- `docs/REST_API.md` 加新端点说明，列响应字段、状态码、权限节点、限流（如有）、与 stats 端点的语义关系

## Acceptance Criteria

- [ ] `GET /nextbot/leaderboards/map-exploration` 返回 `{ entries }`，每个 entry 形如 `{ username, mapExplorationPercent }`
- [ ] 排序：`mapExplorationPercent` 降序
- [ ] 包含所有 TShock 账号（即使 percent=0）
- [ ] tracker 注入 null → 返回空 entries（不抛异常）
- [ ] 路由 `/nextbot/leaderboards/map-exploration`、权限 `nextbot.leaderboards.map_exploration` 注册到位
- [ ] `dotnet build` 0 警告 0 错误
- [ ] `dotnet test` 全部通过（275 + 新增至少 3）
- [ ] `docs/REST_API.md` 文档更新

## Definition of Done

- 全部测试 green，0 警告 0 错误
- 不改其他 leaderboard 端点
- 不改 `IPlayerExplorationTracker.GetExplorationPercent` 实现
- 文档与代码一致

## Technical Approach

### 1. Entry record（`Models/Responses/MapExplorationLeaderboardEntryResponse.cs`）

```csharp
using Newtonsoft.Json;

namespace NextBotAdapter.Models.Responses;

public sealed record MapExplorationLeaderboardEntryResponse(
    [property: JsonProperty("username")] string Username,
    [property: JsonProperty("mapExplorationPercent")] double MapExplorationPercent);
```

### 2. Service（`Services/Leaderboards/MapExplorationLeaderboardService.cs`）

```csharp
using NextBotAdapter.Models.Responses;

namespace NextBotAdapter.Services;

public static class MapExplorationLeaderboardService
{
    public static IReadOnlyList<MapExplorationLeaderboardEntryResponse> GetLeaderboard(
        IUserDataGateway gateway,
        IPlayerExplorationTracker tracker)
    {
        var accounts = gateway.GetAllUserAccounts();
        var entries = new List<MapExplorationLeaderboardEntryResponse>(accounts.Count);

        foreach (var (_, username) in accounts)
        {
            var percent = tracker.GetExplorationPercent(username);
            entries.Add(new MapExplorationLeaderboardEntryResponse(username, percent));
        }

        return entries.OrderByDescending(e => e.MapExplorationPercent).ToList();
    }
}
```

### 3. Endpoint（`Rest/LeaderboardEndpoints.cs` 追加）

```csharp
public static IPlayerExplorationTracker? ExplorationTracker { get; set; }

public static object MapExploration(RestRequestArgs args)
    => MapExploration(UserDataService.DefaultGateway, ExplorationTracker);

public static object MapExploration(IUserDataGateway gateway, IPlayerExplorationTracker? tracker)
{
    if (tracker is null)
    {
        return new RestObject("200") { { "entries", Array.Empty<MapExplorationLeaderboardEntryResponse>() } };
    }

    var entries = MapExplorationLeaderboardService.GetLeaderboard(gateway, tracker);
    return new RestObject("200") { { "entries", entries } };
}
```

### 4. 路由 / 权限 / 注册

- `Infrastructure/EndpointRoutes.cs`：`public const string LeaderboardMapExploration = "/nextbot/leaderboards/map-exploration";`
- `Infrastructure/Permissions.cs`：`public const string LeaderboardMapExploration = "nextbot.leaderboards.map_exploration";`
- `Rest/EndpointRegistrar.cs`：在三个 leaderboard 旁加：
  ```csharp
  new SecureRestCommand(EndpointRoutes.LeaderboardMapExploration, LeaderboardEndpoints.MapExploration, Permissions.LeaderboardMapExploration),
  ```
- `Plugin/NextBotAdapterPlugin.cs`：`Initialize()` 现有 `UserEndpoints.ExplorationTracker = _playerExplorationTracker;` 旁边加 `LeaderboardEndpoints.ExplorationTracker = _playerExplorationTracker;`

### 5. 测试

`NextBotAdapter.Tests/MapExplorationLeaderboardServiceTests.cs`（新增 / 或合并到 `RestEndpointLogicTests.cs` 看现有风格——`DeathLeaderboardService` 是合并测试）：
- `GetLeaderboard_ShouldReturnEntriesSortedDesc_ByPercent`：3 个账号 percent=10/50/30 → 顺序 50/30/10
- `GetLeaderboard_ShouldIncludeAllAccounts_EvenWithZeroPercent`：tracker 对某些账号返回 0 仍然出现在结果里
- `GetLeaderboard_ShouldReturnEmpty_WhenNoAccounts`：gateway 空 → entries 空

`NextBotAdapter.Tests/RestEndpointLogicTests.cs` 或 endpoint 同款测试文件：
- `MapExploration_ShouldReturnEmpty_WhenTrackerIsNull`：tracker=null → entries 空数组
- `MapExploration_ShouldReturnSortedEntries_WhenTrackerProvided`：基本契约测试

确认现有路由 / 权限常量测试（如 `EndpointRouteDefinitionsTests`）会自动覆盖新增常量；如果是断言"完整列表"形式则需要补一行。

## Decision (ADR-lite)

**Context**：上一任务给 stats 端点加了 `mapExplorationPercent` 字段，但只能"按账号查单人"。机器人前端要展示全服探索排名，需要 leaderboard 端点。

**Decision**：参照现有 leaderboard 三件套（route + permission + endpoint + service + entry record + registrar）的对称布局新增"地图探索率"端点。计算复用刚做完的 `IPlayerExplorationTracker.GetExplorationPercent`，避免任何 PopCount / lazy-load / 缓存策略的重复实现。

**Consequences**：
- 优点：与现有 leaderboard 风格 100% 对齐（API consumer 学一个就会全部）；最小新增（1 个 record + 1 个 service + 1 个 endpoint 方法 + 3 处常量 / 注册）；leverage 现有缓存（in-memory bitmap + lazy-load）
- 缺点：首次调用 leaderboard 会触发所有"有 bitmap 文件但本进程未登录"账号的 lazy-load——一次性 IO 突增（每账号 1 次磁盘读 + BitArray 分配），后续命中 in-memory cache。生产场景如果有 1000 账号都有 bitmap，可能首次调用几秒级延迟；可接受
- 待评估：未来如需要分页 / `?limit=N` / `?excludeZero=true` 等参数，再单独立任务

## Out of Scope

- 不做分页 / `limit` 参数（现有 3 个 leaderboard 都不做）
- 不引入 leaderboard 结果缓存层（每次请求实时算）
- 不改 `GetExplorationPercent` 实现 / 性能特征
- 不改其他 leaderboard 端点
- 不改 stamp / 持久化 / lazy-load 逻辑
- 不改 `/users/{user}/stats` 或 `/users/{user}/map-image`
- 不引入新的 `IUserDataGateway` 方法
- 不改 reveal box / 插值 / 瞬移阈值

## Technical Notes

### 涉及文件

- 产品代码（**新增**）：
  - `NextBotAdapter/Models/Responses/MapExplorationLeaderboardEntryResponse.cs`
  - `NextBotAdapter/Services/Leaderboards/MapExplorationLeaderboardService.cs`
- 产品代码（**改**）：
  - `NextBotAdapter/Rest/LeaderboardEndpoints.cs`（加 `ExplorationTracker` 静态字段 + `MapExploration` 端点方法）
  - `NextBotAdapter/Infrastructure/EndpointRoutes.cs`（加 `LeaderboardMapExploration` 常量）
  - `NextBotAdapter/Infrastructure/Permissions.cs`（加 `LeaderboardMapExploration` 常量）
  - `NextBotAdapter/Rest/EndpointRegistrar.cs`（加注册行）
  - `NextBotAdapter/Plugin/NextBotAdapterPlugin.cs`（在 `Initialize` 给 `LeaderboardEndpoints.ExplorationTracker` 赋值）
- 文档：
  - `docs/REST_API.md`
- 测试（**新增**）：
  - `NextBotAdapter.Tests/MapExplorationLeaderboardServiceTests.cs`（service 单元测试）
- 测试（**改**）：
  - `NextBotAdapter.Tests/RestEndpointLogicTests.cs`（端点契约测试）
  - 如有 `EndpointRouteDefinitionsTests` 之类的"列出所有路由"测试，按需追加常量

### 不需要改

- `IPlayerExplorationTracker` / `PlayerExplorationTracker` / `IExplorationStorage` / `FileExplorationStorage`
- `UserEndpoints.cs` / `UserInfoService.cs` / `UserInfoResponse.cs`
- `MapImageService` / `MapFileService` / `PlayerMapImageService`
- 持久化目录 / 文件格式

### 性能边界

- 单次 leaderboard 请求最差情况：所有 N 个账号都有 bitmap 文件 → N 次 lazy-load + N 次 PopCount
- 大世界（20M bit）下 N=1000 的最差延迟：~几秒级（首次），后续 in-memory cache 命中后 ~几百 ms
- low-frequency endpoint（机器人前端定期刷新而非每次玩家操作触发），可接受

### Future Evolution

- 加 `?limit=N` 截断 + 加 `?excludeZero=true` 过滤
- 加 leaderboard cache（5 分钟过期），减少高频请求开销
- 加 `cacheBustHash` 字段帮助前端识别数据是否变化
