# perf: 全项目性能 audit 全量修复（9 项）

## Goal

应用全项目性能 audit + 主代理 trust-but-verify 后保留的 9 个真实优化项。所有修复**严格非破坏**：不改公开 API、不改 REST 路由 / 字段、不改持久化文件格式、不改外部行为。仅减少 IO / 减少 reflection / 减少 allocation / 改善 cache locality / 解锁竞争。

## What I already know

### 9 项修复清单（按优先级）

#### 🔴 V-P1：`PluginConfigService.Load()` 加缓存
**文件**：`Services/Configuration/PluginConfigService.cs:63-82`

每次 `Load()` 都走 `File.ReadAllText` + `BuildCompletedJson`（含 `JObject.FromObject` + `Merge`）+ `ToObject<NextBotAdapterConfig>`。`OnPlayerChat`（每条消息）/ `OnPlayerPreLogin` / `OnNetGreetPlayer`（连接 2 次）/ `NotifyPlayerOnline` / `NotifyPlayerOffline` 都直接调 → 10 名活跃聊天玩家约 1 次磁盘 IO/秒，纯冗余。

**修法**：加 `private NextBotAdapterConfig? _cached;` + 配套 lock；`Load()` 命中缓存直接返回；`Save` / `EnsureConfigComplete`（写盘后）/ `Reload` / `TryUpdateConfig` 写盘后 invalidate（`_cached = null`）。

#### 🔴 V-P2：`PlayerStatisticsReader` 反射结果缓存
**文件**：`Services/UserData/PlayerStatisticsReader.cs:16-22`

每次都 `source.GetType().GetProperty(name, flags)` + `GetField(name, flags)`，无缓存。Stats endpoint × 7 次 / leaderboard × N 账号触发。

**修法**：加 `static readonly ConcurrentDictionary<(Type, string), PropertyInfo?> PropertyCache` + 同款 FieldCache。首次查找写入，后续直接命中。**只缓存 PropertyInfo / FieldInfo 引用，不缓存值**——`property.GetValue(source)` 仍每次实时读，零数据时效影响。

#### 🔴 V-P3：图像像素遍历改行优先
**文件**：
- `Services/World/PlayerMapImageService.cs:27-50` 和 `:66-72`
- `Services/World/MapImageService.cs:63-70`
- `Services/World/MapTileGrid.cs`（Fill 也是 outer-x / inner-y 对 `MapTile[,]` 行优先存储不友好）

**修法**：所有这 4 处嵌套循环外层换 `y`、内层换 `x`，`image[x, y]` 坐标不变。CPU cache locality 大幅改善（理论 20-40% 像素填充 / tile fill 提速）。

#### 🟡 V-P4：`OnlineTimeService.EndSession` 锁外做 IO（snapshot inside, write outside）
**文件**：`Services/UserData/OnlineTimeService.cs:98-115`

当前整个 body 在 `lock(_lock)` 内 + 调 `PersistLocked` 同步写盘。20 名玩家同时离线串行排队 lock，期间所有其他 lock 用户阻塞。

**修法**：保留 lock 内更新 `_records` + 创建 `OnlineTimeStore` snapshot；写盘移到 lock 外。引入 `private readonly object _ioLock = new();` 序列化 IO（防止两次 EndSession 并发写盘 corruption）。同款修法应用于 `Flush` 和 `PersistAllSessions`。**保留即时落盘语义**，不退化到"5 分钟才落盘"。

#### 🟢 V-P5：白/黑名单从 List 改为 HashSet
**文件**：`Services/Security/WhitelistService.cs:133/148/183/195/219` + `BlacklistService.cs:132/146/180/192/215`

`_users` / `_entries` 改为 `HashSet`（白名单直接 `HashSet<string>(StringComparer.OrdinalIgnoreCase)`；黑名单保留 `List<BlacklistEntry>` 因为需要 reason 字段，但额外维护 `HashSet<string> _usernameSet` 做 O(1) 存在性判断）。

#### 🟢 V-P6：`SanitizeFileName` 缓存非法字符集
**文件**：`Services/Exploration/FileExplorationStorage.cs:94-106`

`Path.GetInvalidFileNameChars()` 每次返回新数组 + `Array.IndexOf` O(m) 线性扫描。

**修法**：`private static readonly HashSet<char> InvalidFileNameChars = new(Path.GetInvalidFileNameChars());`，遍历 char 用 `InvalidFileNameChars.Contains(c)` O(1)。

#### 🟢 V-P7：`PluginLogger.Normalize` 用 Regex 一次性替换
**文件**：`Services/Common/PluginLogger.cs:28-44`

3 次 `string.Replace` + `while(Contains("  ")) Replace("  ", " ")` 循环。

**修法**：`private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);` + `WhitespaceRegex.Replace(text, " ").Trim()`。一次扫描完成所有空白合并 + trim。

#### 🟢 V-P8：`PerformAutoLogin` 复用 `HasIpChanged` 已解析的 KnownIps
**文件**：`Plugin/NextBotAdapterPlugin.cs:HasIpChanged` + `PerformAutoLogin:613-621`

同一连接链 `OnNetGreetPlayer` → `EvaluateLoginConfirmation`（调 HasIpChanged 解析一次） → `PerformAutoLogin`（再解析一次）。

**修法**：`HasIpChanged` 改返回 `(bool changed, List<string>? parsed)` 或加 `out List<string>? parsedIps` 参数；`PerformAutoLogin` 直接复用解析结果。**接口 / 调用风格调整需评估对其他调用方的影响**——若 HasIpChanged 是 plugin 内部 private 方法，可直接改签名。

#### 🟢 V-P9：`OnlineTimeService.Flush` 移除不必要的 `.ToList()`
**文件**：`Services/UserData/OnlineTimeService.cs:179`

`foreach (var name in _activeSessions.Keys.ToList())` 内只更新 value（不增删 key）。Dictionary 在 foreach 内修改 value 不会触发 version-check。

**修法**：直接 `foreach (var kvp in _activeSessions)` + 用临时 list 收集 (name, now) 在循环外更新。或者保持 `.ToList()`（极小开销，可不改）。**最小改动**：去掉 `.ToList()`，foreach key 后直接修改 `_activeSessions[name] = now` ——但这在严格意义上"foreach 时修改字典 value"虽然 .NET 实际允许（version 不变），但**可读性不佳**。**推荐**：`foreach (var entry in _activeSessions.ToArray())`（拷贝成数组），微小但不容易出错。

#### 🟢 V-P10（M-6）：`WorldExploredMapImageService` 用 `TryOrInto` 避免 BitArray 拷贝
**文件**：`Services/World/WorldExploredMapImageService.cs:29-41` + `Services/Exploration/IPlayerExplorationTracker.cs` / `PlayerExplorationTracker.cs`

当前每个账号都 `tracker.GetBitmap(name)` 返回 `new BitArray(...)` snapshot 拷贝（大世界 ~2.5 MB / 账号），然后 `union.Or(bitmap)`。100 账号 ≈ 250 MB 短期 GC 分配。

**修法**：在 `IPlayerExplorationTracker` 加新方法 `bool TryOrInto(string accountName, BitArray target)`：
- 若 `_bitmaps` 命中：在 lock 内调 `target.Or(_bitmaps[name])` 直接合并到 target（target 大小已知一致），返回 true
- 若 lazy-load 命中：把磁盘 BitArray Or 进 target、写入 `_bitmaps`、返回 true
- miss：返回 false

`WorldExploredMapImageService.Generate` 改为调 `_tracker.TryOrInto(username, union)` 替代 GetBitmap+Or。**接口签名扩展，向后兼容**（仅 add，不改）。

### 关键不变量（修复不能破坏）

- 所有公开接口签名不变（V-P10 只是**添加**新方法）
- REST 路由 / 字段 / 状态码 / 错误文案不变
- 持久化文件格式 / 路径不变
- `_lock` 内 IO 移到外面后**仍要确保数据一致性**（snapshot inside, write outside 模式 + 必要时新增 _ioLock）
- 现有 322 测试全部通过

## Decision (ADR-lite)

**Context**：全项目性能 audit 找到 9 项可优化点，全部非破坏。统一在一个任务里修，避免 commit 分散。

**Decision**：合并到一个 task / 一个 commit；按文件 / 模块组织 diff；测试覆盖关键场景（cache invalidation / 反射 cache 命中 / lock-IO 解耦 / TryOrInto 行为）。

**Consequences**：
- 优点：聚合修复减少 commit 噪音；性能改善是叠加的（某些路径多个优化叠加效果显著）
- 缺点：单 commit 较大；review 时需要分块看
- 待评估：未来如果发现某项优化引入回归，回滚整个 commit 影响较大；可拆 commit 但 PRD 仍统一

## Requirements

每项保持现有功能行为（输入 → 输出语义零变化），仅改善性能特征。具体要求见上面"修复清单"每项的描述。

## Acceptance Criteria

### 行为契约
- [ ] 所有现有 322 测试 pass
- [ ] V-P1 cache：Save/EnsureConfigComplete/Reload/TryUpdateConfig 后 `_cached` 被 invalidate（新增测试断言）
- [ ] V-P2 reflection cache：第二次 ReadDeaths 同一 (Type, fieldName) 不再调 GetType().GetProperty（新增测试用 spy 或类型断言）
- [ ] V-P3 像素行优先：rendered PNG 与原版 byte-for-byte 一致（现有 endpoint 契约测试通过即可）
- [ ] V-P4 EndSession：lock 内不再做 IO；多并发 EndSession 不丢数据；磁盘内容仍保持单调合法（新增测试模拟并发 + 验证最终磁盘内容）
- [ ] V-P5 HashSet：白名单 add / remove / contains 行为不变；黑名单 add / remove / IsBlacklisted / TryValidateJoin 行为不变（现有 service 测试 pass + 加 1 条 large-set 性能锚点测试可选）
- [ ] V-P6 SanitizeFileName：返回值与原实现一致；空、包含非法字符、保留名等 case 表现不变
- [ ] V-P7 PluginLogger.Normalize：合并空白 + trim 行为与原实现一致（已有测试 pass + 多空格 / tab / newline edge case 加 1 条）
- [ ] V-P8 KnownIps：HasIpChanged + PerformAutoLogin 行为不变（仅复用解析结果）
- [ ] V-P9 Flush：行为不变（active sessions / records 累计正确）
- [ ] V-P10 TryOrInto：合并多人 bitmap 后 union 内容与"GetBitmap + Or"路径完全相同（新增测试断言每个 set bit 都来自至少一个 source）

### 性能（不强求 benchmark，仅观察）
- [ ] V-P1 cache 命中后 `Load()` 不再触发 `File.ReadAllText`（spy 文件系统验证）
- [ ] V-P2 第二次 ReadDeaths 相同类型不再触发 reflection 查找（spy 或反射调用计数验证）
- [ ] V-P10 TryOrInto 不再分配新 BitArray 引用（spy 计数验证）

### 通用
- [ ] `dotnet build` 0 警告 0 错误
- [ ] `dotnet test` 全部通过（322 baseline + 至少 5 新增 ≥ 327）
- [ ] 所有公开接口签名不变（V-P10 仅 add 新方法到 IPlayerExplorationTracker）
- [ ] REST 路由 / 字段 / 状态码 / 错误文案 / 持久化文件格式零变化

## Definition of Done

- 全部测试 green、build 干净
- 性能改善通过 spy / 计数验证（不需要 benchmark 库）
- 所有公开 API / 文件契约保持
- spec 合规

## Technical Approach

详见每项 V-P# 的"修法"段落。重点 / 跨多项的共性：

### 缓存 invalidation 契约（V-P1）

```csharp
public sealed class PluginConfigService
{
    private NextBotAdapterConfig? _cached;
    private readonly object _cacheLock = new();

    public NextBotAdapterConfig Load()
    {
        var existing = _cached;
        if (existing is not null) return existing;
        lock (_cacheLock)
        {
            if (_cached is not null) return _cached;
            _cached = LoadFromDisk();   // 现有 Load() 内容封装
            return _cached;
        }
    }

    public NextBotAdapterConfig Reload()
    {
        lock (_cacheLock) { _cached = null; }
        return Load();
    }

    public void Save(...) { ...写盘...; lock (_cacheLock) { _cached = null; } }
    // EnsureConfigComplete / TryUpdateConfig 同款 invalidate
}
```

注意：`Load` 第一次读取后 `_cached` 持久存活；进程内只读盘一次（除非显式 Reload / Save）。

### Reflection cache（V-P2）

```csharp
public static class PlayerStatisticsReader
{
    private static readonly ConcurrentDictionary<(Type, string), PropertyInfo?> PropertyCache = new();
    private static readonly ConcurrentDictionary<(Type, string), FieldInfo?> FieldCache = new();
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    public static int ReadDeaths(object? source, string fieldName)
    {
        if (source is null || string.IsNullOrWhiteSpace(fieldName)) return 0;
        var type = source.GetType();
        var key = (type, fieldName);

        var property = PropertyCache.GetOrAdd(key, k => k.Item1.GetProperty(k.Item2, Flags));
        if (property?.PropertyType == typeof(int) && property.GetValue(source) is int p) return p;

        var field = FieldCache.GetOrAdd(key, k => k.Item1.GetField(k.Item2, Flags));
        if (field?.FieldType == typeof(int) && field.GetValue(source) is int f) return f;

        return 0;
    }
}
```

### Lock-IO 解耦（V-P4）

```csharp
public void EndSession(string username)
{
    long elapsed;
    OnlineTimeStore? snapshot = null;

    lock (_lock)
    {
        if (!_activeSessions.TryGetValue(username, out var start)) return;
        _activeSessions.Remove(username);
        elapsed = (long)(DateTime.UtcNow - start).TotalSeconds;
        _records[username] = _records.TryGetValue(username, out var existing) ? existing + elapsed : elapsed;
        snapshot = new OnlineTimeStore(new Dictionary<string, long>(_records));
    }

    if (snapshot is not null)
    {
        lock (_ioLock) { SaveStore(snapshot); }
    }

    PluginLogger.Info($"玩家 {username} 本次在线 {elapsed} 秒，已累计保存。");
}
```

`Flush` 和 `PersistAllSessions` 同款重构。`_ioLock` 是新 `private readonly object _ioLock = new();`。

### TryOrInto（V-P10）

```csharp
public bool TryOrInto(string accountName, BitArray target)
{
    if (string.IsNullOrWhiteSpace(accountName) || target is null) return false;

    lock (_lock)
    {
        if (_bitmaps.TryGetValue(accountName, out var bitmap))
        {
            if (bitmap.Length == target.Length) target.Or(bitmap);
            return true;
        }
        if (_missingFiles.Contains(accountName)) return false;
    }

    var (w, h) = _worldSizeProvider();
    if (w <= 0 || h <= 0) return false;
    var result = _storage.Load(accountName, w * h);

    lock (_lock)
    {
        if (_bitmaps.TryGetValue(accountName, out var existing))
        {
            if (existing.Length == target.Length) target.Or(existing);
            return true;
        }
        if (result.Bitmap is null)
        {
            if (result.FileMissing) _missingFiles.Add(accountName);
            return false;
        }
        _bitmaps[accountName] = result.Bitmap;
        _missingFiles.Remove(accountName);
        if (result.Bitmap.Length == target.Length) target.Or(result.Bitmap);
        return true;
    }
}
```

`WorldExploredMapImageService.Generate` 改：
```csharp
foreach (var (_, username) in _gateway.GetAllUserAccounts())
{
    _tracker.TryOrInto(username, union);
}
```

### 像素行优先（V-P3）

每处的 `for (var x ...) for (var y ...)` 改为 `for (var y ...) for (var x ...)`，访问 `image[x, y]` / `target[rawX, rawY]` 坐标不变。

### Whitelist HashSet（V-P5）

`WhitelistService._users` 从 `List<string>` 改为 `HashSet<string>(StringComparer.OrdinalIgnoreCase)`：
- `Add` → `_users.Add(user)`（自动去重）
- `Remove` → `_users.Remove(user)`
- `Contains` → `_users.Contains(user)` O(1)
- `GetAll` → `_users.ToArray()`

`BlacklistService._entries` 保留 `List<BlacklistEntry>`（需要 reason），额外加 `private readonly HashSet<string> _usernameSet = new(StringComparer.OrdinalIgnoreCase);`：
- `IsBlacklisted` / 存在性检查 → `_usernameSet.Contains(name)`
- `TryValidateJoin` 仅在 `_usernameSet.Contains(name)` true 时才 `_entries.FirstOrDefault` 找 reason
- `TryAdd` / `TryRemove` 同步维护 `_usernameSet` + `_entries`

### SanitizeFileName cache（V-P6）

```csharp
private static readonly HashSet<char> InvalidFileNameChars = new(Path.GetInvalidFileNameChars());

private static string SanitizeFileName(string raw)
{
    var chars = raw.ToCharArray();
    for (var i = 0; i < chars.Length; i++)
    {
        if (InvalidFileNameChars.Contains(chars[i])) chars[i] = '_';
    }
    return new string(chars);
}
```

### PluginLogger.Normalize Regex（V-P7）

```csharp
private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);

private static string Normalize(string text)
{
    if (string.IsNullOrEmpty(text)) return text;
    return WhitespaceRegex.Replace(text, " ").Trim();
}
```

### KnownIps 复用（V-P8）

`HasIpChanged` 改签名加 `out List<string>? parsedIps` 参数（因是 plugin 私有 method，不破坏外部）；`PerformAutoLogin` 接受 `parsedIps` 直接复用。

### Flush 去 .ToList()（V-P9）

```csharp
public void Flush()
{
    lock (_lock)
    {
        var now = DateTime.UtcNow;
        // ToArray 避免 foreach 内部修改字典 value 时的潜在问题，比 ToList 同等
        var snapshot = _activeSessions.ToArray();   // 或: var keys = _activeSessions.Keys.ToArray();
        foreach (var (name, start) in snapshot)
        {
            var elapsed = (long)(now - start).TotalSeconds;
            _records[name] = (_records.TryGetValue(name, out var existing) ? existing : 0) + elapsed;
            _activeSessions[name] = now;
        }
        // ... PersistLocked or snapshot+IO outside lock per V-P4 ...
    }
}
```

注：V-P9 与 V-P4 同时改。

## Out of Scope

- 不引入 metrics / benchmark 框架（spy 计数足够）
- 不修配置文件格式 / REST routes / 公开接口签名
- 不改 player exploration / map render 算法语义（仅 cache locality / 数据结构）
- 不动持久化文件结构 / 文件命名 / SanitizeFileName 行为
- 不动既有 PRD Out-of-Scope 项（Windows 保留名 / `_bitmaps` 长期增长 / PNG 编码 lock 等已知 known issues）
- 不动 leaderboard endpoint 业务逻辑

## Technical Notes

### 涉及文件

- 产品代码（**修改**）：
  - `Services/Configuration/PluginConfigService.cs`（V-P1）
  - `Services/UserData/PlayerStatisticsReader.cs`（V-P2）
  - `Services/World/PlayerMapImageService.cs`（V-P3）
  - `Services/World/MapImageService.cs`（V-P3）
  - `Services/World/MapTileGrid.cs`（V-P3）
  - `Services/UserData/OnlineTimeService.cs`（V-P4 + V-P9）
  - `Services/Security/WhitelistService.cs`（V-P5）
  - `Services/Security/BlacklistService.cs`（V-P5）
  - `Services/Exploration/FileExplorationStorage.cs`（V-P6）
  - `Services/Common/PluginLogger.cs`（V-P7）
  - `Plugin/NextBotAdapterPlugin.cs`（V-P8）
  - `Services/Exploration/IPlayerExplorationTracker.cs`（V-P10 加方法）
  - `Services/Exploration/PlayerExplorationTracker.cs`（V-P10 实现）
  - `Services/World/WorldExploredMapImageService.cs`（V-P10 改用 TryOrInto）

- 测试（**新增 / 扩展**）：
  - `PluginConfigServiceTests.cs`（V-P1 cache invalidation 测试）
  - `PlayerStatisticsReaderTests.cs`（V-P2 cache 命中验证）
  - `OnlineTimeServiceTests.cs`（V-P4 lock-IO 解耦验证 + V-P9 行为不变）
  - `WhitelistServiceTests.cs` / `BlacklistServiceTests.cs`（V-P5 行为契约）
  - `FileExplorationStorageTests.cs`（V-P6 SanitizeFileName 等价性）
  - `PluginLoggerTests.cs`（V-P7 normalize 等价 + edge case）
  - `PlayerExplorationTrackerTests.cs`（V-P10 TryOrInto 行为 + 与 GetBitmap+Or 等价）
  - 所有现有契约测试 pass 优先

### 不需要改

- REST 路由 / 端点 / 权限 / 注册器
- 持久化路径 / 文件格式
- `IExplorationStorage` / `FileExplorationStorage.Load` / `Save` 已有方法
- 渲染服务对外契约（fileName 形态、PNG 二进制）
- `OnPlayerUpdate` / `OnServerLeave` / `OnPlayerPostLogin` / `Dispose` 钩子框架
- `docs/REST_API.md` / `docs/CONFIGURATION.md`

### Future Evolution（不在本任务做）

- HttpClient 池化策略（如有需要）
- PNG 异步编码 / 流式输出
- BitArray Or 用 Span<int> 直接位操作避开 BitArray 框架开销
- Whitelist / Blacklist 大型集合的 trie / 索引（极端规模才需要）
