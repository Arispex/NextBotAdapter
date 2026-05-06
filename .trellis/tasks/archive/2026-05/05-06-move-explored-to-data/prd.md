# refactor: 把 Explored 目录移动到 Data 子目录下

## Goal

把玩家探索 bitmap 的持久化目录从 `tshock/NextBotAdapter/Explored/{worldId}/{accountName}.bin` 移到 `tshock/NextBotAdapter/Data/Explored/{worldId}/{accountName}.bin`，与项目里其他持久化文件（`Data/Whitelist.json`、`Data/Blacklist.json`）保持一致的目录结构。

## What I already know

### 当前状态

- 唯一的 `Explored` 字面量在 `NextBotAdapter/Plugin/NextBotAdapterPlugin.cs:60`：
  ```csharp
  var explorationStorage = new FileExplorationStorage(
      Path.Combine(_configService.ConfigDirectoryPath, "Explored"),
      () => Main.worldID);
  ```
- `PluginConfigService` 已有 `DataDirectoryPath => Path.Combine(ConfigDirectoryPath, "Data")` 公开属性（`PluginConfigService.cs:32`）
- `EnsureConfigComplete()` 会自动 `Directory.CreateDirectory(DataDirectoryPath)`（`PluginConfigService.cs:145`），插件启动时已被调用——`Data/` 目录在 plugin 启动后必定存在
- `FileExplorationStorage.Save` 已经会 `Directory.CreateDirectory` 父目录，所以 `Data/Explored/{worldId}/` 会按需自动创建
- 已有 `Data/` 用例：
  - `WhitelistService` → `Data/Whitelist.json`
  - `BlacklistService` → `Data/Blacklist.json`
- 文档残留：`docs/REST_API.md:138` 仍然写着旧路径 `tshock/NextBotAdapter/Explored/{worldId}/{accountUuid}.bin`——既要改前缀，也要把上一个 task 已修但 doc 漏改的 `accountUuid` 同步成 `accountName`

### 影响范围

- 1 行 C# 代码（`NextBotAdapterPlugin.cs:60`）
- 1 处文档（`docs/REST_API.md:138`）
- 不影响任何接口（`IExplorationStorage` / `IPlayerExplorationTracker` / REST 路由 / 响应字段 / 错误码 / 错误文案 / 日志字段命名）

## Requirements

- `NextBotAdapterPlugin.cs:60` 把 `_configService.ConfigDirectoryPath` 改为 `_configService.DataDirectoryPath`，目录字面量 `"Explored"` 不变
- `docs/REST_API.md:138`：路径文案改为 `tshock/NextBotAdapter/Data/Explored/{worldId}/{accountName}.bin`（同时修上一任务遗漏的 `accountUuid` → `accountName`）
- `Data/Explored/{worldId}/` 目录由 `FileExplorationStorage.Save` 现有的 `CreateDirectory` 兜底创建，无需额外预创建
- 不写自动迁移逻辑——旧 `Explored/{worldId}/*.bin` 由 user 在测试环境手动清理

## Acceptance Criteria

- [ ] 服务端启动后，新玩家探索数据写入路径 = `tshock/NextBotAdapter/Data/Explored/{worldId}/{accountName}.bin`
- [ ] 玩家登录时 `Load` 路径同步指向新位置（旧 `Explored/` 文件被忽略）
- [ ] `dotnet build` 0 警告 0 错误
- [ ] `dotnet test` 全部通过（267 测试 + 视情况新增）
- [ ] `docs/REST_API.md:138` 文案更新到位（路径前缀 + accountName 修正）

## Definition of Done

- 现有所有测试通过
- 不改 REST 路由 / 响应结构 / 错误码 / 错误文案 / 日志字段
- 文档与代码一致（文档说什么路径，代码就写到哪）

## Technical Approach

```csharp
// NextBotAdapterPlugin.cs:59-61 修改前
var explorationStorage = new FileExplorationStorage(
    Path.Combine(_configService.ConfigDirectoryPath, "Explored"),
    () => Main.worldID);

// 修改后
var explorationStorage = new FileExplorationStorage(
    Path.Combine(_configService.DataDirectoryPath, "Explored"),
    () => Main.worldID);
```

`docs/REST_API.md:138` 文案：

```
数据按 (世界 ID, 账号名) 持久化于 `tshock/NextBotAdapter/Data/Explored/{worldId}/{accountName}.bin`，玩家上线 / 下线 / 插件卸载时自动加载与保存。
```

测试侧面：
- 现有 `PlayerExplorationTrackerTests` / `FileExplorationStorageTests` 不依赖具体目录前缀，无需改
- 不需要新增测试——这是纯路径前缀重构，行为不变；现有 testing 已覆盖 Save / Load / 文件名清洗 / lazy-load

## Decision (ADR-lite)

**Context**：`Explored/` 目录目前直接挂在 `ConfigDirectoryPath/`（与配置主文件 `NextBotAdapter.json` 同级），而项目里其他持久化数据（白名单 / 黑名单）都放在 `Data/` 子目录下。这种位置不一致使新进 contributor 难以快速定位"哪些是配置、哪些是运行时数据"。

**Decision**：把 `Explored` 移到 `Data/Explored`，与 `Data/Whitelist.json` / `Data/Blacklist.json` 保持一致；通过复用现有 `_configService.DataDirectoryPath`，避免硬编码。

**Consequences**：
- 优点：目录结构一致；新增持久化数据时有明确归属；语义更清晰（Config = 用户写的配置；Data = 插件写的运行时数据）
- 缺点：需要 user 手动清理旧 `Explored/` 文件（测试环境，user 一贯如此处理）
- 待评估：未来若加持久化的"在线时长"或"统计快照"，应当放在 `Data/` 下；这是惯例的早期投资

## Out of Scope

- 不写迁移逻辑（user 测试环境自己清理）
- 不改其他持久化文件位置（`NextBotAdapter.json` 仍在 `ConfigDirectoryPath/` 根；white/blacklist 已在 `Data/`）
- 不改 `IExplorationStorage` / `FileExplorationStorage` 接口或行为
- 不改 `PluginConfigService.DataDirectoryPath` 的定义
- 不改 `docs/CONFIGURATION.md`（不涉及配置项）
- 不改 reveal box / 探索 stamp / 渲染逻辑
- 不改 REST 路由 / 响应字段 / 状态码 / 日志字段命名

## Technical Notes

### 涉及文件

- 产品代码：`NextBotAdapter/Plugin/NextBotAdapterPlugin.cs`（第 60 行 1 字段名替换）
- 文档：`docs/REST_API.md`（第 138 行 1 句话改写）

### 不需要改

- `IPlayerExplorationTracker` / `PlayerExplorationTracker` / `IExplorationStorage` / `FileExplorationStorage`
- `UserEndpoints.cs` / 路由 / 权限常量
- 任何测试文件
- `docs/CONFIGURATION.md`

### Future Evolution

- 任何未来插件持久化数据（玩家统计、leaderboard 快照等）应默认放 `Data/<feature>/...`，与本次修复对齐
