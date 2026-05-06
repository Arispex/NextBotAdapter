# feat: 定时增量刷盘 + bitmap 脏标记，避免断电丢失会话数据

## Goal

给 `PlayerExplorationTracker`（玩家探索 bitmap）和 `OnlineTimeService`（玩家在线时长）这两个**只在玩家正常下线 / 服务端 Dispose 时**才落盘的内存状态，加一个**后台定时刷盘**机制——突发断电 / 死机 / 强杀场景下，最多丢失上一次刷盘到崩溃之间（默认 5 分钟）的数据。配套加 bitmap 脏标记，避免每次刷盘把所有 in-memory bitmap 全量重写（典型场景下 99% 是无变化的浪费 IO）。

## What I already know

### 当前问题（用户提出）

| 数据 | 当前保存触发 | 突发断电丢失范围 |
|---|---|---|
| 玩家探索 bitmap（`PlayerExplorationTracker`） | `OnServerLeave` 单玩家下线 / `Dispose` 关机 | 所有在线玩家本会话累积的 stamp 全丢 |
| 在线时长（`OnlineTimeService`） | `EndSession`（玩家正常下线）/ `Dispose` 关机 | 所有 `_activeSessions` 中未结算的秒数全丢 |
| 白名单 / 黑名单 | 每次 add/remove 立刻 Save | 不丢（write-through） |

### 现有实现要点

#### `OnlineTimeService`（`Services/UserData/OnlineTimeService.cs`）

```csharp
// _records: Dictionary<string, long> - 已持久化累积秒数
// _activeSessions: Dictionary<string, DateTime> - 当前会话起点（UTC）

// EndSession(name) 路径：
_records[name] += (utcNow - _activeSessions[name]).TotalSeconds;
_activeSessions.Remove(name);
PersistLocked();   // 持久化 _records → JSON 文件

// PersistAllSessions() 路径（Dispose 时）：
foreach (var (name, start) in _activeSessions)
    _records[name] += (utcNow - start).TotalSeconds;
_activeSessions.Clear();    // ← 注意：会清空 active sessions
PersistLocked();
```

**关键观察**：`PersistAllSessions` 会**清空** `_activeSessions`——只适合关机场景。**定时刷盘**期间玩家仍在线，需要一个新方法 `Flush()`：累积差量到 `_records` + 把 `_activeSessions[name]` 起点重置到 `UtcNow`（避免下次 `EndSession` 双计），但**不**清空 `_activeSessions`。

#### `PlayerExplorationTracker`

```csharp
// _bitmaps: Dictionary<string, BitArray> - 玩家探索位图
// SaveAll() 路径（Dispose 时）：
//   lock 内拷贝整个 _bitmaps → snapshot
//   lock 外串行 _storage.Save 每个账号
// 单账号 Save(name) 路径（OnServerLeave 时）：
//   lock 内 new BitArray(...) 拷贝 → lock 外 _storage.Save(name, snapshot)
```

**性能问题**：当前 `SaveAll` **无差异**，全量重写。1000 in-memory bitmap × ~2.5 MB = ~2.5 GB / 次。定时刷盘（如 5 分钟）下会成持续 IO 风暴。

### 关键不变量（fix 不能破坏）

- 现有契约：`OnServerLeave` / `Dispose` 路径行为不变（兜底刷盘仍然要可靠）
- `_lock` 内不做 IO（与现有约定一致）
- 接口 `IPlayerExplorationTracker` / `IOnlineTimeService` / `IExplorationStorage` 现有方法签名不变（只新增 `Flush` 之类的方法）
- REST 路由 / 字段 / 状态码 / 错误文案 / 日志字段命名零变化
- 持久化路径 / 文件格式不变

## Decision (ADR-lite)

**Context**：内存累积型数据（探索 bitmap、在线时长）目前仅在玩家正常下线 / 服务端 Dispose 时落盘，断电场景下整个开服周期数据丢失风险大。修复需平衡两点：
1. **兜底**：突发断电至少保留到上次刷盘
2. **性能**：定时刷盘不能在每次都全量重写大 bitmap

**Decision**：
1. 加**单一后台定时器**（`System.Threading.Timer`）在 plugin 生命周期内运行；定时回调串联 `_onlineTimeService.Flush()` + `_playerExplorationTracker.SaveAll()`
2. 在 `PlayerExplorationTracker` 加**脏标记** `HashSet<string> _dirty`：`MarkArea` / `MarkAtPosition` 在 lock 内打脏；`SaveAll` 只拷贝并写脏的；写盘成功后清脏，失败的留在 `_dirty` 下次重试；**`Save(name)` 单账号也走脏检查**——未脏则 short-circuit 不写
3. 在 `OnlineTimeService` 加新方法 `Flush()`：累积所有 `_activeSessions` 差量到 `_records`，并把每个 active session 的 start 重置到 `UtcNow`（避免下次 `EndSession` 双计），最后 `PersistLocked()`；**`_activeSessions` 不清**
4. 间隔**硬编码常量** `TimeSpan.FromMinutes(5)`，可读但不可配；将来如果有需要再做 config

**Consequences**：
- 优点：突发断电最多丢 5 分钟数据；脏标记把全量重写降到 O(本周期变更账号数)；与现有关机路径正交（兜底仍然可靠）；接口签名不变（新增方法不破坏向后兼容）
- 缺点：脏标记新增内存占用（每个被 stamp 过的账号一个 string，可忽略）；定时器跑在线程池工作线程上，与现有 lock 模型已有保护——无新并发风险
- 待评估：若将来需要更小 RPO（如 1 分钟），可改间隔配置化；若 IO 仍然过重可加"批量大小限流"（每个 tick 最多写 N 个账号）；本任务暂不做

## Requirements

### `OnlineTimeService` 加 Flush

- 新增 `IOnlineTimeService.Flush()` 方法 + `OnlineTimeService.Flush()` 实现
- 行为：lock 内对每个 `_activeSessions[name]`：`elapsed = (utcNow - start).TotalSeconds; _records[name] += elapsed; _activeSessions[name] = utcNow;` → `PersistLocked()`
- 不清空 `_activeSessions`
- `EndSession` / `PersistAllSessions` / `GetTotalSeconds` / `StartSession` 行为不变

### `PlayerExplorationTracker` 加脏标记

- 新增 `private readonly HashSet<string> _dirty = new(StringComparer.Ordinal)`，与 `_bitmaps` 共用 `_lock`
- `MarkArea` / `MarkAtPosition`：在 lock 内、stamp 后 `_dirty.Add(accountName)`
- `Save(string accountName)`（单账号）：
  - 在 lock 内：若 `!_dirty.Contains(accountName)` → 直接 return（无变更，无需写）
  - 否则 lock 内 `new BitArray(...)` 拷贝 + `_dirty.Remove(accountName)` → lock 外 `_storage.Save`
  - 若 `_storage.Save` 返回 false → lock 内 `_dirty.Add(accountName)` 重新打脏（下次重试）
- `SaveAll`：
  - 在 lock 内：从 `_dirty` 中筛出仍在 `_bitmaps` 中的条目；对每个生成快照 + `_dirty.Remove`
  - lock 外：串行 `_storage.Save` 每个；统计成功 / 失败
  - 失败的条目 → lock 内 `_dirty.Add` 重新打脏
  - 既有完成度日志保持（INFO 全成 / WARN 有失败 / dict 空仍不打）
- `Load` / `GetBitmap`（懒加载）从磁盘读出来塞进 `_bitmaps` 时**不**打脏（数据已与磁盘一致）

### Plugin 启动定时器

- `NextBotAdapterPlugin` 加私有字段 `private System.Threading.Timer? _persistenceTimer`
- `Initialize` 末尾启动：
  ```csharp
  var interval = TimeSpan.FromMinutes(5);
  _persistenceTimer = new Timer(OnPersistenceTimerTick, null, interval, interval);
  ```
- `Dispose(disposing=true)` 早于现有持久化调用：
  ```csharp
  _persistenceTimer?.Dispose();   // 阻塞等待回调执行完
  _persistenceTimer = null;
  ```
  然后是现有 `_onlineTimeService?.PersistAllSessions(); _playerExplorationTracker?.SaveAll();`
- 定时回调实现：
  ```csharp
  private void OnPersistenceTimerTick(object? state)
  {
      try
      {
          _onlineTimeService?.Flush();
          _playerExplorationTracker?.SaveAll();
      }
      catch (Exception ex)
      {
          PluginLogger.Warn($"定时持久化任务异常：{ex.Message}");
      }
  }
  ```
- 不引入新配置项（间隔硬编码常量），不暴露 REST 接口

## Acceptance Criteria

- [ ] **OnlineTimeService.Flush**：start session → 等几秒 → flush → 不结束 session → records 已包含累积秒数；继续等几秒 → flush → records 增量正确（无双计）；end session → records 仍包含正确累积
- [ ] **PlayerExplorationTracker dirty**：调 MarkArea(name) → `_dirty` 含 name；Save(name) → `_storage.Save` 被调一次 + `_dirty` 不再含 name；再调 Save(name) → `_storage.Save` **不**被调（已不脏）
- [ ] **PlayerExplorationTracker SaveAll dirty 过滤**：3 个账号 in-memory，只 stamp 1 个 → SaveAll → `_storage.Save` 仅被调 1 次（仅 dirty 账号）
- [ ] **失败重试**：fake storage 让某账号 Save 返回 false → SaveAll 后该账号仍在 `_dirty`；下次 SaveAll 仍会尝试
- [ ] **Plugin 定时器**：启动后 timer 不为 null；Dispose 后被释放（无泄漏）
- [ ] **Plugin Dispose 顺序**：先关 timer 再 PersistAllSessions + SaveAll（确保不会有定时回调在 Dispose 持久化期间并发）
- [ ] 现有 293 测试全部通过；新增至少 5-6 条测试
- [ ] `dotnet build` 0 警告 0 错误
- [ ] 不改 REST / 路由 / 字段 / 持久化路径 / 文件格式

## Definition of Done

- 全部测试 green、build 干净
- 现有 `EndSession` / `PersistAllSessions` / `OnServerLeave` / `Dispose` 路径行为不变
- 接口签名不变（仅在 `IOnlineTimeService` / `IPlayerExplorationTracker` 上**新增**方法 / 字段）
- spec 合规

## Technical Approach

### 1. `IOnlineTimeService.cs` + `OnlineTimeService.cs`

接口加：

```csharp
void Flush();
```

实现：

```csharp
public void Flush()
{
    lock (_lock)
    {
        var now = DateTime.UtcNow;
        foreach (var name in _activeSessions.Keys.ToList())
        {
            var start = _activeSessions[name];
            var elapsed = (long)(now - start).TotalSeconds;
            _records[name] = (_records.TryGetValue(name, out var existing) ? existing : 0) + elapsed;
            _activeSessions[name] = now;   // 重置起点，下次差量从这里算
        }
        PersistLocked();
    }
}
```

注意：`_activeSessions.Keys.ToList()` 防止"在迭代字典时修改字典"——其实只改 value 不改 key 不会触发 InvalidOperationException，但用 `ToList()` 更稳妥。

### 2. `IPlayerExplorationTracker.cs`（不变签名）+ `PlayerExplorationTracker.cs`

接口签名不动。实现内部：

加字段：
```csharp
private readonly HashSet<string> _dirty = new(StringComparer.Ordinal);
```

`MarkArea`：

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
        _dirty.Add(accountName);   // ← 打脏
    }
}
```

`MarkAtPosition`（同样在 lock 内 stamp 后 `_dirty.Add`）。

`Save(string accountName)` 单账号：

```csharp
public void Save(string accountName)
{
    if (string.IsNullOrWhiteSpace(accountName)) return;

    BitArray? snapshot;
    lock (_lock)
    {
        if (!_dirty.Contains(accountName)) return;   // 不脏跳过
        if (!_bitmaps.TryGetValue(accountName, out var bitmap)) return;
        snapshot = new BitArray(bitmap);
        _dirty.Remove(accountName);
    }

    if (!_storage.Save(accountName, snapshot))
    {
        // 写盘失败，重新打脏，下次重试
        lock (_lock) { _dirty.Add(accountName); }
    }
}
```

`SaveAll`：

```csharp
public void SaveAll()
{
    Dictionary<string, BitArray> snapshot;
    lock (_lock)
    {
        // 从 _dirty 筛出仍在 _bitmaps 中的，建快照，清脏
        snapshot = new Dictionary<string, BitArray>(_dirty.Count, StringComparer.Ordinal);
        foreach (var name in _dirty)
        {
            if (_bitmaps.TryGetValue(name, out var b))
                snapshot[name] = new BitArray(b);
        }
        _dirty.Clear();
    }

    var success = 0;
    var failure = 0;
    var failedNames = new List<string>();
    foreach (var (name, bitmap) in snapshot)
    {
        if (_storage.Save(name, bitmap)) success++;
        else { failure++; failedNames.Add(name); }
    }

    if (failedNames.Count > 0)
    {
        lock (_lock) { foreach (var name in failedNames) _dirty.Add(name); }
    }

    if (failure > 0)
        PluginLogger.Warn($"SaveAll 完成，成功={success}，失败={failure}");
    else if (success > 0)
        PluginLogger.Info($"SaveAll 完成，成功={success}");
}
```

注意：`Load` 和 `GetBitmap` 懒加载塞 `_bitmaps` 时**不**调 `_dirty.Add`（数据已与磁盘一致）。

### 3. `NextBotAdapterPlugin.cs`

加私有字段：

```csharp
private System.Threading.Timer? _persistenceTimer;
private static readonly TimeSpan PersistenceInterval = TimeSpan.FromMinutes(5);
```

`Initialize` 末尾（其他 wiring 之后）：

```csharp
_persistenceTimer = new System.Threading.Timer(OnPersistenceTimerTick, null, PersistenceInterval, PersistenceInterval);
PluginLogger.Info($"已启用定时持久化任务，间隔={PersistenceInterval.TotalSeconds} 秒。");
```

`Dispose` **早于**现有持久化逻辑：

```csharp
protected override void Dispose(bool disposing)
{
    if (disposing)
    {
        _persistenceTimer?.Dispose();
        _persistenceTimer = null;

        _onlineTimeService?.PersistAllSessions();
        _playerExplorationTracker?.SaveAll();
        // ... 既有逻辑 ...
    }
    base.Dispose(disposing);
}
```

回调方法：

```csharp
private void OnPersistenceTimerTick(object? state)
{
    try
    {
        _onlineTimeService?.Flush();
        _playerExplorationTracker?.SaveAll();
    }
    catch (Exception ex)
    {
        PluginLogger.Warn($"定时持久化任务异常，原因：{ex.Message}");
    }
}
```

### 4. 测试

`OnlineTimeServiceTests.cs`（如果有，否则在现有相关测试中加）：
- `Flush_ShouldAddElapsedToRecords_WithoutEndingSession`
- `Flush_ShouldNotDoubleCount_OnSubsequentEndSession`（start → flush → end → 总秒数等于 end - start）
- `Flush_ShouldBeIdempotent_AcrossMultipleCalls`（多次 flush 累计仍正确）

`PlayerExplorationTrackerTests.cs`：
- `MarkArea_ShouldMarkAccountDirty`（用反射 / 通过观察 Save 行为间接断言）
- `Save_ShouldSkipUnchangedAccounts`（未 stamp 的账号调 Save 不触发 storage IO）
- `SaveAll_ShouldOnlyPersistDirtyAccounts`（3 个账号、只 stamp 1 → SaveAll 后 storage `SaveCallCount == 1`）
- `SaveAll_ShouldRetainDirtyOnFailure_AndRetrySuccessfullyNextTime`（fake storage 第一次返回 false → 该账号仍 dirty；切换 fake 为 true → SaveAll 二次成功）
- `Load_ShouldNotMarkDirty`（存盘已有 + Load → 该账号不 dirty → 后续 Save 短路）

`NextBotAdapterPlugin` 钩子级测试不需要——`Plugin/` 标记 `[ExcludeFromCodeCoverage]`。可以手工 inspect 确认 timer 启动 / 停止顺序。

## Out of Scope

- 不引入定时间隔配置项（硬编码 5 分钟；future evolution 可做）
- 不引入"批量限流"（每 tick 最多 N 个账号）
- 不改持久化文件格式 / 路径 / 文件名
- 不改 REST 路由 / 端点 / 响应字段 / 状态码
- 不改 leaderboard / 渲染服务
- 不引入 WAL / 增量日志（架构变更过大，与"整张 BitArray 一次性写文件"模型不兼容）
- 不修第三轮 audit 的 SaveAll 关机延迟问题（O(N) 拷贝在 lock 内；本任务也维持现有快照模式，但因为只快照 dirty 子集而非全量，关机延迟也顺带改善）
- 不修 `_bitmaps` 长生命周期不退订（前两轮已记录为 known issue）

## Technical Notes

### 涉及文件

- 产品代码（**修改**）：
  - `NextBotAdapter/Services/UserData/IOnlineTimeService.cs`（接口加 `Flush`）
  - `NextBotAdapter/Services/UserData/OnlineTimeService.cs`（实现 `Flush`）
  - `NextBotAdapter/Services/Exploration/PlayerExplorationTracker.cs`（加 `_dirty` + 改 stamp / Save / SaveAll）
  - `NextBotAdapter/Plugin/NextBotAdapterPlugin.cs`（timer 启停 + 回调）

- 测试：
  - `NextBotAdapter.Tests/PlayerExplorationTrackerTests.cs`（新增 5 条 + 视情况调整 InMemoryStorage 已有计数器）
  - `NextBotAdapter.Tests/OnlineTimeServiceTests.cs`（如已有，新增 3 条；否则只在现有 service tests 中追加）

### 不需要改

- `IPlayerExplorationTracker.cs`（公开签名不变）
- `IExplorationStorage.cs` / `FileExplorationStorage.cs`
- 路由 / 权限 / 注册器 / endpoint
- 渲染服务 / MapRenderMutex / leaderboard
- 持久化路径 / 文件格式
- 配置 model / 配置文件 / `docs/CONFIGURATION.md`
- `docs/REST_API.md`

### 并发模型

- `Flush` 在 `OnlineTimeService._lock` 内做 dict 操作 + IO（与现有 `PersistLocked` 一致——内部 `File.WriteAllText` 在 lock 内调用，与现有 `EndSession` 行为相同）。这与 `PlayerExplorationTracker` 的"IO 不在 lock 内"约定**不**对称——但 OnlineTimeService 文件是 KB 级 JSON，IO 极快，保持现有约定不破坏既有契约。
- `PlayerExplorationTracker.SaveAll`：lock 内 snapshot + `_dirty.Clear`，lock 外 IO，失败时重新 lock 加回 `_dirty`——与现有契约一致
- 定时器回调线程池工作线程：与现有 `_lock` / `_storage` 并发模型已经天然兼容（REST 端点 worker 也走同样路径）

### Future Evolution

- 间隔配置化（可加到 `NextBotAdapterConfig` 的某个 settings section）
- 批量限流（每 tick 写 N 个账号、轮转、避免单 tick 长 IO）
- WAL / 增量日志（更小 RPO）
- bitmap 退订：玩家长期离线后从 `_bitmaps` 移除（节省内存）
