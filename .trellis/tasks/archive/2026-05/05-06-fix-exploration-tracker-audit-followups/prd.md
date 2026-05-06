# fix: 玩家探索 tracker race 与缓存遗漏（audit follow-ups #1/#2/#3/#5）

## Goal

修复 code review 发现的 4 个真实问题，以提高 `PlayerExplorationTracker` 的并发正确性、性能（leaderboard 大账号集 IO）以及跨会话状态一致性：

1. **#1 race**：`MarkArea` / `MarkAtPosition` 与 `Load` 之间的两段式 lock，导致 stamp 写入孤儿 BitArray
2. **#2 Load 无条件覆写**：`Load` 直接覆盖 `_bitmaps[name]`，可能抹掉懒加载路径已写入的内存数据
3. **#3 Leaderboard 无负缓存**：每次 leaderboard 请求对所有"无 bitmap 文件"账号重复磁盘探测
4. **#5 `_lastSamples` 跨会话残留**：玩家非正常掉线时残留的"上次位置"会让下次会话的第一个 stamp 画一条错位插值线

外部 REST 行为（路由 / 响应字段 / 状态码 / 错误文案 / 日志字段命名）零变化；现有 282 测试全部通过。

## What I already know

### 现状（from audit `af047e0c3...`）

#### #1 race 路径

`MarkArea`（约 line 49-67）/ `MarkAtPosition`（约 line 74-125）当前结构：

```csharp
var bitmap = GetOrCreateBitmap(accountName, width, height);  // 第一段 lock + 释放
lock (_lock)
{
    MarkBox(bitmap, width, height, ...);                     // 第二段 lock，写已捕获引用
}
```

如果两段 lock 之间另一线程 `Load` 把 `_bitmaps[name]` 替换为新 BitArray，`MarkBox` 写入孤儿引用，stamp 静默丢失。

#### #2 Load 覆写路径

`Load`（约 line 220-237）当前结构：

```csharp
var bitmap = _storage.Load(accountName, expectedBitCount);
if (bitmap is null) return;
lock (_lock)
{
    _bitmaps[accountName] = bitmap;   // 无条件覆盖
}
```

如果懒加载路径（`GetBitmap`）已经把 in-memory bitmap 放进字典，`Load` 会无条件覆盖，丢失任何 stamp 累积。

#### #3 Leaderboard 负缓存缺失

`GetBitmap`（约 line 145-188）cache miss 时无条件调 `_storage.Load`：

```csharp
var loaded = _storage.Load(accountName, width * height);
if (loaded is null) return null;     // 直接返回，没记录"已查过"
```

导致 leaderboard 多次请求会对每个无 bitmap 文件的账号重复进行磁盘探测。规模为 N×（每账号 1 次 IO 探测）。

#### #5 `_lastSamples` 跨会话残留

`OnServerLeave` 调 `ForgetLastSample`，但**非正常掉线**（网络超时 / 服务端崩溃恢复 / 客户端强杀）下钩子可能不触发。下次会话 `MarkAtPosition` 第一个 stamp 触发插值，把上次位置当起点画一条插值线。

### 关键不变量（fix 不能破坏）

- `_bitmaps` 的 BitArray 引用一旦写入，**stamp / GetBitmap / Save 必须看到同一个引用**——保证 stamp 不丢、Save 写到正确数据
- `GetBitmap` 返回**防御性拷贝**（render 线程可以慢慢迭代不被 stamp race）
- `MarkRenderMutex.Lock` 与 `_lock` **单向获取**——已验证无死锁，不能引入新 lock 顺序
- 旧文件 / 缺失文件 / 损坏文件场景下退化为 0% / 全黑（与现状一致）

## Requirements

### #1 + #2 合并修复（atomic stamp + Load 不覆盖）

- `MarkArea` / `MarkAtPosition` 改成"在单个 `_lock` 内**原子地** 取/创 bitmap 并 stamp"
- `GetOrCreateBitmap` 改成 lock-free（即 `MarkArea` / `MarkAtPosition` 内部内联 `TryGetValue` / `new BitArray` / `_bitmaps[name] = ...`），或者改成"必须由调用者持 `_lock`"语义并加注释
- `Load` 改成"only insert if not already present"——若 `_bitmaps` 已有 entry（来自懒加载或之前的 stamp），保留现有内存数据，不被覆盖

### #3 negative cache

- 在 `PlayerExplorationTracker` 加 `private readonly HashSet<string> _missingFiles = new(StringComparer.Ordinal);`，与 `_bitmaps` / `_lastSamples` 共用 `_lock` 保护
- `GetBitmap` cache miss 流程：先看 `_missingFiles` 是否命中（命中 → return null，不再做 IO）；否则调 `_storage.Load`，若返回 null 则 `_missingFiles.Add(name)`
- **不需要在 stamp 路径主动 remove**——`GetBitmap` 永远先查 `_bitmaps` 命中（被 stamp 之后已写入），`_missingFiles` 命中分支被自然绕开。但作为 defensive 习惯，stamp 路径写入 `_bitmaps` 时**顺手 `_missingFiles.Remove(name)`**，提高可读性 & 阻断"如果将来加了别的 _bitmaps 移除路径"留下的隐含 bug
- `Load` 成功（写入 `_bitmaps`）时也 `_missingFiles.Remove(name)`，保持对称

### #5 跨会话清理 `_lastSamples`

- 由 `NextBotAdapterPlugin.OnPlayerPostLogin` **在 `Load(accountName)` 之后**额外调用 `_playerExplorationTracker?.ForgetLastSample(accountName)`——把"新会话开始"语义放在 plugin 层（semantic 上每次登录都视为新会话）

### 不变项

- 公开接口签名：`IPlayerExplorationTracker` / `IExplorationStorage` 不变
- REST 路由 / 响应字段 / 状态码 / 错误文案 / 日志字段命名：均不变
- bitmap 持久化路径 / 文件格式：不变
- `MapRenderMutex.Lock` / `MapImageService` / `PlayerMapImageService` / `MapFileService`：不动
- `OnPlayerUpdate` / `OnServerLeave` / `Dispose`：不动
- `MapExplorationLeaderboardService` / `LeaderboardEndpoints`：不动（受益于 #3 的内部优化即可，无需改外部代码）

## Acceptance Criteria

- [ ] **#1**：`MarkArea` / `MarkAtPosition` 与 `Load` 并发时，stamp 永远落到 `_bitmaps[name]` 当前持有的 BitArray 上（不写入孤儿）。新增并发回归测试。
- [ ] **#2**：`Load` 在 `_bitmaps` 已有 entry 时**不覆写**（保留 in-memory）。新增测试用例验证：先 stamp（写入 in-memory bitmap）→ 再调 `Load` → bitmap 仍包含 stamp 痕迹（来自 in-memory 的位）。
- [ ] **#3**：负缓存生效——同一无 bitmap 文件账号的连续 `GetBitmap` 调用，第二次起 storage `Load` 不再被调用（用 SpyStorage 计数）。新增至少 1 条测试。
- [ ] **#3 副作用回归**：玩家从无 bitmap → 加入 + 走动 → 进入内存 bitmap → 后续 `GetBitmap` 命中内存（不被负缓存阻挡）。新增至少 1 条端到端测试。
- [ ] **#5**：登录时 `_lastSamples` 中该账号的旧条目被清除——下次 `MarkAtPosition` 不会从旧坐标插值。新增测试：模拟"残留 lastSample → OnPlayerPostLogin → MarkAtPosition" → 验证不再画插值线。
- [ ] 现有 282 测试全部通过；新增至少 4-5 条测试
- [ ] `dotnet build` 0 警告 0 错误
- [ ] 不改外部 REST / 日志 / 持久化契约

## Definition of Done

- 所有测试 green
- 行为契约（外部 REST、日志字段、持久化）零变化
- spec 合规（quality / database / error-handling / logging guidelines）

## Technical Approach

### #1 + #2 合并修：`PlayerExplorationTracker.cs`

把 `GetOrCreateBitmap` 改成"必须由调用者持 `_lock`"（重命名为私有 `GetOrCreateBitmapLocked`，加注释），`MarkArea` / `MarkAtPosition` 整个流程在单个 `_lock` 内：

```csharp
public void MarkArea(string accountName, int tileX, int tileY)
{
    if (string.IsNullOrWhiteSpace(accountName)) return;
    var (width, height) = _worldSizeProvider();
    if (width <= 0 || height <= 0) return;

    lock (_lock)
    {
        var bitmap = GetOrCreateBitmapLocked(accountName, width, height);
        MarkBox(bitmap, width, height, tileX, tileY);
    }
}

public void MarkAtPosition(string accountName, int tileX, int tileY)
{
    if (string.IsNullOrWhiteSpace(accountName)) return;
    var (width, height) = _worldSizeProvider();
    if (width <= 0 || height <= 0) return;

    lock (_lock)
    {
        var bitmap = GetOrCreateBitmapLocked(accountName, width, height);
        // ... existing 距离 / 瞬移 / 插值逻辑（保持不变） ...
    }
}

// 调用者必须已经持有 _lock。
private BitArray GetOrCreateBitmapLocked(string accountName, int width, int height)
{
    if (_bitmaps.TryGetValue(accountName, out var existing)) return existing;
    var created = new BitArray(width * height);
    _bitmaps[accountName] = created;
    _missingFiles.Remove(accountName);   // 同步清负缓存（#3 的 defensive）
    return created;
}
```

`Load` 改成"只在不存在时插入"：

```csharp
public void Load(string accountName)
{
    if (string.IsNullOrWhiteSpace(accountName)) return;
    var (width, height) = _worldSizeProvider();
    if (width <= 0 || height <= 0) return;

    var expectedBitCount = width * height;
    var bitmap = _storage.Load(accountName, expectedBitCount);
    if (bitmap is null) return;

    lock (_lock)
    {
        if (_bitmaps.ContainsKey(accountName))
        {
            // 内存里已经有数据（懒加载 / stamp 路径已建立），保留 in-memory 不覆写
            return;
        }
        _bitmaps[accountName] = bitmap;
        _missingFiles.Remove(accountName);
    }

    PluginLogger.Info($"加载玩家探索数据成功，accountName={accountName}");
}
```

### #3 negative cache：`PlayerExplorationTracker.cs`

```csharp
private readonly HashSet<string> _missingFiles = new(StringComparer.Ordinal);

public BitArray? GetBitmap(string accountName)
{
    if (string.IsNullOrWhiteSpace(accountName)) return null;

    lock (_lock)
    {
        if (_bitmaps.TryGetValue(accountName, out var bitmap)) return new BitArray(bitmap);
        if (_missingFiles.Contains(accountName)) return null;     // ← 负缓存命中
    }

    // ... 既有的 lazy-load IO 路径 ...

    var loaded = _storage.Load(accountName, width * height);

    lock (_lock)
    {
        if (_bitmaps.TryGetValue(accountName, out var existing))
        {
            return new BitArray(existing);
        }
        if (loaded is null)
        {
            _missingFiles.Add(accountName);                       // ← 记录"已查过且无文件"
            return null;
        }
        _bitmaps[accountName] = loaded;
        _missingFiles.Remove(accountName);                        // 对称
        return new BitArray(loaded);
    }
}
```

### #5 跨会话清理：`NextBotAdapterPlugin.cs:OnPlayerPostLogin`

```csharp
private void OnPlayerPostLogin(PlayerPostLoginEventArgs args)
{
    _onlineTimeService?.StartSession(args.Player.Account.Name);
    var accountName = args.Player?.Account?.Name;
    if (!string.IsNullOrEmpty(accountName))
    {
        _playerExplorationTracker?.Load(accountName);
        _playerExplorationTracker?.ForgetLastSample(accountName);   // ← 新增：每次登录视为新会话
    }
}
```

### 测试

`PlayerExplorationTrackerTests.cs` 新增（建议至少 5 条）：

1. **#1 race**：构造一个 SlowSpyStorage（`Load` 内部 `Thread.Sleep` 几 ms），让一个 thread 调 `tracker.Load(name)`；另一个 thread 在 race window 里 `MarkArea(name, ...)` → 验证 stamp **落在最终的 in-memory bitmap 上**（不被孤儿吞掉）。可以用同步原语或主动制造 race 而不是依赖时序。**Acceptable alternative**：直接调用内部状态（如反射 `_bitmaps`）模拟"stamp 之后 Load 不应覆写"，把 race 简化成顺序契约测试。
2. **#2 Load 不覆写 in-memory**：调用顺序 `MarkArea(name, x, y)`（写 in-memory）→ `Load(name)`（storage 里有不同的 bitmap）→ 验证 in-memory bitmap **保留** stamp 后的状态（而不是被 storage 版本覆盖）。
3. **#3 negative cache 生效**：`GetBitmap(unknown)` 第一次 → SpyStorage `LoadCallCount == 1`；第二次 `GetBitmap(unknown)` → `LoadCallCount` 仍 == 1（不再触发 IO）。
4. **#3 副作用回归**：`GetBitmap(unknown) → null`（注册进负缓存）→ `MarkAtPosition(unknown, x, y)` → in-memory bitmap 创建 → `GetBitmap(unknown)` 应返回非空（命中内存而非被负缓存阻挡）。
5. **#5 OnPlayerPostLogin 清理**：单测在 plugin 层做太重，可以**直接测 tracker**：先 `MarkAtPosition(name, x1, y1)` 让 `_lastSamples[name] = (x1, y1)` → 调 `ForgetLastSample(name)` → 再调 `MarkAtPosition(name, x2, y2)`（远点）→ 验证 stamp 不画插值线（只 stamp x2/y2 周围的 reveal box，没在 x1/y1 与 x2/y2 之间画线）。
   - 或者新增对 plugin 钩子的接线测试（取决于 NextBotAdapterPlugin 是否易于单测；现状是 `[ExcludeFromCodeCoverage]`，不强求）

实际上 #5 的核心契约（"`ForgetLastSample` 后下次 `MarkAtPosition` 不画插值线"）已经被现有测试覆盖（`MarkAtPosition_AfterForgetLastSample_*` 这类——若已有，复用断言即可；若无，补一条 tracker-level 测试即可，不需要测 plugin 钩子层）。

## Decision (ADR-lite)

**Context**：审计发现 4 个真实但严重度不一的问题，归类于"内部状态一致性 / 缓存效率"，集中在 `PlayerExplorationTracker` + `OnPlayerPostLogin`。

**Decision**：4 个修复打包成单 task / 单 commit；保持外部接口与契约零变化；用单 lock + lock-free helper 重构 stamp 路径，用 conditional insert 重构 `Load`，新增 `_missingFiles` HashSet 实现负缓存，在 plugin 层每次登录主动清理 `_lastSamples`。

**Consequences**：
- 优点：消除真实并发 bug；消除潜伏 bug 的隐含假设（"OnPlayerUpdate 是唯一 stamp 入口"）；leaderboard 性能在大账号集场景下改善 N→1 的 IO 量级；跨会话状态一致性
- 缺点：新增 `_missingFiles` 字段长期内存增长（每个无文件账号 1 个 string，可忽略）；Load 不覆写在线 in-memory 数据意味着"如果磁盘文件被外部更新，进程内不会感知"——这本来就是现有约定（外部修改 .bin 文件不在支持范围）
- 待评估：未来如果引入"reload 命令" / "force refresh from disk"需求，可单独加一个 `Load(accountName, force: true)` overload；本任务不做

## Out of Scope

- 不改 `IExplorationStorage` / `FileExplorationStorage`
- 不改 `MapExplorationLeaderboardService`（自动受益于 #3）
- 不改 REST 路由 / 响应字段 / 状态码 / 错误文案 / 日志字段
- 不改 `MapImageService` / `MapFileService` / `PlayerMapImageService` / `MapRenderMutex`
- 不改 `OnPlayerUpdate` / `OnServerLeave` / `Dispose`
- 不改持久化路径 / 文件格式 / 文件名清洗
- 不引入 leaderboard 端缓存（PRD 的修复是 tracker 层负缓存，不是端点层结果缓存）
- 不修审计报告里 #4（Windows 保留名）/ #6（trimmedUser 命名）/ #7（损坏文件 magic byte）/ #8（DateTimeOffset.Now）/ #9（隐含约定文档化）——单独立任务或 future evolution

## Technical Notes

### 涉及文件

- 产品代码：
  - `NextBotAdapter/Services/Exploration/PlayerExplorationTracker.cs`（核心修复 #1/#2/#3）
  - `NextBotAdapter/Plugin/NextBotAdapterPlugin.cs`（OnPlayerPostLogin 加 ForgetLastSample 一行）

- 测试：
  - `NextBotAdapter.Tests/PlayerExplorationTrackerTests.cs`（新增 4-5 条）

### 不需要改

- `IPlayerExplorationTracker.cs`（接口签名不变）
- `IExplorationStorage.cs` / `FileExplorationStorage.cs`
- `UserEndpoints.cs` / `UserInfoService.cs` / `UserInfoResponse.cs`
- `LeaderboardEndpoints.cs` / `MapExplorationLeaderboardService.cs`
- `MapImageService.cs` / `PlayerMapImageService.cs` / `MapFileService.cs` / `MapRenderMutex.cs`
- 路由 / 权限常量 / 注册器
- `docs/REST_API.md`（外部行为不变）

### 并发模型确认

- `_lock` 持有期间允许：dict 操作（`_bitmaps` / `_lastSamples` / `_missingFiles`）+ `MarkBox` 循环 + 局部计算（距离、插值）
- `_lock` **不在** 持有期间做：磁盘 IO（`_storage.Load` 在 lock 外）、PNG 编码、网络 IO
- 与 `MapRenderMutex.Lock` 的关系：`UserEndpoints.MapImage` 先调 `tracker.GetBitmap`（持 `_lock` 拷贝快照后释放）→ 再进 `playerService.Generate`（持 `MapRenderMutex.Lock`）。**单向**，无死锁——本次修复不动这个契约

### Future Evolution（不在本任务做）

- 审计 #4 / #6 / #7 / #8 / #9 单独立任务
- 引入 `RefreshFromDisk(accountName)` 主动 reload API
- 引入 `_bitmaps` LRU 驱逐策略（避免长生命周期进程的内存只增不减）
