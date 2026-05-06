# feat: 在 user stats 增加地图探索度百分比

## Goal

给 REST `GET /users/{user}/stats` 响应里增加一个 `mapExplorationPercent` 字段，表示该玩家已探索 tile 数占全图 tile 数的百分比。机器人前端用这个数值显示玩家"地图探索度"作为进度指标。

## What I already know

### 现状

- `IPlayerExplorationTracker.GetBitmap(accountName)` 已存在，返回 BitArray 的防御性拷贝；cache miss 走 lazy-load（上一任务实现）
- BitArray 长度 = `Main.maxTilesX * Main.maxTilesY`（每个 tile 一 bit），由 `PlayerExplorationTracker.GetOrCreateBitmap` 用 `new BitArray(width * height)` 创建
- 持久化文件大小 = `(bitCount + 7) / 8` 字节；`BitArray(byte[])` 重建时设 `Length = expectedBitCount`，超出 Length 的位会被零化（`MarkBox` stamp 也保证不写入越界 bit），因此 PopCount 可直接对 int[] 整数做位计数无需 mask
- 现有 stats 端点结构（`UserEndpoints.Stats` / `UserInfoService.TryGetUserInfo` / `UserInfoResponse` record + `UserInfoMapper`）：`Stats(args)` 工厂方法读 route → `TryGetUserInfo` 查 PlayerData → `with` 表达式 patch `OnlineSeconds`

### 现有 Stats 响应字段（`UserInfoResponse`）

`health`、`maxHealth`、`mana`、`maxMana`、`questsCompleted`、`deathsPve`、`deathsPvp`、`onlineSeconds`

### User 已拍板设计

- 暴露方式：**只**加进 `/users/{user}/stats` 字段，不做单独 endpoint，不做 leaderboard
- 分母：**`bitmap.Length`（= `maxTilesX * maxTilesY`，全图）**——user 判断 Terraria 全图都是可达的，100% 探索是可达成的
- 玩家无 bitmap（从未上线 / 未探索）：返回 0（与现有 stats 字段缺数据返回 0 的风格一致，不报 404）

## Requirements

- `UserInfoResponse` record 新增字段 `MapExplorationPercent` (`double`，JSON 字段名 `mapExplorationPercent`)，默认 0
- `UserEndpoints.Stats` 响应新增 `mapExplorationPercent` 字段，值同 `UserInfoResponse.MapExplorationPercent`
- 在 `IPlayerExplorationTracker` 上加新方法 `double GetExplorationPercent(string accountName)`，封装"取 bitmap → PopCount → 算百分比"的整套语义；返回值 `0.0–100.0`，保留 2 位小数
- 计算路径复用现有 `GetBitmap`（自动享受 lazy-load + 拷贝快照）
- bitmap 不存在 / 长度 0 / 失败 → return `0.0`（不抛异常）
- 高效位计数：`BitArray.CopyTo(int[])` + `BitOperations.PopCount`（`System.Numerics`），对 int[] 整数遍历——单次调用大世界 (~20M bit) 量级几 ms，可接受
- `UserInfoService.TryGetUserInfo` 增加 optional `IPlayerExplorationTracker?` 参数，传入则计算 percent，否则保持 0
- `UserEndpoints.Stats(args)` 工厂方法把 `UserEndpoints.ExplorationTracker` 传进去（已有静态字段，复用即可）

## Acceptance Criteria

- [ ] `GET /users/{user}/stats` 响应 JSON 多一个 `mapExplorationPercent` 字段（double，0–100，保留 2 位小数）
- [ ] 玩家从未上线 / 无 bitmap：`mapExplorationPercent: 0.0`
- [ ] 玩家有持久化 bitmap 但本次会话未登录（lazy-load 路径）：仍能正确计算并返回（不再是 0）
- [ ] 全 0 bitmap → `0.0`；全 1 bitmap → `100.0`；半数 set → `~50.0`（允许 ±0.01 误差）
- [ ] 不影响其他现有 stats 字段（值与现状一致）
- [ ] `dotnet build` 0 警告 0 错误
- [ ] `dotnet test` 全部通过（267 + 新增至少 4）
- [ ] `docs/REST_API.md` 的 `/users/{user}/stats` 端点文档新增字段说明

## Definition of Done

- 所有测试 green
- 不改路由 / 状态码 / 错误码 / 错误文案 / 日志字段
- 不改 leaderboard 端点 / 不新增 REST 端点 / 不改 `/users/{user}/map-image`
- 文档与代码字段名一致

## Technical Approach

### 1. `IPlayerExplorationTracker` / `PlayerExplorationTracker`

新增方法：

```csharp
double GetExplorationPercent(string accountName);
```

实现：

```csharp
public double GetExplorationPercent(string accountName)
{
    var bitmap = GetBitmap(accountName);
    if (bitmap is null || bitmap.Length == 0)
    {
        return 0.0;
    }

    var ints = new int[(bitmap.Length + 31) / 32];
    bitmap.CopyTo(ints, 0);

    var explored = 0;
    foreach (var v in ints)
    {
        explored += BitOperations.PopCount((uint)v);
    }

    return Math.Round(100.0 * explored / bitmap.Length, 2);
}
```

- 复用 `GetBitmap` → cache 命中 / lazy-load / 防御性拷贝全部继承
- 拷贝快照使 PopCount 不需要 hold lock
- `BitOperations.PopCount` 在 .NET 9 走 SIMD 加速

### 2. `UserInfoResponse`

新增字段：

```csharp
public sealed record UserInfoResponse(
    [property: JsonProperty("health")] int Health,
    ...
    [property: JsonProperty("onlineSeconds")] long OnlineSeconds = 0,
    [property: JsonProperty("mapExplorationPercent")] double MapExplorationPercent = 0);
```

### 3. `UserInfoService.TryGetUserInfo`

新增 overload 接受 `IPlayerExplorationTracker?`：

```csharp
public static bool TryGetUserInfo(
    string user,
    IPlayerDataAccessor accessor,
    IOnlineTimeService? onlineTimeService,
    IPlayerExplorationTracker? explorationTracker,
    out UserInfoResponse response,
    out string? error)
{
    response = new UserInfoResponse(0, 0, 0, 0, 0, 0, 0);
    error = null;
    if (!accessor.TryGetPlayerData(user, out var data, out error)) return false;

    response = UserInfoMapper.CreateResponse(data) with
    {
        OnlineSeconds = onlineTimeService?.GetTotalSeconds(user) ?? 0,
        MapExplorationPercent = explorationTracker?.GetExplorationPercent(user) ?? 0
    };
    return true;
}
```

旧 overload 保留为 wrapper（向后兼容）：transitively 调新 overload 传 `null`。

### 4. `UserEndpoints.Stats`

修改：

```csharp
public static object Stats(RestRequestArgs args)
    => Stats(ReadRouteUser(args), UserDataService.Default, OnlineTimeService, ExplorationTracker);

public static object Stats(
    string? user,
    IPlayerDataAccessor accessor,
    IOnlineTimeService? onlineTimeService = null,
    IPlayerExplorationTracker? explorationTracker = null)
{
    if (string.IsNullOrWhiteSpace(user)) return EndpointResponseFactory.MissingUser();
    if (!UserInfoService.TryGetUserInfo(user, accessor, onlineTimeService, explorationTracker, out var response, out var error))
    {
        return EndpointResponseFactory.Error(error ?? "User was not found.");
    }
    return new RestObject("200")
    {
        { "health", response.Health },
        { "maxHealth", response.MaxHealth },
        { "mana", response.Mana },
        { "maxMana", response.MaxMana },
        { "questsCompleted", response.QuestsCompleted },
        { "deathsPve", response.DeathsPve },
        { "deathsPvp", response.DeathsPvp },
        { "onlineSeconds", response.OnlineSeconds },
        { "mapExplorationPercent", response.MapExplorationPercent }
    };
}
```

### 5. 测试

- `PlayerExplorationTrackerTests.cs` 新增：
  - `GetExplorationPercent_ShouldReturnZero_WhenBitmapMissing`
  - `GetExplorationPercent_ShouldReturnZero_ForAllZeroBitmap`（通过 stamp 0 个 box 后查询）
  - `GetExplorationPercent_ShouldReturn100_ForAllOneBitmap`（用 SpyStorage 预存全 1 bitmap）
  - `GetExplorationPercent_ShouldReturnPartial_ForKnownPattern`（已知比例的 bitmap，断言 percent 在 ±0.01 范围内）
  - `GetExplorationPercent_ShouldLazyLoadFromStorage`（cache miss + storage 有 bitmap → 命中）

- `UserEndpointsTests.cs` 或新增 `UserStatsTests.cs`：
  - `Stats_ShouldIncludeMapExplorationPercent_WhenTrackerProvided`
  - `Stats_ShouldDefaultToZero_WhenTrackerNull`

- `UserInfoMapperTests.cs` / 新增：验证 `mapExplorationPercent` 字段在 record 序列化中正确出现（如有现成 mapper 测试，加一条）

## Decision (ADR-lite)

**Context**：现有 user stats 端点已经聚合了玩家健康度 / 法力 / 任务 / 死亡数 / 在线时长，缺一个"地图探索度"指标作为玩家进度的代理。bitmap 数据已经存在，加一个百分比字段是顺势补全。

**Decision**：在现有 `/users/{user}/stats` 字段集追加 `mapExplorationPercent`（double，0–100，2 位小数），分母用 `bitmap.Length` (= 全图)。计算逻辑封装在 `IPlayerExplorationTracker.GetExplorationPercent` 上，复用 `GetBitmap` 的 lazy-load。

**Consequences**：
- 优点：最低侵入（不新增端点、不动路由）；与现有 stats 字段命名风格一致；性能可接受（PopCount + SIMD，几 ms）；机器人前端单次请求拿到全套玩家进度
- 缺点：分母选 `bitmap.Length` 意味着 100% 是"reveal box 把全图覆盖一遍"——空中、虚空（如果有的话）也算分母；user 已拍板接受
- 待评估：未来如果 user 觉得百分比偏低，可以升级到"仅可达 tile"分母；本任务不做

## Out of Scope

- 不新增独立端点（`/users/{user}/map-exploration`、`/leaderboard/map-exploration` 都不做）
- 不改分母逻辑（不引入"可达 tile"判定）
- 不改 stamp / lazy-load / 持久化路径
- 不改 REST 路由 / 状态码 / 错误码 / 错误文案
- 不改 `/users/{user}/map-image` / `/world/map-image` / `/world/map/file`
- 不引入缓存层（每次请求实时算 PopCount，量级几 ms）
- 不做 percent 历史轨迹 / 增量记录
- 不改持久化文件格式

## Technical Notes

### 涉及文件

- 产品代码：
  - `NextBotAdapter/Services/Exploration/IPlayerExplorationTracker.cs`（接口加 `GetExplorationPercent`）
  - `NextBotAdapter/Services/Exploration/PlayerExplorationTracker.cs`（实现）
  - `NextBotAdapter/Models/Responses/UserInfoResponse.cs`（record 加字段）
  - `NextBotAdapter/Services/UserData/UserInfoService.cs`（overload 加 tracker 参数）
  - `NextBotAdapter/Rest/UserEndpoints.cs`（Stats 端点接 tracker、新增响应字段）
- 文档：
  - `docs/REST_API.md`（`/users/{user}/stats` 端点说明加字段）
- 测试：
  - `NextBotAdapter.Tests/PlayerExplorationTrackerTests.cs`（4-5 条新增）
  - `NextBotAdapter.Tests/UserEndpointsTests.cs`（2 条新增）

### 不需要改

- `IExplorationStorage` / `FileExplorationStorage`
- `PluginConfigService` / 路由 / 权限常量 / 持久化路径
- `MapImageService` / `MapFileService` / `PlayerMapImageService`
- `OnPlayerPostLogin` / `OnServerLeave` / `OnPlayerUpdate` 事件钩子
- `NextBotAdapterPlugin.Initialize`（`UserEndpoints.ExplorationTracker` 已经在那里赋值）

### 性能边界

大世界（8400×2400 = 20,160,000 bit）：
- `bitmap.CopyTo(int[])`：拷贝 ~80 KB（int[] 630K 个）→ μs 量级
- `BitOperations.PopCount` 循环 630K 次：.NET 9 SIMD 下 ~几 ms
- 总开销：单次 stats 请求可控、可接受、不需缓存

### Future Evolution

- 若 user 体感百分比偏低，可升级分母语义到"仅可达 tile"——一次性扫 `Main.tile` 计算"非虚空 + 非纯空气" tile 数缓存到世界 ID 维度
- 若需要全服 leaderboard，新增 `/leaderboard/map-exploration` 单独端点（遍历 `Data/Explored/{worldId}/*.bin` PopCount 排序）
