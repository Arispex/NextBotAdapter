# feat: 全玩家探索区域并集渲染端点

## Goal

新增 REST `GET /nextbot/world/explored-map-image`：把所有 TShock 账号的"已探索 bitmap"按位 OR 合成一张并集图，用现有 `IPlayerMapImageService` 渲染管线输出 PNG。语义上"任意玩家走过的区域"。

## What I already know

### 现有建材（全部可复用，无新基础设施）

| 组件 | 复用方式 |
|---|---|
| `IUserDataGateway.GetAllUserAccounts()` | 枚举所有 TShock 账号（与 leaderboard 一致） |
| `IPlayerExplorationTracker.GetBitmap(name)` | 自动走 lazy-load + `_missingFiles` 负缓存（round 2 fix C/B 后），无 bitmap 文件账号短路 |
| `IPlayerMapImageService.Generate(fileName, bitmap)` | 已支持任意 fileName + 任意 BitArray，直接传 union bitmap |
| `MapRenderMutex.Lock` | 与 `/users/{user}/map-image` 共用一把锁串行化 |
| `BitArray.Or` | 按字位或，in-place 合并 |
| `FileExplorationStorage.Load` size 校验 | 进入 `_bitmaps` 的 BitArray 长度一定 = `maxX*maxY`，Or 长度安全 |

### 现有 leaderboard / endpoint 对称风格（参考实施）

- 路由常量在 `Infrastructure/EndpointRoutes.cs`，kebab-case 路径
- 权限常量在 `Infrastructure/Permissions.cs`，snake_case
- `Rest/MapEndpoints.cs` 中 `Image(args) => Image(Service)` 工厂模式（service 注入 `IMapImageService`）
- 注册器 `Rest/EndpointRegistrar.cs` 加一行 `SecureRestCommand`
- Plugin Initialize 给静态 `Service` 字段赋值

### 用户拍板方向

1. **路由**：`/nextbot/world/explored-map-image`（与 `/world/map-image` 全亮 family 对称）
2. **fileName**：`world-explored`（生成时拼出来如 `world-explored-2026-05-07_10-30-15.png`）
3. **不缓存**：每次请求实时合并 + 实时渲染，请求频率不高可接受
4. **更新 API 文档**：`docs/REST_API.md` 加新端点章节

## Decision (ADR-lite)

**Context**：单玩家视角图（`/users/{user}/map-image`）和全亮地图（`/world/map-image`）已有；缺一个"全服共享探索"视角。机器人前端要展示"哪些地方有人走过"作为服务器探索进度指标。

**Decision**：新增 `GET /nextbot/world/explored-map-image`，按现有 `MapEndpoints` / leaderboard 三件套模式（路由 + 权限 + endpoint factory + service + registrar）实现。Service 内部按位 OR 合并所有玩家 bitmap，输出复用 `IPlayerMapImageService` 渲染管线。

**Consequences**：
- 优点：与现有 `/world/map-image`（全亮）/ `/users/{user}/map-image`（单人）形成 API family；零新基础设施（全部复用现有组件）；leverage round 2 修过的负缓存让冷启动可接受
- 缺点：每次请求都重算 union（不缓存），N 个账号循环 + bitmap copy + Or 一次；PNG 编码 1-2s 持 `MapRenderMutex.Lock` 阻塞其他渲染。请求频率低可接受
- 待评估：未来如发现请求频率高或合并耗时影响响应延迟，可加 5 分钟 TTL 缓存

## Requirements

### 路由 + 权限 + 注册

- `Infrastructure/EndpointRoutes.cs` 加 `public const string WorldExploredMapImage = "/nextbot/world/explored-map-image";`
- `Infrastructure/Permissions.cs` 加 `public const string WorldExploredMapImage = "nextbot.world.explored_map_image";`
- `Rest/EndpointRegistrar.cs` 在 `WorldMapImage` 旁加 `new SecureRestCommand(EndpointRoutes.WorldExploredMapImage, MapEndpoints.ExploredImage, Permissions.WorldExploredMapImage)`

### Service 层

- 新接口 `Services/World/IWorldExploredMapImageService.cs`：
  ```csharp
  public interface IWorldExploredMapImageService
  {
      (string FileName, byte[] Content) Generate();
  }
  ```
- 新实现 `Services/World/WorldExploredMapImageService.cs`：
  - 构造注入：`IUserDataGateway`、`IPlayerExplorationTracker`、`IPlayerMapImageService`、`Func<(int Width, int Height)> worldSizeProvider`
  - `Generate()` 流程：取世界尺寸 → 创建空 union BitArray → 遍历 `gateway.GetAllUserAccounts()` → 对每个账号调 `tracker.GetBitmap(username)`（null 跳过）→ `union.Or(bitmap)` → `_renderer.Generate("world-explored", union)` 返回
  - 世界尺寸 ≤ 0 时返回空 BitArray 仍走渲染（`PlayerMapImageService` 自身处理）—— 与单玩家路径一致

### Endpoint

- `Rest/MapEndpoints.cs` 加：
  - `public static IWorldExploredMapImageService? ExploredService { get; set; }`
  - `public static object ExploredImage(RestRequestArgs args) => ExploredImage(ExploredService);`
  - `public static RestObject ExploredImage(IWorldExploredMapImageService? svc)`：null → 500 "World explored map service is not configured."；try 调 `svc.Generate()` → 200 + `{ fileName, base64 }`；catch → 500 + ex.Message
- 错误响应风格、status code、错误文案与 `UserEndpoints.MapImage` 完全对齐

### Plugin wiring

- `Plugin/NextBotAdapterPlugin.cs:Initialize`：
  - 提取 `var playerMapImageService = new PlayerMapImageService();` 局部变量
  - `UserEndpoints.PlayerMapImageService = playerMapImageService;`
  - `MapEndpoints.ExploredService = new WorldExploredMapImageService(UserDataService.DefaultGateway, _playerExplorationTracker, playerMapImageService, () => (Main.maxTilesX, Main.maxTilesY));`

### 文档

- `docs/REST_API.md` 加新章节，参考 `/world/map-image` 与 `/users/{user}/map-image` 的现有布局。响应字段、status code、permission、备注（合并所有玩家 bitmap、未上线玩家自动跳过、与单玩家端点共享渲染管线）

## Acceptance Criteria

- [ ] `GET /nextbot/world/explored-map-image` 返回 `{ fileName, base64 }`，fileName 形如 `world-explored-{timestamp}.png`
- [ ] 多个玩家有不同 bitmap 时，输出 PNG 是 OR 合并结果（任意玩家走过 → 该 tile 显示）
- [ ] 全服 0 玩家有数据 → 返回全黑 PNG（不报 404）
- [ ] 服务未配置 → 500 "World explored map service is not configured."
- [ ] 渲染异常 → 500 + ex.Message
- [ ] 路由 / 权限注册到位
- [ ] `dotnet build` 0 警告 0 错误
- [ ] `dotnet test` 全部通过（304 baseline + 至少 5 新增）
- [ ] `docs/REST_API.md` 更新

## Definition of Done

- 全部测试 green、build 干净
- 不改 `/world/map-image` / `/users/{user}/map-image` / `/world/map/file` 任何行为
- 不改 leaderboard / 持久化 / stamp / lazy-load / 渲染管线
- 不改路由 / 权限既有命名风格
- 文档与代码一致

## Technical Approach

### 1. `Services/World/IWorldExploredMapImageService.cs` + `WorldExploredMapImageService.cs`

```csharp
public interface IWorldExploredMapImageService
{
    (string FileName, byte[] Content) Generate();
}

public sealed class WorldExploredMapImageService : IWorldExploredMapImageService
{
    private readonly IUserDataGateway _gateway;
    private readonly IPlayerExplorationTracker _tracker;
    private readonly IPlayerMapImageService _renderer;
    private readonly Func<(int Width, int Height)> _worldSizeProvider;

    public WorldExploredMapImageService(
        IUserDataGateway gateway,
        IPlayerExplorationTracker tracker,
        IPlayerMapImageService renderer,
        Func<(int Width, int Height)> worldSizeProvider)
    {
        _gateway = gateway;
        _tracker = tracker;
        _renderer = renderer;
        _worldSizeProvider = worldSizeProvider;
    }

    public (string FileName, byte[] Content) Generate()
    {
        var (width, height) = _worldSizeProvider();
        var union = new BitArray(Math.Max(0, width * height));

        foreach (var (_, username) in _gateway.GetAllUserAccounts())
        {
            var bitmap = _tracker.GetBitmap(username);
            if (bitmap is not null && bitmap.Length == union.Length)
            {
                union.Or(bitmap);
            }
        }

        return _renderer.Generate("world-explored", union);
    }
}
```

`bitmap.Length == union.Length` 是防御性的（理论上 storage 层已保证一致）；不一致则跳过该账号——保持 fail-safe，不抛异常。

### 2. `Infrastructure/EndpointRoutes.cs` + `Permissions.cs`

加常量。

### 3. `Rest/MapEndpoints.cs`

参考现有 `Image` 方法的工厂 / 主方法 / 响应组装风格新增 `ExploredImage`。

### 4. `Rest/EndpointRegistrar.cs`

加一行 `SecureRestCommand`，紧邻 `WorldMapImage` 注册。

### 5. `Plugin/NextBotAdapterPlugin.cs:Initialize`

提取 `playerMapImageService` 局部变量；构造并赋值 `MapEndpoints.ExploredService`。

### 6. `docs/REST_API.md`

加新端点章节，参考现有 `/world/map-image` 章节模板。要点：
- 路径、方法、permission node
- 响应字段（status / fileName / base64）
- 状态码（200 / 500）
- 备注：合并所有 TShock 账号已探索区域；未探索 / 未上线玩家自动跳过；fileName 前缀 "world-explored"；不缓存，每次实时计算

### 7. 测试

`NextBotAdapter.Tests/WorldExploredMapImageServiceTests.cs`（**新增**）：
- `Generate_ShouldUnionAllPlayerBitmaps`：3 个 fake 账号有 disjoint bitmaps → 验证传给 fake renderer 的 BitArray 是三者 OR
- `Generate_ShouldSkipAccountsWithNullBitmap`：fake tracker 对某账号返回 null → 不影响其他账号 union
- `Generate_ShouldUseWorldExploredAsFileNameSeed`：fake renderer 收到的 name 参数 == `"world-explored"`
- `Generate_ShouldRenderAllZero_WhenNoAccountsHaveData`：所有 GetBitmap 返回 null → union 全 0 → 仍调 renderer

`NextBotAdapter.Tests/MapEndpointsTests.cs`（**扩展**）：
- `ExploredImage_ShouldReturn500_WhenServiceNotConfigured`
- `ExploredImage_ShouldReturn200_WithFileNameAndBase64_OnSuccess`

`NextBotAdapter.Tests/EndpointRouteDefinitionsTests.cs`：InlineData 加路由 + 权限常量

`NextBotAdapter.Tests/EndpointRegistrarTests.cs` / `EndpointBehaviorTests.cs`：AssertRoute 加新命令

如有现成 `FakePlayerMapImageService` / fake gateway，复用；否则在测试文件内 private class 模式新建。

## Out of Scope

- 不缓存（每次请求实时算）
- 不支持"按玩家集合 filter"（只渲染指定玩家集合）
- 不动 `/world/map-image` / `/users/{user}/map-image` / `/world/map/file`
- 不动 leaderboard / 持久化 / stamp / lazy-load / 渲染服务内部
- 不引入新的 gateway / tracker 方法
- 不动 reveal box / 插值 / 瞬移阈值
- 不动 `docs/CONFIGURATION.md`

## Technical Notes

### 涉及文件

- 产品代码（**新增**）：
  - `NextBotAdapter/Services/World/IWorldExploredMapImageService.cs`
  - `NextBotAdapter/Services/World/WorldExploredMapImageService.cs`
- 产品代码（**修改**）：
  - `NextBotAdapter/Rest/MapEndpoints.cs`（加 `ExploredService` 静态字段 + `ExploredImage` 端点方法）
  - `NextBotAdapter/Infrastructure/EndpointRoutes.cs`（加 `WorldExploredMapImage` 常量）
  - `NextBotAdapter/Infrastructure/Permissions.cs`（加 `WorldExploredMapImage` 常量）
  - `NextBotAdapter/Rest/EndpointRegistrar.cs`（加 `SecureRestCommand` 注册行）
  - `NextBotAdapter/Plugin/NextBotAdapterPlugin.cs`（Initialize 构造并注入 service）
- 文档：
  - `docs/REST_API.md`（加新端点章节）
- 测试（**新增**）：
  - `NextBotAdapter.Tests/WorldExploredMapImageServiceTests.cs`
- 测试（**修改**）：
  - `NextBotAdapter.Tests/MapEndpointsTests.cs`（如已有；否则新增端点契约测试）
  - `NextBotAdapter.Tests/EndpointRouteDefinitionsTests.cs`
  - `NextBotAdapter.Tests/EndpointRegistrarTests.cs`
  - `NextBotAdapter.Tests/EndpointBehaviorTests.cs`

### 不需要改

- `IPlayerExplorationTracker` / `PlayerExplorationTracker` / `IExplorationStorage` / `FileExplorationStorage`
- `IPlayerMapImageService` / `PlayerMapImageService`
- `IUserDataGateway`
- `MapImageService` / `MapFileService` / `MapRenderMutex`
- 路由 / 权限 / 注册 已有部分
- `OnPlayerUpdate` / `OnServerLeave` / `OnPlayerPostLogin` / `Dispose`
- `docs/CONFIGURATION.md`

### 性能特征

- 单次冷启动调用：N 个账号 × `_missingFiles` HashSet check（绝大多数 ~μs）+ 真实有数据账号 × bitmap copy + Or（~ms 级）
- PNG 编码：与单玩家版同规模，1-2s（持 `MapRenderMutex.Lock`）
- 内存：union BitArray ~2.5 MB（大世界）+ 各账号 bitmap snapshot 的 short-lived copy

### Future Evolution（不在本任务做）

- 5 分钟 TTL 结果缓存
- `?players=A,B,C` 子集过滤
- 在 leaderboard endpoint 同时返回 union percent
