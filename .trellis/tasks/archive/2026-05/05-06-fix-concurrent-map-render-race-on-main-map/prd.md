# Fix concurrent map render race on Main.Map

## Goal

修复多个地图渲染端点共享全局静态 `Main.Map` 导致的并发竞态：当 `/nextbot/world/map-image`（全图）、`/nextbot/users/{user}/map-image`（玩家视角）和 `/nextbot/world/map-file`（map 文件）中的任意两个请求几乎同时到达时，三条流程都对 `Main.Map = CreateWorkingMap()` 这一全局赋值做了无锁覆盖，导致后到的请求把先到的工作状态完全覆盖，两个响应都返回**同一**视图（后到的那个），违背了 endpoint 各自的契约。

## What I already know

### 竞态路径（来源：上一个会话的 user 实测 + 代码定位）

[`MapImageService.Generate()`](NextBotAdapter/Services/World/MapImageService.cs) 和 [`PlayerMapImageService.Generate()`](NextBotAdapter/Services/World/PlayerMapImageService.cs:15) 各自有同样三步：

1. `Main.Map = CreateWorkingMap()` ←替换全局静态字段，**清空** `_tiles` 工作数组
2. 填充 `_tiles`（全图：`LightUpWholeMap` 全亮；玩家：`FillMaskedTiles` 按 bitmap 仅写已探索）
3. 循环读 `Main.Map._tiles[...]` 写 PNG

并发交错时（TShock REST 把每个请求派给线程池 worker）：

```
T-A 全图：Main.Map = workingMap_A
T-A 全图：LightUpWholeMap 把 _tiles 填亮
T-B 玩家：Main.Map = workingMap_B    ←整个 workingMap_A 被丢弃
T-B 玩家：FillMaskedTiles 写稀疏数据
T-A 全图：渲染读 Main.Map._tiles    ←读到 B 的玩家视图
T-B 玩家：渲染读 Main.Map._tiles    ←读到 B 的玩家视图
两张图都是玩家视角  ✗
```

User 实测确认：`/users/{user}/map-image` 和 `/world/map-image` 接连请求时，两张返回的 PNG 都是后到那个 endpoint 的视图。

### `MapFileService` 同款风险

[`MapFileService.GetMapFile()`](NextBotAdapter/Services/World/MapFileService.cs) 调 `MapHelper.SaveMap()` 内部依赖 `Main.Map`、`Main.MapFileMetadata`、`Main.ActivePlayerFileData` 等多个全局静态字段；逻辑同款，未报告但同样有竞态。

### 现有架构

- 三个 service 在不同文件、各自有 `PrepareMapEnvironment` 和 `CreateWorkingMap` 私有方法（重复代码，但本任务不动）
- service 实例由 [`NextBotAdapterPlugin.Initialize`](NextBotAdapter/Plugin/NextBotAdapterPlugin.cs) 在插件启动时创建并塞给 `MapEndpoints.Service` / `UserEndpoints.PlayerMapImageService` / `WorldEndpoints.MapFileService` 静态属性
- 现有 unit test 用 Fake service 实现接口，不触发真实 `Main.Map` 路径，**无法**直接复现竞态

## Requirements

1. 引入一个**单一**进程级 lock 对象，三个 service 的渲染方法**都用它**串行化
2. 锁范围必须覆盖 `PrepareMapEnvironment` + 填充 + 渲染读取 + 输出（PNG 编码 / `SaveMap` 写文件）的完整生命周期——任何在 lock 外读 `Main.Map._tiles` 的代码都要纳入锁
3. 修复后顺序请求两个 endpoint 应当返回各自正确的视图（user 之前已经能复现，修后他自己再测）

## Acceptance Criteria

- [ ] 新增的 lock 是**进程内单例**，三个 service 引用同一个引用（不是各持私有 `object`）
- [ ] `MapImageService.Generate()` / `PlayerMapImageService.Generate()` / `PlayerMapImageService.GenerateBlank()` / `MapFileService.GetMapFile()` 全部在 lock 内执行
- [ ] 现有 252+ 测试仍然全绿
- [ ] 新增至少 1 个测试，断言所有用 `Main.Map` 的渲染入口共享同一把锁（防止未来回退到分散锁）
- [ ] 构建 0 警告 0 错误
- [ ] 顺序请求两个 endpoint 时返回各自正确的视图（手动验证项）

## Definition of Done

- 代码改动遵循 backend quality-guidelines（thin endpoint / service-layer 业务逻辑 / 测试覆盖行为变更）
- lock 对象的命名 / 注释明确表达"保护 `Main.Map` 全局状态"，不要让未来读者以为是某个无关的同步原语
- 不动文档 / 不改 endpoint 契约

## Technical Approach

### 1. 新增 [`Services/World/MapRenderMutex.cs`](NextBotAdapter/Services/World/MapRenderMutex.cs)

```csharp
namespace NextBotAdapter.Services;

/// <summary>
/// Single process-wide mutex serializing every code path that reads or writes
/// the Terraria static map state (Main.Map, Main.MapFileMetadata, etc.).
///
/// Multiple REST endpoints (/world/map-image, /users/{user}/map-image,
/// /world/map-file) share these globals with no other synchronization. Without
/// this lock, two concurrent requests interleave their CreateWorkingMap +
/// fill + render steps and both return the LATER request's view.
///
/// Always wrap the entire rendering lifecycle (Prepare + fill + read + encode/
/// SaveMap) in a single lock(MapRenderMutex.Lock) — partial coverage still
/// races during PNG encoding or SaveMap.
/// </summary>
internal static class MapRenderMutex
{
    public static readonly object Lock = new();
}
```

### 2. 三个 service 的入口方法外层 `lock(MapRenderMutex.Lock)`

- `MapImageService.Generate()`
- `PlayerMapImageService.Generate(string accountName, BitArray bitmap)`
- `PlayerMapImageService.GenerateBlank(string accountName)`
- `MapFileService.GetMapFile()`

锁范围 = 整个 method body（最外层 wrap）。

### 3. 测试

新增 `NextBotAdapter.Tests/MapRenderMutexTests.cs`：

- `Lock_IsSingleSharedInstance` —— `MapRenderMutex.Lock` 在多次访问下返回同一引用（间接验证不是 property 每次 new）
- `Lock_IsNonNull`
- 跨 service 共享性的代码层验证比较难直接 unit test（service 实现里 `lock` 用的对象是 `MapRenderMutex.Lock`，但这是编译期绑定）。**额外做法**：用 reflection 扫 `MapImageService` / `PlayerMapImageService` / `MapFileService` 三个类的 `Generate` / `GetMapFile` 方法 IL，断言里面引用了 `MapRenderMutex.Lock` 字段。这条比较脆弱；如果代价过高，**可以省略**，留 acceptance criterion 通过 code review 确认

> 实施 agent 自行决定要不要做 IL 断言；做不到也不是 blocker，能验证 lock 单例存在即可。

### 4. 不引入

- 不改 service 间共享代码（保留 `CreateWorkingMap` / `PrepareMapEnvironment` 各自重复）
- 不改 endpoint 契约
- 不改异步模型（仍然同步阻塞，TShock REST worker 排队等锁）

## Decision (ADR-lite)

- **Context**：跨 service 的全局静态 `Main.Map` 共享导致并发竞态；user 已 reproduce 并选定方案 A（lock 串行化），方案 B（去掉 `Main.Map` 全局依赖）已被推迟
- **Decision**：新增 `Services/World/MapRenderMutex.cs` 暴露 `static readonly object Lock`，三个 service 的渲染入口方法外层包 `lock(MapRenderMutex.Lock)`
- **Consequences**：
  - 优点：1 个新文件 + 4 处 method 体外层包 lock，最小改动；明确表达"保护 `Main.Map` 全局"的意图
  - 代价：所有地图渲染请求串行化（高并发下排队，但本插件的渲染请求频率本就极低，不是热路径）
  - 后续优化：方案 B（去掉 `Main.Map` 全局依赖）单独立项，在那个任务里可以拆掉这把锁

## Out of Scope

- 不去掉 `Main.Map` 全局依赖（方案 B）
- 不引入 async / 取消 token / 请求队列调度
- 不改 endpoint 路由 / 响应格式 / 错误码 / 渲染参数（reveal box / 插值 / 瞬移阈值等）
- 不抽 service 间共享的 `CreateWorkingMap` / `PrepareMapEnvironment` 重复代码（DRY 改进留给方案 B）
- 不写多线程集成测试（要求真实 `Main.Map`，单测用 Fake 无法复现）

## Future Evolution Note

未来想根除竞态而不是串行化所有请求，应做**方案 B**：每次渲染分配自己的 `MapTile[,]` 数组，避免触碰 `Main.Map` 全局——但这需要确认 `MapHelper.CreateMapTile` / `GetMapTileXnaColor` / `MapHelper.SaveMap` 是否依赖 `Main.Map` 自身（之前研究过 `CreateMapTile` 依赖 `Main.tile` 但似乎不依赖 `Main.Map`）。本期 lock 是"止血"，方案 B 是"治本"。

## Technical Notes

- 影响文件：
  - 新增：[`NextBotAdapter/Services/World/MapRenderMutex.cs`](NextBotAdapter/Services/World/MapRenderMutex.cs)
  - 修改：[`NextBotAdapter/Services/World/MapImageService.cs`](NextBotAdapter/Services/World/MapImageService.cs)
  - 修改：[`NextBotAdapter/Services/World/PlayerMapImageService.cs`](NextBotAdapter/Services/World/PlayerMapImageService.cs)
  - 修改：[`NextBotAdapter/Services/World/MapFileService.cs`](NextBotAdapter/Services/World/MapFileService.cs)
  - 新增：[`NextBotAdapter.Tests/MapRenderMutexTests.cs`](NextBotAdapter.Tests/MapRenderMutexTests.cs)
- 不要 `lock(Main.Map)` —— `Main.Map` 字段本身被赋值会变，monitor 跟着变，锁不住
- 不要 `lock(typeof(MapImageService))` —— 锁 Type 是反模式；用专用 `MapRenderMutex.Lock` 更清晰
- 现有 backend spec 来源：[`.trellis/spec/backend/quality-guidelines.md`](.trellis/spec/backend/quality-guidelines.md)
