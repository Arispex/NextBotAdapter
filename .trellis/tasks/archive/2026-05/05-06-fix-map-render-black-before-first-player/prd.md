# fix: 玩家视角地图在玩家本会话未登录前 REST 查询全黑（lazy-load 修复）

> 注：任务目录 slug 沿用 `fix-map-render-black-before-first-player`，但实际 bug 锁定在 `/users/{user}/map-image`（玩家视角图）的 lazy-load 路径，**不是** `/world/map-image`（vanilla 全亮图）。后者运行正常，已 out of scope。

## Goal

修复 REST `/users/{user}/map-image` 在 TShock 服务端重启后**该玩家本次重启周期内尚未登录过**时返回全黑图的问题——即使磁盘上 `Explored/{worldId}/{accountName}.bin` 已有该玩家上一轮会话持久化的探索数据。让 REST 首次查询一个 bitmap 已持久化但还没载入内存的玩家时，按需从磁盘加载并缓存。

## What I already know

### Root cause（已确诊）

`PlayerExplorationTracker._bitmaps` 是 in-memory `Dictionary<string, BitArray>`，bitmap 只在三种时机被写入：
1. 玩家登录成功 → `OnPlayerPostLogin` 钩子调 `_playerExplorationTracker.Load(accountName)` → 把磁盘 `Explored/{worldId}/{accountName}.bin` 反序列化到 `_bitmaps`
2. 玩家走路 → `OnPlayerUpdate` → `MarkAtPosition` → `GetOrCreateBitmap`（cache miss 时新建空 bitmap 写入 `_bitmaps`）
3. 上述 2 的 stamp 路径会持续 in-place mutate `_bitmaps[accountName]`

`PlayerExplorationTracker.GetBitmap(accountName)`（`PlayerExplorationTracker.cs:145-158`）只查 in-memory dict（`_bitmaps.TryGetValue(accountName, out var bitmap) ? new BitArray(bitmap) : null`），cache miss 直接返回 null。

`UserEndpoints.MapImage` 拿到 null 后调 `playerService.GenerateBlank(trimmedUser)` 返回全黑。

所以在"服务端刚重启 + 玩家本次重启后未登录 + 磁盘 bitmap 文件存在"这个三件齐全的窗口里，REST 永远拿不到磁盘数据。**只要玩家登录一次（触发 `Load`），bitmap 进内存，后续 REST 就正常**。

### 用户复现步骤（已确认）

1. 重启 TShock 服务端
2. 立即请求 `/users/A/map-image`（A 之前已探索过大量区域，磁盘有 `A.bin`）
3. 得到全黑（不符合预期）
4. A 玩家在客户端加入服务器**并完成 TShock 账号登录**
5. 再次请求 `/users/A/map-image`
6. 得到正常图（符合预期）

### 影响范围

- `/users/{user}/map-image` ：受影响（bug 主体）
- `/world/map-image`：不受影响（不读 bitmap，走 vanilla 全亮渲染）
- `/world/map/file`：不受影响（同上）

## Decision (ADR-lite)

**Context**：bitmap 是 lazy-loaded（仅在玩家登录时载入内存），但 REST 查询不会触发 lazy-load，导致冷启动后该玩家未登录的窗口里 REST 永远全黑。

**Decision**：把 lazy-load 内聚进 `PlayerExplorationTracker.GetBitmap(accountName)`——cache miss 时主动调 `IExplorationStorage.Load(accountName, expectedBitCount)` 一次，加载成功就缓存进 `_bitmaps` 并返回拷贝；加载失败（文件不存在 / 损坏）继续返回 null（表现退化为 `GenerateBlank`，与现状一致）。

**Consequences**：
- 优点：REST 端契约从"只查内存"升级为"按需取 bitmap"，与"`/users/{user}/map-image` 应当反映该账号的最新持久化数据"的语义对齐；冷启动 → 任何账号的首次 REST 查询都能正确返回；不动 `OnPlayerPostLogin` 既有 Load 逻辑（双方写入同一个 dict、并发安全）
- 缺点：每个账号首次 REST 查询多一次磁盘 IO（一次性，命中后缓存常驻直到进程退出）；若该账号 bitmap 文件不存在则每次 REST 仍会 try 一次（成本极低，OS 文件系统缓存命中）
- 未做：不预加载所有 bitmap（启动时间 + 内存代价不划算，多数玩家 bitmap 永远不会被查询）

## Requirements

- `PlayerExplorationTracker.GetBitmap(accountName)` 在 in-memory dict cache miss 时，主动从 `_storage.Load(accountName, width * height)` 取一次；成功则缓存进 `_bitmaps` 并返回快照拷贝；失败返回 null。
- 加载成功后，**后续的 stamp（`MarkAtPosition` / `MarkArea`）必须看到这份内存 bitmap**，不能让 lazy-load 路径与 `OnPlayerPostLogin` 的 Load 路径产生两个独立的 BitArray 实例（dict key 一致即可，已天然满足）。
- 并发安全：`GetBitmap` 在 `_lock` 保护下检查 dict + 读取 / 写入；磁盘 IO 不在 lock 内（避免阻塞其他 stamp 调用）；写入 dict 时使用 double-check 模式。
- `_worldSizeProvider()` 返回 `<= 0` 时（世界尚未载入），`GetBitmap` lazy-load 应当退化为 return null，不抛异常。
- 不改变其他公开行为：`Load(accountName)`、`Save(accountName)`、`SaveAll`、`MarkAtPosition`、`ForgetLastSample` 全保持现状语义。
- 不改 REST 路由 / 响应字段 / 错误码 / 错误文案。

## Acceptance Criteria

- [ ] **Bug 修复**：服务端冷启动 + 该玩家未登录的窗口下，调 `/users/A/map-image`（A 在之前会话已持久化 bitmap 文件）返回的 PNG 与 A 登录后的输出 pixel-by-pixel 一致。
- [ ] **No regression**：玩家未登录且无持久化文件 → REST 返回 GenerateBlank（与现状一致）。
- [ ] **No regression**：玩家在线时 stamp 行为不变（MarkAtPosition / 持续走路 / 离线时 Save 路径）。
- [ ] **Test**：新增至少 2 条单元测试覆盖：
  1. `GetBitmap` cache miss + storage 有数据 → 返回该数据，且第二次 `GetBitmap` 不再读 storage（验证缓存写回）
  2. `GetBitmap` cache miss + storage 无数据 → 返回 null（行为对齐 `GenerateBlank` 路径）
- [ ] 现有 264 个测试全部通过。

## Definition of Done

- `dotnet build` 0 警告 0 错误
- `dotnet test` 全部通过（现有 + 新增）
- 不改外部 REST 行为（路由 / 响应字段 / 状态码 / 错误文案）
- 不引入新文件（修改现有 `PlayerExplorationTracker.cs` 即可）

## Technical Approach

修改 `PlayerExplorationTracker.GetBitmap(accountName)`：

```csharp
public BitArray? GetBitmap(string accountName)
{
    if (string.IsNullOrWhiteSpace(accountName)) return null;

    lock (_lock)
    {
        if (_bitmaps.TryGetValue(accountName, out var bitmap))
        {
            return new BitArray(bitmap);
        }
    }

    // Cache miss: try lazy-load from disk so REST queries can return the latest
    // persisted bitmap even before the player has logged in this session.
    var (width, height) = _worldSizeProvider();
    if (width <= 0 || height <= 0) return null;

    var loaded = _storage.Load(accountName, width * height);
    if (loaded is null) return null;

    lock (_lock)
    {
        // Double-check: another thread (e.g. OnPlayerPostLogin) may have loaded
        // the bitmap between our two locks. Honor whichever instance is already
        // in the dictionary so stamps and snapshots see the same BitArray.
        if (_bitmaps.TryGetValue(accountName, out var existing))
        {
            return new BitArray(existing);
        }
        _bitmaps[accountName] = loaded;
        return new BitArray(loaded);
    }
}
```

测试侧面：
- `PlayerExplorationTrackerTests.cs` 中新增 2 条测试（用现有 `InMemoryStorage` fake，预先 `Save` 一份 bitmap 到 storage，然后调 `GetBitmap` 验证 lazy-load 命中）
- 验证"二次调用不再读 storage" 可以用一个 Spy storage 计数 `Load` 被调用的次数

## Out of Scope

- 不改 `/world/map-image` / `/world/map/file`（不受 bug 影响）
- 不改 reveal box / 插值 / 瞬移阈值 / stamp 逻辑
- 不预加载所有 bitmap（启动时不遍历 `Explored/{worldId}/`）
- 不引入 negative cache（针对"文件确认不存在"的玩家不设标记，下次 REST 还会 try Load 一次——成本极低）
- 不改持久化目录结构 / 文件命名 / 文件格式
- 不改 `OnPlayerPostLogin` 的 Load 路径（它仍然是登录时主动预热）
- 不改 `IExplorationStorage` 接口

## Technical Notes

### 涉及文件

- 产品代码：
  - `NextBotAdapter/Services/Exploration/PlayerExplorationTracker.cs`（仅 `GetBitmap` 方法主体改造）
- 测试：
  - `NextBotAdapter.Tests/PlayerExplorationTrackerTests.cs`（新增 2 条测试 + 可能引入 `SpyStorage` 用于计数）

### 不需要改

- `IPlayerExplorationTracker.cs`（公开接口签名不变）
- `IExplorationStorage.cs` / `FileExplorationStorage.cs`（不变）
- `UserEndpoints.cs` / 路由 / 权限常量（不变）
- `NextBotAdapterPlugin.cs`（事件钩子不变）
- `docs/REST_API.md`（外部行为不变）

### 并发模型

- `GetBitmap` 已经在 `_lock` 保护下做"复制并返回 BitArray 快照"——保留这个不变量
- IO 在 lock 外执行，避免渲染并发阻塞
- 写入 dict 用 double-check 避免 race（同一玩家的 OnPlayerPostLogin Load 与 REST GetBitmap 并发）

### Future Evolution（不在本任务做）

- 若磁盘 bitmap 文件 IO 在生产环境出现问题（坏文件 / 部分写），可在 lazy-load 路径加 negative cache（5 分钟过期 / 进程级别）以避免重复 try
- 若需要"REST 查询时主动重新载盘"以应对外部进程改 `.bin` 的场景，可加 `RefreshFromDisk(accountName)` API
