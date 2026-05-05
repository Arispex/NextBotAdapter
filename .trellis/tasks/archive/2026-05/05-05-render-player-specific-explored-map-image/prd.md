# Render player-specific explored map image

## Goal

提供一个新的 REST 渲染端点（或扩展现有端点），输出**指定玩家视角下的地图图片**——已探索区域按真实瓦片颜色显示，未探索区域显示为黑色（或透明）。让玩家在游戏外可以审视自己已经探索了多少世界、哪些洞穴 / 地下城还没去过。

## What I already know

### 现有渲染管线

- [`MapImageService.Generate()`](NextBotAdapter/Services/World/MapImageService.cs) 当前流程：
  1. `PrepareMapEnvironment()` 初始化 `Main.Map`（`WorldMap`，`_tiles[maxTilesX+Edge*2, maxTilesY+Edge*2]`）
  2. `LightUpWholeMap()` 通过 `MapTileGrid.Fill(...)` 调 `MapHelper.CreateMapTile(x, y, byte.MaxValue, 0)`，给每格世界坐标生成一个**完全点亮**的 `MapTile` 写入 `_tiles[x+Edge, y+Edge]`
  3. 逐瓦片 `MapHelper.GetMapTileXnaColor(tile)` → 写 `Image<Rgba32>`
- 之所以"全部点亮"，是因为 `byte.MaxValue` 是 light 参数；如果传 `0` 或没构造 `MapTile`，理论上对应位置应输出黑色

### `MapHelper.CreateMapTile` 调用形态

- 4 参数版（`MapImageService` 用）：`MapHelper.CreateMapTile(x, y, byte.MaxValue, 0)` —— 第 4 参数 `0` 含义未确认
- 3 参数版（`MapFileService` 用）：`MapHelper.CreateMapTile(x, y, byte.MaxValue)`
- `MapTile` 的内部字段（type / color / light / unknown？）影响 `GetMapTileXnaColor` 输出

### 玩家探索数据

- 客户端：玩家本地有 `.map` 文件存自己的 explored bitmap（每瓦片一位 light + position）
- 服务端：vanilla Terraria server **不**保存任何玩家 explored 数据；TShock 是否扩展了这块未知

### 现有 REST 路由

- 路由集中在 [`NextBotAdapter/Infrastructure/EndpointRoutes.cs`](NextBotAdapter/Infrastructure/EndpointRoutes.cs)，端点逻辑在 [`NextBotAdapter/Rest/`](NextBotAdapter/Rest/) 下
- 现有 map 相关端点：
  - `GET /world/map/image`（`MapEndpoints.Image`）—— 全图 PNG
  - `GET /world/map/file`（`WorldEndpoints.MapFile`）—— `.map` 服务端视角
- 现有按玩家筛选的端点（如用户信息）使用 query param，参考 `UserEndpoints` 风格

## Research References

- [`research/server-side-player-explored-data.md`](research/server-side-player-explored-data.md) — 服务端**无法**直接拿到玩家真实 explored bitmap（客户端独有概念），唯一可行路径是从 `PacketTypes.PlayerUpdate` 采样玩家位置，自己累积一份"模拟探索"bitmap
- [`research/maptile-semantics.md`](research/maptile-semantics.md) — `default(MapTile)` → `(0,0,0,0)` 透明；或直接写 `Rgba32(0,0,0,255)` 实心黑；`Light=0` 不是通用"未探索"开关（空格子会被 `GetBackgroundType` 强制变天空 / 矿洞色）
- [`research/rest-api-shape.md`](research/rest-api-shape.md) — 推荐 `GET /nextbot/world/map-image?player={user}` query 参数（与项目 `?reason=`/`?path=v` 惯例一致，向后兼容）

## Feasibility Conclusion

**部分可行（需重大妥协）。**

服务端**没有任何途径**拿到玩家真实的 explored bitmap——这是 vanilla Terraria 的客户端独有数据，协议不传，TShock / OTAPI 都没暴露任何 hook，整个 TShock 生态查不到先例。任何"玩家探索图"功能只能由插件自己**模拟**：

- 监听 `PacketTypes.PlayerUpdate`（~60Hz），从 `TPlayer.position` 提取 tile 坐标
- 在玩家所在 tile 周围标记一个圆盘 / 矩形（vanilla 约 41×41，建议半边 ~50）为"已探索"
- 存到 per-(UUID, world) 的 `BitArray`，渲染时作为可见性掩码

### 与真实客户端 `<player>.map` 的差距

| 场景 | 真实客户端 | 我们的模拟 |
|---|---|---|
| 走着探索 | ✅ | ✅ |
| 用钩 / 翼飞过看到 | ✅ | ✅（位置接近就标记） |
| 传送门 / 镜子 / 海螺 | ✅（短暂出现处也算探索） | ❌（位置瞬移不会扫到途中） |
| 站着看远处某段（视野内但没靠近） | ✅ | ❌ |
| 第一次启用插件之前已经探索的内容 | ✅ | ❌（无历史） |

## Decision (ADR-lite)

- **Context**：服务端拿不到玩家真实 explored 数据；用户接受"模拟探索"路线，但希望先做最小可验证版本，再决定是否保留功能
- **Decision**：方案 A——`PlayerUpdate` 位置采样 + 周边圆盘标记 + per-玩家 bitmap 持久化 + REST 端点按 bitmap 渲染掩码；同时为开发测试加一个**临时**控制台 / 游戏内命令，把生成的 PNG 直接落到磁盘文件夹，效果不满意时删除整个功能
- **Consequences**：
  - 优点：自洽于 vanilla server 限制；用户可以肉眼快速验证效果
  - 风险：模拟数据不等于真实 `<player>.map`，文档要写清楚；测试命令是 throwaway，要单独标注便于后续删除

## Requirements (MVP)

### 1. 探索追踪（核心）

- 注册 `GetDataHandlers.PlayerUpdate` event handler（参考 [TShock GetDataHandlers.cs](https://github.com/Pryaxis/TShock/blob/general-devel/TShockAPI/GetDataHandlers.cs)）
- 仅追踪**已登录到 TShock 账号**的玩家（用账号 UUID 作 key，不用 character name，避免重名 / 改名问题）
- 每次收到 `PlayerUpdate`：
  - 把 `TPlayer.position` 转 tile 坐标 `(tx, ty) = (int)(x/16, y/16)`
  - 在 `(tx, ty)` 周围 **41×41** 矩形（vanilla 地图视野尺寸；常量先 hard-code，后续可移到 config）标记为已探索
  - 边界裁剪到 `[0, maxTilesX) × [0, maxTilesY)`
- 数据结构：`Dictionary<string accountUuid, BitArray bitmap>`，bitmap 大小 = `maxTilesX * maxTilesY`，flat index `idx = y * maxTilesX + x`
- 不追踪未登录 / 客访玩家

### 2. 持久化

- 路径：`tshock/NextBotAdapter/explored/{worldId}/{accountUuid}.bin`（worldId 用 `Main.worldID`，按世界隔离）
- 格式：raw byte[]，从 `BitArray.CopyTo`，文件大小 = `ceil(maxTilesX * maxTilesY / 8)` 字节（约 2.5MB / 玩家 / 大世界，可接受）
- 保存触发：玩家退出（`ServerApi.Hooks.NetGreetPlayer` 反向事件）+ 插件 `Dispose`
- 加载触发：玩家登录到 TShock 账号成功后立即加载
- 文件不存在 / 损坏 → 视为全黑（fail-safe，新玩家天然如此）

### 3. REST 端点

- 路由复用 `GET /nextbot/world/map-image`，新增可选 query `player={accountName}`
- 不带 `player` → 现有全图行为（不破坏）
- 带 `player`：
  - 该名玩家**未注册** TShock 账号 → 400 `error = "User was not found."`（与 `/nextbot/users/{user}/inventory` 一致，参考 [docs/REST_API.md:62](docs/REST_API.md:62)）
  - 玩家**已注册**但本插件无追踪 bitmap → 200，返回**全黑**图（语义：账号存在但还没探索任何区域）
  - 玩家已注册且有追踪 bitmap → 渲染：已探索按真实瓦片色（沿用现 `MapHelper.GetMapTileXnaColor`）；未探索写 `Rgba32(0, 0, 0, 255)` 实心黑
- 响应 schema：保持 `{fileName, base64}` 不变
- 权限：复用 `nextbot.world.map_image`

### 4. 临时测试命令（throwaway，明确标注）

- 形态：游戏内 / 控制台 `/nb test-map [player]` 子命令（参考现有 `/nb` 命令注册）
- 行为：
  - 不带 `[player]`：dump 现有全图 PNG（对照基线）
  - 带 `[player]`：dump 该玩家视角图（按以上 REST 端点同款规则；玩家未注册同样直接返回错误信息到命令调用者）
- 输出路径：`tshock/NextBotAdapter/test-output/map-{player|world}-{yyyyMMdd-HHmmss}.png`
- **整段功能（命令注册 + 文件落盘）独立成单文件**（如 `Plugin/Dev/TestMapCommand.cs`），代码顶部注释 `// TEMP: dev-only command, remove before final release`，方便后续整体删除
- 权限：`nextbot.test.map`（新增），仅运维 / 自己用

## Acceptance Criteria

- [ ] 监听 `PlayerUpdate`，已登录玩家走动时其账号 UUID 对应 bitmap 被持续标记
- [ ] 玩家退出后再登录，bitmap 从磁盘恢复（不会丢失上次探索）
- [ ] `GET /nextbot/world/map-image`（无 player）行为与现在完全一致
- [ ] `GET /nextbot/world/map-image?player={user}` 返回 200 + PNG，已探索区域可见瓦片色，未探索区域纯黑
- [ ] 不存在的玩家账号返回 404 `User was not found.`
- [ ] 账号存在但无追踪数据 → 200 + 全黑 PNG
- [ ] 游戏内 `/nb test-map [player]` 把图片写到 `tshock/NextBotAdapter/test-output/`
- [ ] `dotnet build` 0 警告 0 错误，`dotnet test` 全绿
- [ ] `docs/REST_API.md` 补充 `player` 参数说明 + "模拟探索 ≠ 真实 `<player>.map`" 的限制声明

## Out of Scope (MVP)

- 多玩家视角并集（`?player=A,B`）
- 探索覆盖率统计 / 探索百分比字段
- 客户端 `<player>.map` 上传 / 同步
- 配置化探索半径（先 hard-code 41×41，后续如需要再加 config）
- 历史数据回溯（插件启用前的探索无法追溯，文档写明）
- 测试命令的长期化（命令本身是 throwaway，做完功能验证后单独 PR 删除）
- 性能 profiling（先不优化；如果 60Hz 多玩家场景下 CPU 太高，再在 follow-up 任务里加 throttle）

## Technical Approach

1. 新增 `Services/Exploration/`（新子目录）：
   - `IPlayerExplorationTracker` / `PlayerExplorationTracker` —— 内存中维护 `Dictionary<string, BitArray>`，提供 `MarkArea`、`GetBitmap`、`Save`、`Load`
   - `ExplorationStorage` —— 处理磁盘 IO，按 `(worldId, accountUuid)` 分文件
2. 在 plugin lifecycle 注册：
   - `GetDataHandlers.PlayerUpdate += OnPlayerUpdate`（位置采样）
   - `ServerApi.Hooks.NetGreetPlayer / ServerLeave` 处理登录加载 / 离线持久化
   - `Dispose` 时全量持久化
3. 修改 `MapImageService`：
   - 增加 `Generate(string accountUuid)` 重载（或新增 `IPlayerMapImageService`），渲染时传入 bitmap，未探索写黑
4. 修改 `MapEndpoints.Image`：读取 query `player`，按是否带参分发
5. 新增控制台 / 游戏内 subcommand `test-map`，文件落盘到 test-output 目录
6. 测试：
   - `PlayerExplorationTrackerTests` —— `MarkArea` 正确性、边界裁剪、save/load 往返
   - `ExplorationStorageTests` —— 文件格式、损坏文件 fail-safe
   - `MapEndpointsTests` —— `?player=X` 路由分发、404 / 200 / 全黑场景

## Out of Scope (preliminary)

- 不破坏现有 `/world/map/image` 全图行为
- 不修改 TShock 客户端 / Terraria 客户端
- 不实现"玩家探索数据导入 / 导出"独立功能（除非是实现本功能的副产物）

## Technical Notes

- 所在目录：[`NextBotAdapter/Services/World/`](NextBotAdapter/Services/World/)
- 关键 Terraria / TShock API 表面：`Terraria.Map.MapHelper`、`Terraria.Map.MapTile`、`Terraria.Map.WorldMap`、`Terraria.Main.Map._tiles`
- TSAPI 6.1.0、OTAPI（GitHub `TShock/Pryaxis`、`SignatureBeef/Open-Terraria-API`）
- 已确认通过的可拓展点：`MapTileGrid.Fill<T>` 已经把"逐格写入 `_tiles[x+Edge, y+Edge]`"抽成纯函数，新功能只需要换一个 factory（例如「探索过返回 lit tile，否则返回 unexplored tile / 跳过」）
