# fix: tracker IO 异常永久负缓存 + 钩子顺序竞态 + SaveAll 可观测性（audit round 2）

## Goal

修复第二轮 code review 发现的 4 个问题：

- **C（真实 bug）**：`_missingFiles` 把瞬时 IO 异常当成"文件不存在"永久缓存——服务器跑期间出现一次 NFS 抖动 / 磁盘瞬时不可读 / 权限被外部短暂收回，对应账号的探索数据**直到进程重启之前永远拿不到**。
- **A（罕见竞态）**：`OnPlayerPostLogin` 中 `Load → ForgetLastSample` 顺序使得"`Load` 期间 `OnPlayerUpdate` 写入的合法 `_lastSamples`"被误清。
- **B（性能 + 一致性）**：`Load(name)` null 路径不写 `_missingFiles`，与 `GetBitmap` 的负缓存路径不对称——首次 leaderboard 仍会对每个无文件账号做一次冗余 IO（依赖 C 先修，否则会扩大 C 的影响）。
- **D（可观测性）**：`SaveAll` 对单账号写盘失败无感知 / 不汇总——服务器关机时数据丢了运维不知道。

## What I already know

### 当前现状

#### `FileExplorationStorage.Load`（约 line 17-51）

```csharp
public BitArray? Load(string accountName, int expectedBitCount)
{
    if (string.IsNullOrWhiteSpace(accountName) || expectedBitCount <= 0) return null;
    var filePath = ResolveFilePath(accountName);
    if (!File.Exists(filePath)) return null;        // ← "missing" → null
    try
    {
        var bytes = File.ReadAllBytes(filePath);
        var expectedByteCount = (expectedBitCount + 7) / 8;
        if (bytes.Length != expectedByteCount)
        {
            PluginLogger.Warn(...);
            return null;                            // ← "corrupt" → null
        }
        return new BitArray(bytes) { Length = expectedBitCount };
    }
    catch (Exception ex)
    {
        PluginLogger.Warn(...);
        return null;                                // ← "IO exception" → null（与 missing 混为一谈）
    }
}
```

返回类型 `BitArray?` 把"不存在"、"损坏"、"IO 异常"三种 null 揉成一种——上层 `PlayerExplorationTracker` 无法区分能否安全地负缓存。

#### `FileExplorationStorage.Save`（约 line 53-78）

```csharp
public void Save(string accountName, BitArray bitmap)
{
    ...
    try
    {
        ...
        File.WriteAllBytes(filePath, bytes);
    }
    catch (Exception ex)
    {
        PluginLogger.Error(...);                    // ← 仅打日志，调用方不感知
    }
}
```

`SaveAll` 调用方无法分辨成功 / 失败，关机时无法汇总。

#### `PlayerExplorationTracker.GetBitmap` 现行负缓存路径（约 line 191-194）

```csharp
if (loaded is null)
{
    _missingFiles.Add(accountName);                 // ← 任何 null 都进负缓存（包括异常）
    return null;
}
```

`PlayerExplorationTracker.Load` 现行 null 路径（约 line 237-240）：

```csharp
if (bitmap is null) return;                         // ← 不写 _missingFiles，与 GetBitmap 不对称
```

#### `OnPlayerPostLogin`（约 line 207-220）

```csharp
private void OnPlayerPostLogin(PlayerPostLoginEventArgs args)
{
    _onlineTimeService?.StartSession(args.Player.Account.Name);
    var accountName = args.Player?.Account?.Name;
    if (!string.IsNullOrEmpty(accountName))
    {
        _playerExplorationTracker?.Load(accountName);            // ← 先 Load
        _playerExplorationTracker?.ForgetLastSample(accountName); // ← 后 ForgetLastSample（漏洞：会误清 PlayerUpdate 写入的新 lastSample）
    }
}
```

### 关键不变量（fix 不能破坏）

- 外部 REST 行为零变化（路由 / 字段 / 状态码 / 错误文案 / 日志字段命名 + 持久化路径 / 文件格式）
- 持久化层 fail-safe：异常不向上抛，调用方拿稳定结果
- 并发模型不变：`_lock` 内 dict 操作 + math，IO 在 lock 外
- 现有 287 测试全部通过（某些 `IExplorationStorage` 测试需要因接口变化适配，但断言逻辑不变）

## Decision (ADR-lite)

**Context**：`IExplorationStorage.Load(name, count)` 返回 `BitArray?` 把"不存在"、"损坏"、"IO 异常"三种 null 揉成一种，无法支持安全的负缓存决策；`Save` 是 void 且内部吞异常，调用方无法感知失败。

**Decision**：升级 `IExplorationStorage` 接口语义：

- `Load` 返回 `record ExplorationLoadResult(BitArray? Bitmap, bool FileMissing)`——"文件不存在"独立报告，调用方据此决定是否进负缓存
- `Save` 返回 `bool`——成功 / 失败，调用方据此累加可观测性指标

**Consequences**：
- 优点：消除 C（瞬时 IO 错误永久负缓存），保留 B 的优化（`Load` null 路径在确认 missing 时也写负缓存）；D 让 `SaveAll` 能汇总成功率；接口表达更准确
- 缺点：`IExplorationStorage` 接口变化，测试 fake 需要适配（已经有 `InMemoryStorage` fake 等会改）；`record` 引入小额内存分配（每次 Load 一个临时对象，可忽略）
- 待评估：未来若 storage 增加更多状态（如"权限不足"、"卷未挂载"），`bool FileMissing` 可演化为 enum；本任务暂不引入

## Requirements

### Fix C：区分"文件不存在"和"异常"，仅前者写负缓存

- 引入 `public sealed record ExplorationLoadResult(BitArray? Bitmap, bool FileMissing)`
- `IExplorationStorage.Load` 签名改为返回 `ExplorationLoadResult`
- `FileExplorationStorage.Load` 实现细分：
  - 输入校验失败（empty name / count ≤ 0） → `(null, false)`
  - `!File.Exists(filePath)` → `(null, true)`（确认 missing，可负缓存）
  - 文件存在但 size 不匹配 → `(null, false)`（损坏，**不**负缓存——可能是不完整写入，下次重试可能恢复）
  - 文件存在但 IO 异常（catch 块） → `(null, false)`（瞬时错误，**不**负缓存）
  - 成功 → `(bitmap, false)`
- `PlayerExplorationTracker.GetBitmap` 在 `loaded.Bitmap is null && loaded.FileMissing` 时才写 `_missingFiles`

### Fix B：`Load` 路径同样按 `FileMissing` 写负缓存（与 GetBitmap 对称）

- `PlayerExplorationTracker.Load(name)` 收到 `(null, true)` 时，在 `_lock` 内写 `_missingFiles.Add(name)`
- 收到 `(null, false)`（异常 / 损坏）时不写 `_missingFiles`（与 C 一致）

### Fix A：`OnPlayerPostLogin` 顺序交换

- 把 `_playerExplorationTracker?.ForgetLastSample(accountName)` 调用**前移**到 `Load(accountName)` 之前
- 这样即使 `Load` 期间 `OnPlayerUpdate` 触发并写入 `_lastSamples`，那条数据是合法的（新 session 起点），不会被误清

### Fix D：`Save` 返回 bool + `SaveAll` 汇总日志

- `IExplorationStorage.Save` 签名改为 `bool Save(...)`
- `FileExplorationStorage.Save` 成功路径返回 `true`，catch 路径返回 `false`（保留现有 Error 日志）
- `PlayerExplorationTracker.SaveAll` 累加成功 / 失败计数；最终：
  - 全部成功（failure == 0）→ `PluginLogger.Info($"SaveAll 完成，成功={success}")`
  - 有失败 → `PluginLogger.Warn($"SaveAll 完成，成功={success}，失败={failure}")`
- `PlayerExplorationTracker.Save(name)`（单账号）也接受 bool 返回值，但当前调用方（`OnServerLeave`）不需要新行为；返回值忽略即可（现有 Save 方法仍是 void）

## Acceptance Criteria

- [ ] **C**：`FileExplorationStorage.Load` 在文件不存在时返回 `(null, FileMissing=true)`；catch 异常时返回 `(null, FileMissing=false)`；size 不匹配时返回 `(null, FileMissing=false)`；成功时返回 `(bitmap, FileMissing=false)`
- [ ] **C**：`PlayerExplorationTracker.GetBitmap` 在 IO 异常导致 `(null, false)` 时**不**进 `_missingFiles`，下次调用会重试 IO
- [ ] **C** 测试：构造 throwing storage（fake 在 Load 时抛异常） → `GetBitmap` 第一次返回 null + 不写负缓存 → 第二次仍走 IO（再次抛 → 再 null）；切换 fake 行为为成功 → 第三次能拿到数据
- [ ] **B**：`PlayerExplorationTracker.Load(name)` 在 `FileMissing=true` 时也写 `_missingFiles`
- [ ] **B** 测试：调 `tracker.Load(unknown)` → 不抛异常 + `_missingFiles` 包含 `unknown`（用 `GetBitmap` 后 `LoadCallCount` 不增加做 spy 间接断言）
- [ ] **A**：`OnPlayerPostLogin` 中 `ForgetLastSample` 在 `Load` 之前调用
- [ ] **A** 测试：可复用现有的 #5 单元测试断言；plugin 钩子层无新单测
- [ ] **D**：`SaveAll` 在全部成功时打 INFO 日志（含成功数）；有失败时打 WARN 日志（含成功 + 失败数）
- [ ] **D** 测试：构造 fake storage 让其中一个账号 Save 失败 → `SaveAll` 调完后日志包含成功失败计数
- [ ] 现有 287 测试全部通过；新增至少 4-5 条测试
- [ ] `dotnet build` 0 警告 0 错误
- [ ] 不改 REST 路由 / 响应字段 / 状态码 / 错误文案 / 持久化路径

## Definition of Done

- 所有测试 green、build 干净
- 接口变更范围控制在 `IExplorationStorage` + 实现 + 直接调用方（`PlayerExplorationTracker`）+ 测试 fake
- 行为契约（外部 REST + 持久化路径 + 日志字段命名）零变化
- spec 合规

## Technical Approach

### 1. 新增 `Models/ExplorationLoadResult.cs`（或放在 `Services/Exploration/` 目录）

```csharp
namespace NextBotAdapter.Services;

public sealed record ExplorationLoadResult(System.Collections.BitArray? Bitmap, bool FileMissing);
```

注：放在 `Services/Exploration/` 下与 `IExplorationStorage` 同目录更内聚（不属于 REST 响应模型）。

### 2. `IExplorationStorage`

```csharp
public interface IExplorationStorage
{
    ExplorationLoadResult Load(string accountName, int expectedBitCount);
    bool Save(string accountName, BitArray bitmap);
}
```

### 3. `FileExplorationStorage`

```csharp
public ExplorationLoadResult Load(string accountName, int expectedBitCount)
{
    if (string.IsNullOrWhiteSpace(accountName) || expectedBitCount <= 0)
        return new ExplorationLoadResult(null, false);

    var filePath = ResolveFilePath(accountName);
    if (!File.Exists(filePath))
        return new ExplorationLoadResult(null, true);

    try
    {
        var bytes = File.ReadAllBytes(filePath);
        var expectedByteCount = (expectedBitCount + 7) / 8;
        if (bytes.Length != expectedByteCount)
        {
            PluginLogger.Warn($"加载玩家探索数据失败，原因：文件大小不匹配，accountName={accountName}，expected={expectedByteCount}，actual={bytes.Length}");
            return new ExplorationLoadResult(null, false);
        }
        var bitmap = new BitArray(bytes) { Length = expectedBitCount };
        return new ExplorationLoadResult(bitmap, false);
    }
    catch (Exception ex)
    {
        PluginLogger.Warn($"加载玩家探索数据失败，accountName={accountName}，原因：{ex.Message}");
        return new ExplorationLoadResult(null, false);
    }
}

public bool Save(string accountName, BitArray bitmap)
{
    if (string.IsNullOrWhiteSpace(accountName) || bitmap.Length <= 0) return false;

    var filePath = ResolveFilePath(accountName);
    try
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);
        var byteCount = (bitmap.Length + 7) / 8;
        var bytes = new byte[byteCount];
        bitmap.CopyTo(bytes, 0);
        File.WriteAllBytes(filePath, bytes);
        return true;
    }
    catch (Exception ex)
    {
        PluginLogger.Error($"保存玩家探索数据失败，accountName={accountName}，原因：{ex.Message}");
        return false;
    }
}
```

### 4. `PlayerExplorationTracker`

`GetBitmap` 适配：

```csharp
var result = _storage.Load(accountName, width * height);

lock (_lock)
{
    if (_bitmaps.TryGetValue(accountName, out var existing))
        return new BitArray(existing);

    if (result.Bitmap is null)
    {
        if (result.FileMissing) _missingFiles.Add(accountName);
        return null;
    }

    _bitmaps[accountName] = result.Bitmap;
    _missingFiles.Remove(accountName);
    return new BitArray(result.Bitmap);
}
```

`Load(name)` 适配：

```csharp
public void Load(string accountName)
{
    if (string.IsNullOrWhiteSpace(accountName)) return;
    var (width, height) = _worldSizeProvider();
    if (width <= 0 || height <= 0) return;

    var expectedBitCount = width * height;
    var result = _storage.Load(accountName, expectedBitCount);

    bool inserted;
    lock (_lock)
    {
        if (_bitmaps.ContainsKey(accountName)) return;

        if (result.Bitmap is null)
        {
            if (result.FileMissing) _missingFiles.Add(accountName);
            return;
        }

        _bitmaps[accountName] = result.Bitmap;
        _missingFiles.Remove(accountName);
        inserted = true;
    }

    if (inserted)
    {
        PluginLogger.Info($"加载玩家探索数据成功，accountName={accountName}");
    }
}
```

`SaveAll` 适配（D）：

```csharp
public void SaveAll()
{
    Dictionary<string, BitArray> snapshot;
    lock (_lock) { ... existing snapshot logic ... }

    var success = 0;
    var failure = 0;
    foreach (var (name, bitmap) in snapshot)
    {
        if (_storage.Save(name, bitmap)) success++;
        else failure++;
    }

    if (failure > 0)
        PluginLogger.Warn($"SaveAll 完成，成功={success}，失败={failure}");
    else if (success > 0)
        PluginLogger.Info($"SaveAll 完成，成功={success}");
    // success == 0 && failure == 0：dict 空，不打日志（启动后未持久化任何 bitmap）
}
```

`Save(name)` 单账号（保持 void 不变，但内部调用 `_storage.Save` 拿到 bool 后忽略——可选地加 fail 日志，但已有 storage 层 Error 日志，不需要重复）：

```csharp
public void Save(string accountName) { ... _storage.Save(accountName, snapshot); }   // 忽略返回值
```

### 5. `NextBotAdapterPlugin.OnPlayerPostLogin`（A）

```csharp
private void OnPlayerPostLogin(PlayerPostLoginEventArgs args)
{
    _onlineTimeService?.StartSession(args.Player.Account.Name);
    var accountName = args.Player?.Account?.Name;
    if (!string.IsNullOrEmpty(accountName))
    {
        _playerExplorationTracker?.ForgetLastSample(accountName);   // ← 前移
        _playerExplorationTracker?.Load(accountName);
    }
}
```

### 6. 测试

`FileExplorationStorageTests.cs` 已有的 happy path + missing file + size mismatch 测试**继续 pass**（断言改成 `result.Bitmap is null` + `result.FileMissing` 即可）。新增至少 1 条："Save returns true on success / false on directory permission failure"（或用 mock dir 模拟）——可选，因为是直觉性极强的纯 mapping，不强求

`PlayerExplorationTrackerTests.cs` 新增：

1. **C**：`GetBitmap_ShouldNotCacheMissOnIoException_AndRetryOnNextCall`——构造一个 ThrowingStorage（实现 `IExplorationStorage.Load` 抛异常→返回 `(null, false)`），第一次 `GetBitmap(name)` 返回 null + `_missingFiles` **不**包含 name；`LoadCallCount == 1`；第二次 `GetBitmap(name)` 仍触发 IO（`LoadCallCount == 2`）；切换 fake 行为为成功（返回 `(bitmap, false)`） → 第三次成功
2. **B**：`Load_ShouldRecordMissing_WhenStorageReportsFileMissing`——`InMemoryStorage` 的 `Load(unknown)` 返回 `(null, true)`，调 `tracker.Load(unknown)` → 后续 `GetBitmap(unknown)` `LoadCallCount` 不增加（`_missingFiles` 命中）
3. **D-1**：`SaveAll_ShouldLogInfo_WhenAllSucceed`——用 fake storage（默认成功）SaveAll 后断言日志（如能用 `PluginLogger` 测试 hook）；如果 PluginLogger 不易 mock，直接断言 storage 收到的 Save 调用次数 == in-memory 数量即可，日志合理性留给手工核对
4. **D-2**：`SaveAll_ShouldCountFailures_WhenStorageFails`——fake storage 让某账号 Save 返回 false，验证 storage 仍接到所有 Save 调用（不会因为某次 false 中断）。日志断言同 D-1

注：D 测试如果 `PluginLogger` 不易 mock，重点放在"调用次数 + 不抛异常 + 不中断"这些可验证的断言上。日志格式留给手工 inspect。

测试 fake 列表（需要更新签名）：
- `NextBotAdapter.Tests/PlayerExplorationTrackerTests.cs::InMemoryStorage`
- `NextBotAdapter.Tests/FileExplorationStorageTests.cs` 实测（直接对真实 `FileExplorationStorage`，断言 `result.Bitmap` / `result.FileMissing`）
- 可能其他测试中使用的 fake——grep 全文确认

## Out of Scope

- 不改 `IPlayerExplorationTracker` 接口（公开 API 不变）
- 不改 REST 路由 / 端点 / 响应字段 / 状态码 / 错误文案
- 不改持久化路径 / 文件格式 / SanitizeFileName
- 不改 stamp / 插值 / 瞬移 / reveal box
- 不引入异常类型层级（仅用 bool 表达 missing/error 区分）
- 不改 leaderboard service（自动受益于 C/B）
- 不动 `MapImageService` / `MapFileService` / `PlayerMapImageService` / `MapRenderMutex`
- 不动 `OnPlayerUpdate` / `OnServerLeave` / `Dispose`
- 不动 `docs/REST_API.md`（外部行为零变化）
- 不修审计未涉及的项（Windows 保留名 / `_bitmaps` 不清退 / PNG 编码 lock 等都在更早的 PRD 列为 future）

## Technical Notes

### 涉及文件

- 产品代码（**新增**）：
  - `NextBotAdapter/Services/Exploration/ExplorationLoadResult.cs`（新 record）
- 产品代码（**修改**）：
  - `NextBotAdapter/Services/Exploration/IExplorationStorage.cs`（接口签名）
  - `NextBotAdapter/Services/Exploration/FileExplorationStorage.cs`（Load + Save 实现）
  - `NextBotAdapter/Services/Exploration/PlayerExplorationTracker.cs`（GetBitmap / Load / SaveAll）
  - `NextBotAdapter/Plugin/NextBotAdapterPlugin.cs`（OnPlayerPostLogin 顺序）
- 测试：
  - `NextBotAdapter.Tests/FileExplorationStorageTests.cs`（适配新返回类型）
  - `NextBotAdapter.Tests/PlayerExplorationTrackerTests.cs`（更新 InMemoryStorage fake + 新增 C/B/D 测试）

### 不需要改

- `IPlayerExplorationTracker.cs`（公开方法签名不变）
- REST endpoints / routes / permissions / registrar
- leaderboard service / map services
- `docs/REST_API.md`

### 并发模型

- `_lock` 不变量保持：dict 操作 + math 在 lock 内；IO 在 lock 外
- `Load` 的 INFO 日志移到 lock 外（仅在确实写入 `_bitmaps` 时打）——避免 lock 内 IO 风格调用，以及避免短路时打误导日志

### Future Evolution（不在本任务做）

- `bool FileMissing` 演化为 enum（添加 "PermissionDenied"、"VolumeNotMounted" 等）
- `IExplorationStorage` 加 `Exists(name) -> bool` 方法支持更细粒度判定
- `Save` 返回 `SaveResult` record 携带详细原因
- 修审计 #4（Windows 保留名）/ #6（trimmedUser 命名）/ `_bitmaps` 不退订等
