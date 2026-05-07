# fix: 全项目审计 follow-up（V-3 / V-4 / V-6）

> 注：任务 slug 沿用 `post-login-revalidate-blacklist-whitelist`，但实际包含三个独立小修复。

## Goal

修复全项目审计经主代理 trust-but-verify 后保留的 3 个真实问题：

- **V-4（核心）**：黑/白名单仅在 `OnPlayerInfo` 按 `args.Name`（玩家显示名）拦截；玩家可改显示名绕过 → 进服后再 `/login <被封账号> <密码>`，绕过封禁 / 白名单。修法：在 `OnPlayerPostLogin` 加一次按 `Account.Name` 的二次校验。
- **V-6**：`OnlineTimeService.Reload` 在锁外做 IO，期间另一线程的 `Flush` / `EndSession` 写入会被 IO 完成后的"完全替换"覆盖。修法：改为按账号"取最大值"合并（records 单调增长保证安全）。
- **V-3**：`ConfigEndpoints.VerifyNextBot` 用 `.GetAwaiter().GetResult()` 在 REST worker 线程上 sync-block；自托管低 QPS 下实际影响接近 0，但 Defensive 修法不破坏接口。

## What I already know

### V-4 现状

文件：`Plugin/NextBotAdapterPlugin.cs`

`OnPlayerInfo`（约 line 362-378）按 `args.Name` 拦截：
```csharp
if (_blacklistService is not null && !_blacklistService.TryValidateJoin(args.Name, out var blacklistReason))
{ ... args.Player?.Disconnect(blacklistReason!); }

if (_whitelistService is not null && !_whitelistService.TryValidateJoin(args.Name, out var denialReason))
{ ... args.Player?.Disconnect(denialReason ?? "你不在白名单中"); }
```

`OnPlayerPostLogin`（约 line 207-220）目前只做：
```csharp
_onlineTimeService?.StartSession(args.Player.Account.Name);
var accountName = args.Player?.Account?.Name;
if (!string.IsNullOrEmpty(accountName))
{
    _playerExplorationTracker?.ForgetLastSample(accountName);
    _playerExplorationTracker?.Load(accountName);
}
```

`OnPlayerPreLogin` 只查 LoginConfirmation，**不**查黑/白名单。

`IBlacklistService.TryValidateJoin(string user, out string? denialReason)` / `IWhitelistService.TryValidateJoin(string user, out string? denialReason)`：返回 `true` 允许进服、`false` 拒绝（denialReason 含拒绝文案）。这两个 service 的 `TryValidateJoin` 内部已经处理"功能未启用 / user 在名单中"等逻辑，调用方只需透传 user 名字。

### V-6 现状

文件：`Services/UserData/OnlineTimeService.cs:53-63`

```csharp
public OnlineTimeStore Reload()
{
    var store = Load();   // ← IO 在锁外
    lock (_lock)
    {
        _records = new Dictionary<string, long>(store.Records);   // ← 完全替换
    }
    return store;
}
```

竞态：
1. `Load()` IO 期间，活跃线程通过 `Flush` 把 `_records[Alice] += newDelta` 写盘 + 更新内存
2. 但 `Reload` 的 `Load()` 已经读到了旧版（不含 newDelta）
3. `Reload` 持锁后用旧版完全替换内存，**newDelta 在内存里被抹掉**

注意：`_records` 单调增长（`StartSession` / `Flush` / `EndSession` 都是累加，不减少），所以"取最大值合并"是安全的（每个 key 取 in-memory 与磁盘的较大值即可保留较新数据）。

### V-3 现状

文件：`Rest/ConfigEndpoints.cs:101`

```csharp
var result = probe.ProbeAsync(settings).GetAwaiter().GetResult();
```

REST worker 线程同步阻塞等待 HTTP probe（默认 5s 超时）。TShock REST 通常无 SyncContext，不会真死锁，但理论上线程饥饿。

**Defensive 修法**：用 `Task.Run` + `GetAwaiter().GetResult()` 包一层，避免任何潜在 SyncContext 问题。这并不改善线程饥饿（依然有 1 个线程被阻塞等待），但提高了对宿主线程模型变化的鲁棒性。

> 注：真正的彻底修法是把 endpoint 签名改成 `async Task<object>`，但 TShock REST 框架是否支持需调研，且会引入更大改动。本任务采用 defensive 路径。

## Decision (ADR-lite)

**Context**：审计 + 主代理 verify 后保留的 3 个真实但范围窄的修复。其他 audit 项（V-1 / V-2 / V-5 / M-2 / M-3 / M-4）经讨论确认在自托管单管理员 threat model 下不构成实际问题，**不修**。

**Decision**：三个修复合并到一个 task / 一个 commit，因为都是审计 follow-up + 都是 plugin / service 内部小改动 + 都不改外部契约。

**Consequences**：
- 优点：消除真实可复现的封禁绕过（V-4）；定时刷盘 + reload 并发场景的数据完整性提升（V-6）；defensive 防御 sync context（V-3）
- 缺点：单 commit 含 3 处独立改动，commit message 略复杂；测试增加几条
- 待评估：长期 V-3 真要解决线程饥饿需把 REST handler 改 async，等 TShock REST 框架升级后再做

## Requirements

### V-4：OnPlayerPostLogin 按 Account.Name 二次校验黑名单 + 白名单

- 在 `OnPlayerPostLogin` 内、调用 `_onlineTimeService?.StartSession` **之前**，按 `args.Player.Account.Name` 调一次黑名单 + 白名单校验
- 黑名单失败 → `args.Player?.Disconnect(blacklistReason)` + WARN 日志 + return（不继续 StartSession / Load 等）
- 白名单失败 → 同样 Disconnect + WARN + return
- WARN 日志措辞需明确"按账号名核验"以与 OnPlayerInfo 阶段日志区分
- 不改 OnPlayerInfo / OnPlayerPreLogin 现有逻辑（不破坏 TShock 标准入服流程）
- 不改 `IBlacklistService` / `IWhitelistService` 接口

### V-6：OnlineTimeService.Reload 改为按账号取最大值合并

- 把 `_records = new Dictionary<string, long>(store.Records);` 替换成 foreach + max merge
- 既有 `_activeSessions` 不动（只动持久化的 `_records`）
- 既有外部行为：`Reload()` 返回值仍是 `OnlineTimeStore`（磁盘读出来的），调用方契约不变
- `IOnlineTimeService.Reload` 接口签名不变

### V-3：VerifyNextBot 加 Task.Run 包裹

- `var result = probe.ProbeAsync(settings).GetAwaiter().GetResult();` 改成 `var result = Task.Run(async () => await probe.ProbeAsync(settings).ConfigureAwait(false)).GetAwaiter().GetResult();`
- 接口签名 / 路由 / 响应字段 / 状态码完全不变

## Acceptance Criteria

### V-4
- [ ] 复现：白名单只允许 `["Alice"]`、账号 `Bob` 存在 → player.Name = "Alice" 进服 → `/login Bob <密码>` → 现在被踢出（白名单按 Bob 校验失败）
- [ ] 复现：黑名单含 `["Alice"]` → player.Name = "AliceAlt" 进服 → `/login Alice <密码>` → 现在被踢出（黑名单按 Alice 校验失败）
- [ ] 现有"player.Name == account.Name"普通流程行为不变（既有测试通过）
- [ ] WARN 日志含"按账号名核验"区分

### V-6
- [ ] start session → flush → reload during simulated IO race → records 累计正确（不丢 flush 增量）
- [ ] 普通无并发 reload → 行为与现状一致

### V-3
- [ ] VerifyNextBot 端点行为不变（response shape / status / fields）
- [ ] probe 抛异常 / 成功 / 超时三种路径仍正确返回

### 通用
- [ ] 现有 314 测试全部通过 + 新增 3-5 条
- [ ] `dotnet build` 0 警告 0 错误
- [ ] 不改任何 REST 路由 / 字段 / 状态码 / 错误文案 / 持久化文件格式

## Definition of Done

- 全部测试 green、build 干净
- 接口签名（公开）零变化
- 行为契约（外部 REST + 持久化路径 + 文件格式 + 日志字段命名）零变化
- spec 合规

## Technical Approach

### V-4：`Plugin/NextBotAdapterPlugin.cs:OnPlayerPostLogin`

```csharp
private void OnPlayerPostLogin(PlayerPostLoginEventArgs args)
{
    var accountName = args.Player?.Account?.Name;

    // 黑名单 / 白名单按账号名二次校验：覆盖 OnPlayerInfo 按 player.Name
    // 漏掉的场景（玩家用不在名单的显示名进服后再 /login 到名单内 / 外的账号）
    if (!string.IsNullOrEmpty(accountName))
    {
        if (_blacklistService is not null && !_blacklistService.TryValidateJoin(accountName, out var blacklistReason))
        {
            PluginLogger.Warn($"账号 {accountName} 登录后被黑名单拒绝（按账号名核验）。");
            args.Player?.Disconnect(blacklistReason ?? "账号已被加入黑名单");
            return;
        }

        if (_whitelistService is not null && !_whitelistService.TryValidateJoin(accountName, out var whitelistReason))
        {
            PluginLogger.Warn($"账号 {accountName} 登录后被白名单拒绝（按账号名核验）。");
            args.Player?.Disconnect(whitelistReason ?? "你不在白名单中");
            return;
        }
    }

    _onlineTimeService?.StartSession(args.Player.Account.Name);
    if (!string.IsNullOrEmpty(accountName))
    {
        _playerExplorationTracker?.ForgetLastSample(accountName);
        _playerExplorationTracker?.Load(accountName);
    }
}
```

注意：
- 二次校验放在 `StartSession` **之前**，避免 disconnect 后留下脏 active session
- accountName null/空则跳过校验（防止 TShock 钩子早期没设 Account 时 NRE；但 PostLogin 阶段 Account 应当一定不为 null）
- TryValidateJoin 返回 true 表示允许（功能未启用时也返回 true）；返回 false 才走 disconnect

### V-6：`Services/UserData/OnlineTimeService.cs`

```csharp
public OnlineTimeStore Reload()
{
    var store = Load();   // IO outside lock，与现有保持

    lock (_lock)
    {
        // Merge 而非完全替换：磁盘版本是某时刻的快照，可能已落后于 in-memory 最新累计。
        // _records 是单调增长的（StartSession/Flush/EndSession 都是累加），按账号取较大值是安全的。
        foreach (var (k, v) in store.Records)
        {
            if (!_records.TryGetValue(k, out var current) || v > current)
                _records[k] = v;
        }
    }

    return store;
}
```

### V-3：`Rest/ConfigEndpoints.cs:VerifyNextBot`

```csharp
public static object VerifyNextBot(PluginConfigService configService, INextBotSessionProbeService probe)
{
    try
    {
        var settings = configService.Load().NextBot;
        var result = Task.Run(async () =>
            await probe.ProbeAsync(settings).ConfigureAwait(false)).GetAwaiter().GetResult();

        // ... rest unchanged ...
    }
    catch (Exception ex)
    {
        PluginLogger.Error($"NextBot 连接验证端点调用失败，原因：{ex.Message}");
        return EndpointResponseFactory.Error(ex.Message, "500");
    }
}
```

`Task.Run` 把 await 挪到独立线程，REST worker 不会被任何 SyncContext 死锁。线程饥饿问题这次不彻底解决，留待 TShock 升级后做 async-all-the-way。

### 测试

- `NextBotAdapter.Tests/PluginAuthGuardsTests.cs`（**新增** 或加到现有最贴近的 plugin 测试）：
  - 直接调 plugin 不可（`[ExcludeFromCodeCoverage]`），改成抽出 `IPostLoginRevalidator` helper static class，在测试里调 helper 验证：
    - `Revalidate_ShouldReturnFalse_WhenAccountInBlacklist`
    - `Revalidate_ShouldReturnFalse_WhenAccountNotInWhitelist`
    - `Revalidate_ShouldReturnTrue_WhenAccountAllowed`
  - **或**直接通过 fake `IBlacklistService` / `IWhitelistService` 在 plugin 层做最小集成测试（此前类似新增过 helper，看现有项目风格）。
  - 如果项目已有可测的 wrapper，复用；如果没有，最小代价是抽 1 个 static helper（可继续 `[ExcludeFromCodeCoverage]` plugin 主体不动）

注意：`Plugin/NextBotAdapterPlugin.cs` 是 `[ExcludeFromCodeCoverage]` 类，传统不直接测；但 V-4 是核心 security 改动。**实施时 implement-agent 自行决定测试粒度**：
  1. 抽 helper static class 单测 → 推荐（最干净，能直接断言"账号在黑名单 → 该被拒"）
  2. 仅依赖 `BlacklistServiceTests` / `WhitelistServiceTests` 已有的 `TryValidateJoin` 覆盖 → 接受（plugin 二次校验是直接 wire 既有方法，不易跑偏）

如果选 1，新建文件 `NextBotAdapter/Plugin/PostLoginAccountGuard.cs`（或类似），把"二次校验"逻辑封装成可测的 static method，plugin `OnPlayerPostLogin` 调用它。

- `NextBotAdapter.Tests/OnlineTimeServiceTests.cs`：
  - `Reload_ShouldMergeAndKeepLargerValues`：start session → flush（让 `_records[Alice] = 100`） → 模拟磁盘上有更老快照（Alice = 80）→ Reload → records[Alice] 仍 = 100（不被磁盘老快照覆盖）
  - `Reload_ShouldAddNewRecords_FromDisk`：磁盘有内存没有的账号 → Reload → 该账号被加进 `_records`
  - 现有 reload 测试（如有）应当继续 pass

- `NextBotAdapter.Tests/RestEndpointLogicTests.cs` 或 ConfigEndpoints 测试：
  - VerifyNextBot 已有契约测试（如成功 / 失败 / 异常）继续 pass。`Task.Run` 包裹是内部实现细节，不需要新增测试。

## Out of Scope

- 不修 V-1（self-hosted 下 admin 读自己 token 等价 cat 配置文件）
- 不修 V-2（self-hosted 下 admin 通过 API 改配置 = vim 改配置文件）
- 不修 V-5（KnownIps race，发生概率极低）
- 不修 M-2 / M-3 / M-4（经核实严重度被高估或属可接受设计权衡）
- 不改 `IBlacklistService` / `IWhitelistService` / `IOnlineTimeService` / `INextBotSessionProbeService` 接口签名
- 不改 REST 路由 / 端点 / 响应字段 / 状态码 / 错误文案
- 不改持久化文件格式 / 路径
- 不动 `OnPlayerPreLogin` / `OnPlayerInfo` / `OnNetGreetPlayer` / `OnServerLeave` / `OnPlayerUpdate` / `Dispose`
- 不动 `docs/REST_API.md` / `docs/CONFIGURATION.md`（外部行为零变化）
- 不把 VerifyNextBot 改 async 端点（短期 defensive 修，长期再单独立任务）

## Technical Notes

### 涉及文件

- 产品代码（**修改**）：
  - `NextBotAdapter/Plugin/NextBotAdapterPlugin.cs`（OnPlayerPostLogin 加二次校验）
  - `NextBotAdapter/Services/UserData/OnlineTimeService.cs`（Reload 改 merge）
  - `NextBotAdapter/Rest/ConfigEndpoints.cs`（VerifyNextBot 加 Task.Run 包裹）
- 产品代码（**可能新增**，由 implement-agent 决定）：
  - `NextBotAdapter/Plugin/PostLoginAccountGuard.cs`（如果选静态 helper 方便测试，可放 Plugin/ 下；放 Services/Security/ 也可）
- 测试：
  - `NextBotAdapter.Tests/OnlineTimeServiceTests.cs`（reload merge 测试）
  - `NextBotAdapter.Tests/PostLoginAccountGuardTests.cs`（如抽了 helper）

### 不需要改

- `IBlacklistService` / `BlacklistService.cs`
- `IWhitelistService` / `WhitelistService.cs`
- `IOnlineTimeService` 接口（仅实现类内部改）
- `INextBotSessionProbeService` / `NextBotSessionProbeService.cs`
- 路由 / 权限常量 / 注册器
- 渲染服务 / leaderboard / exploration tracker
- 持久化路径 / 文件格式 / SanitizeFileName
- `docs/`

### 并发模型

- V-4 修复：在 PostLogin 钩子（与 OnPlayerInfo 同 TCP 接收线程）调 service 的 `TryValidateJoin`，service 内部已有 `_lock` 保护。无新并发风险。
- V-6 修复：merge 仍在 `_lock` 内；IO 仍在 lock 外（与现有约定一致）
- V-3 修复：`Task.Run` 把 await 挪到 thread pool 另一线程；REST worker 仍 sync 等待，但与原代码相比无新风险

### Future Evolution（不在本任务做）

- VerifyNextBot 改 `async Task<object>` 端点（需 TShock 框架支持）
- 抽更通用的"identity validator"（覆盖 user.Account.Name 在多种钩子点的统一校验）
- KnownIps race 修复（V-5）
