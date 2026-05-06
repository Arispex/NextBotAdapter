# refactor: 自动保存日志移到 plugin 层，去掉每子系统计数

## Goal

`PlayerExplorationTracker.SaveAll` 内当前打的日志 `"自动保存完成，成功=N"` 是定时刷盘的唯一可见信号——但 `N` 只是 bitmap 子系统的"本周期 dirty 账号"数量，**不**反映同周期 `OnlineTimeService.Flush` 是否跑过；用户在 AFK 场景看到 `成功=0` 容易误以为系统没在工作。且未来若再加更多自动保存对象，这条日志的"成功 N"语义会越来越窄。

把日志从 `PlayerExplorationTracker.SaveAll` 内部移到 plugin 层的定时回调里，去掉计数，仅打"自动保存完成"——把"自动保存"的统一语义留给 plugin 层管理。

## What I already know

### 现状（commit `376c0dc` 之后）

`PlayerExplorationTracker.SaveAll(string contextLabel = "保存")` 末尾内部打：

```csharp
if (failure > 0)
    PluginLogger.Warn($"{contextLabel}完成，成功={success}，失败={failure}");
else if (success > 0)
    PluginLogger.Info($"{contextLabel}完成，成功={success}");
// success == 0 && failure == 0：无脏数据，不打
```

定时回调流程：

```csharp
private void OnPersistenceTimerTick(object? state)
{
    try
    {
        _onlineTimeService?.Flush();              // ← 无日志、无计数
        _playerExplorationTracker?.SaveAll("自动保存");   // ← 输出 "自动保存完成，成功=N"
    }
    catch (Exception ex)
    {
        PluginLogger.Warn($"定时持久化任务异常，原因：{ex.Message}");
    }
}
```

`Dispose` 内类似调 `SaveAll("关机保存")`。

### 关键观察

- `Flush` 把 `OnlineTime.json` 整文件重写一次（无论几人在线），无 per-account 计数
- `SaveAll` 仅写 `_dirty` 子集 bitmap 文件，per-account 计数 `success`
- 两者计数语义不一致——不该混在一条日志的 N 里
- 失败诊断已经在 `FileExplorationStorage.Save` 的 Error 日志里逐账号给出（`"保存玩家探索数据失败，accountName=X，原因=..."`），无需在聚合层重复

### 用户拍板方向

- 去掉 `SaveAll` 内部的成功/失败聚合日志（包括 `成功=N` 计数）
- 把"自动保存完成"统一日志放到 plugin 层定时回调
- 失败诊断保持依赖 storage 层 Error 日志（per-account），不在聚合层加 WARN
- 不需要给 Dispose 关机路径单独加 log（保持 silent，shutdown 时其他生命周期日志已足够）

## Requirements

### `PlayerExplorationTracker.SaveAll`

- 移除内部 INFO `成功=N` 日志和 WARN `成功=N，失败=M` 日志
- 移除 `contextLabel` 参数（不再需要）→ 接口和实现签名都还原成 `void SaveAll()`
- 保留所有持久化业务逻辑：dirty 子集快照 + 写盘 + 失败重打脏 + 失败 / 成功计数（如有内部使用）——本次仅去日志
- 由于不再 emit 日志，内部不需要再维护 `success` / `failure` / `failedNames` 这些用于日志的局部变量（除 failedNames 仍用于"失败重打脏"逻辑——保留）

### `NextBotAdapterPlugin.OnPersistenceTimerTick`

- 在 `Flush + SaveAll` 跑完后追加：`PluginLogger.Info("自动保存完成。")`
- 异常路径仍走现有 catch + WARN（不变）

### `NextBotAdapterPlugin.Dispose`

- 把 `SaveAll("关机保存")` 改回 `SaveAll()`（无参数）
- **不**新增任何日志（关机路径其他生命周期日志已足够）

### 测试 fake 跟改

- 所有实现 `IPlayerExplorationTracker` 的 fake（`FakeExplorationTracker` 在 `UserEndpointsTests.cs` / `MapExplorationLeaderboardServiceTests.cs` / `RestEndpointLogicTests.cs` 中两个）：把 `void SaveAll(string contextLabel = "保存")` 改回 `void SaveAll()`

## Acceptance Criteria

- [ ] `PlayerExplorationTracker.SaveAll` 不再 emit 任何 INFO / WARN 日志
- [ ] `IPlayerExplorationTracker.SaveAll` 接口签名是 `void SaveAll()`（无 `contextLabel` 参数）
- [ ] 定时回调跑完后输出一条 `自动保存完成。`（INFO）
- [ ] 关机路径不输出 `关机保存完成` 类日志
- [ ] storage 层失败 Error 日志（per-account）保持不变
- [ ] 失败重打脏行为不变：fake storage 让某账号 Save 返回 false → 该账号仍在 `_dirty`，下次重试
- [ ] dirty 短路行为不变：未脏账号 `Save(name)` / `SaveAll` 仍跳过
- [ ] `dotnet build` 0 警告 0 错误
- [ ] `dotnet test` 全部通过（304 baseline，预期仍是 304）

## Definition of Done

- 所有测试 green、build 干净
- 接口签名干净（`SaveAll()` 无参数，与初始一致；`contextLabel` 临时 polish 完整撤回）
- 行为契约（外部 REST + 持久化路径 + 文件格式 + 失败重打脏 + dirty 短路）零变化

## Technical Approach

### 1. `IPlayerExplorationTracker.SaveAll` 还原签名

```csharp
void SaveAll();   // 去掉 string contextLabel = "保存" 参数
```

### 2. `PlayerExplorationTracker.SaveAll` 实现：去掉日志 + 还原签名

```csharp
public void SaveAll()
{
    Dictionary<string, BitArray> snapshot;
    lock (_lock)
    {
        snapshot = new Dictionary<string, BitArray>(_dirty.Count, StringComparer.Ordinal);
        foreach (var name in _dirty)
        {
            if (_bitmaps.TryGetValue(name, out var b))
                snapshot[name] = new BitArray(b);
        }
        _dirty.Clear();
    }

    var failedNames = new List<string>();
    foreach (var (name, bitmap) in snapshot)
    {
        if (!_storage.Save(name, bitmap))
            failedNames.Add(name);
    }

    if (failedNames.Count > 0)
    {
        lock (_lock) { foreach (var name in failedNames) _dirty.Add(name); }
    }

    // 不打日志：失败诊断由 FileExplorationStorage.Save 的 per-account Error 日志提供
}
```

去掉 `success` / `failure` 局部变量（现仅 failedNames 用于 dirty 重打）。去掉两条 `PluginLogger.Info` / `PluginLogger.Warn`。

### 3. `NextBotAdapterPlugin.OnPersistenceTimerTick`

```csharp
private void OnPersistenceTimerTick(object? state)
{
    try
    {
        _onlineTimeService?.Flush();
        _playerExplorationTracker?.SaveAll();   // ← 不再传 label
        PluginLogger.Info("自动保存完成。");      // ← 新增汇总日志
    }
    catch (Exception ex)
    {
        PluginLogger.Warn($"定时持久化任务异常，原因：{ex.Message}");
    }
}
```

### 4. `NextBotAdapterPlugin.Dispose`

```csharp
_playerExplorationTracker?.SaveAll();   // ← 不再传 "关机保存"，不再加日志
```

### 5. 测试 fake 还原

`UserEndpointsTests.cs` / `MapExplorationLeaderboardServiceTests.cs` / `RestEndpointLogicTests.cs`：fake 上的 `void SaveAll(string contextLabel = "保存") { }` 改回 `void SaveAll() { }`。

现有 `PlayerExplorationTrackerTests.cs` 测试断言基本无关计数（断言的是 storage 层 `SaveCallCount` / `_dirty` 状态间接行为），应当继续 pass；如有断言日志输出的（不太可能），删掉对应断言。

## Decision (ADR-lite)

**Context**：`SaveAll` 的内部聚合日志 `成功=N` 把"bitmap 子系统的本周期 dirty 数"暴露成"自动保存的总成功数"，与 `OnlineTimeService.Flush` 持久化的 fact 脱节，且未来扩展自动保存对象时这条日志会越来越窄。

**Decision**：把"自动保存完成"语义统一在 plugin 层（定时回调）一条 INFO 日志收尾；`SaveAll` 内部不再打日志，失败诊断走 storage 层 per-account Error 日志。

**Consequences**：
- 优点：plugin 层日志反映用户视角的整体语义；未来加 `OtherService.Flush()` 不需要改日志位置；接口签名干净（`SaveAll()` 无参数）；失败仍可追溯（Error 日志有 accountName）
- 缺点：失去"本周期成功了 N 个 bitmap"的快速观察指标——但这个指标的语义本来就有歧义；如果将来真的需要更详细可观测性，可以在 plugin 层聚合每个子系统的 metrics（不是日志）
- 待评估：未来若希望"自动保存日志包含所有子系统的 metrics"，可考虑给 `Flush` / `SaveAll` 都改成返回 `(success, failure)` 的 tuple，plugin 层组装；本任务不做

## Out of Scope

- 不改 `OnlineTimeService.Flush` 行为（仍然不打日志，不返回计数）
- 不改 `FileExplorationStorage.Save` 的 per-account Error 日志
- 不改业务逻辑：dirty 短路 / 失败重打脏 / Flush 累积逻辑全部不变
- 不改并发模型：`_lock` 持有范围、IO 在 lock 外的约定不变
- 不改外部 REST / 路由 / 字段 / 持久化路径 / 文件格式
- 不引入新的 metric 系统 / 子系统计数聚合层
- 不动 `docs/REST_API.md` / `docs/CONFIGURATION.md`

## Technical Notes

### 涉及文件

- 产品代码：
  - `NextBotAdapter/Services/Exploration/IPlayerExplorationTracker.cs`（接口签名还原）
  - `NextBotAdapter/Services/Exploration/PlayerExplorationTracker.cs`（去日志 + 签名还原）
  - `NextBotAdapter/Plugin/NextBotAdapterPlugin.cs`（定时回调加日志，Dispose 调用还原）
- 测试 fake：
  - `NextBotAdapter.Tests/UserEndpointsTests.cs`
  - `NextBotAdapter.Tests/MapExplorationLeaderboardServiceTests.cs`
  - `NextBotAdapter.Tests/RestEndpointLogicTests.cs`

### 不需要改

- `IExplorationStorage.cs` / `FileExplorationStorage.cs`
- `OnlineTimeService.cs`
- 路由 / 权限 / 注册器
- 渲染服务 / leaderboard / MapRenderMutex
- `docs/`

### Future Evolution（不在本任务做）

- 如需更详细的"自动保存可观测性"，可让 `IOnlineTimeService.Flush` / `IPlayerExplorationTracker.SaveAll` 返回 `(success, failure)` tuple，由 plugin 层聚合后输出统一 metrics 日志（如 `自动保存完成。在线时长=X，玩家探索=Y/Z`）
