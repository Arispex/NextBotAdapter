# fix: 玩家探索数据按账号名为 key 而非客户端 UUID

## Goal

修复玩家探索 bitmap 用错 key 导致的数据污染与丢失问题：当前实现以 `player.Account.UUID` 作为 bitmap 的存储 key，但 TShock 的 `UserAccount.UUID` 字段实际存的是"最后一次成功登入该账号时客户端送来的 UUID"，本质等同于设备指纹。结果就是同设备多账号会互相覆盖 bitmap，账号换设备后 bitmap 失联成孤儿。本任务把内部存储 key 从"设备 UUID"改为账号名（`Account.Name`）。

## What I already know

### 当前实现（有 bug）

- `NextBotAdapterPlugin.cs:209`：`OnPlayerPostLogin` 用 `args.Player.Account.UUID` 调 `_playerExplorationTracker.Load(uuid)`
- `NextBotAdapterPlugin.cs:224`：`OnServerLeave` 用 `player.Account.UUID` 调 `Save / ForgetLastSample`
- `NextBotAdapterPlugin.cs:247`：`OnPlayerUpdate` 用 `player.Account.UUID` 调 `MarkAtPosition`
- `NextBotAdapterPlugin.cs:574`：`PerformAutoLogin` 调 `TShock.UserAccounts.SetUserAccountUUID(account, player.UUID)`，每次自动登录都把账号上的 UUID 字段覆盖成当前客户端 UUID
- TShock 内置 UUID 自动登入（启用时）也会写 `Account.UUID`
- bitmap 持久化路径：`tshock/NextBotAdapter/Explored/{worldId}/{accountUuid}.bin`

### 失败模式

1. **同设备多账号互相污染**：设备 D 登账号 A 后 `A.UUID = D`，写文件 `D.bin`；同设备登 B 后 `B.UUID` 也被覆盖为 `D`，B 上线 `Load(D)` 读到 A 的 bitmap，B 走路 stamp 也写到同一个 `D.bin`。
2. **换设备记录丢失**：账号 A 在设备 D1 探索后 `A.UUID = D1`、文件 `D1.bin`；换到设备 D2 登 A，`A.UUID` 被覆盖为 `D2`，`Load(D2)` 文件不存在 → 全黑；旧 `D1.bin` 成孤儿。

### REST 端点

- `/users/{user}/map-image` 中 `{user}` 路由参数的语义就是账号名（与 `/users/{user}/inventory`、`/users/{user}/stats` 一致），与 bitmap key 改成 `Account.Name` 一致，无需改路由语义。
- `IUserAccountLookup.TryGetAccountUuid(user, out accountUuid)` 是该路径上唯一会返回"UUID 字符串"的接口，目前在 `UserEndpoints.MapImage` 唯一使用。
- `UserEndpoints.MapImage` 当前流程：`lookup.TryGetAccountUuid(trimmedUser, out var accountUuid)` → `tracker.GetBitmap(accountUuid)`。修复后改为：先确认账号存在并取得规范化 `Account.Name`，再用账号名取 bitmap。

### 测试现状

- `PlayerExplorationTrackerTests.cs` / `FileExplorationStorageTests.cs` 现有测试只在内部用字符串作为 key 比对，并不依赖该字符串是 UUID。
- `UserEndpointsTests.cs` 里 `FakeAccountLookup` 实现了 `TryGetAccountUuid` 接口；接口若被重命名，测试中的 fake 也要同步。

## Requirements

- 玩家探索 bitmap 的存储 key 改为 `player.Account.Name`，3 处事件回调全部跟改。
- `PlayerExplorationTracker` / `IPlayerExplorationTracker` / `FileExplorationStorage` / `IExplorationStorage` 的方法参数命名从 `accountUuid` 改为 `accountName`，避免误导后续读者。
- `IUserAccountLookup.TryGetAccountUuid` 重命名为 `TryGetAccountName`，返回值语义改为账号规范化名（`account.Name ?? string.Empty`）；`TShockUserAccountLookup` 与 `UserEndpoints.MapImage` 跟改。
- `UserEndpoints.MapImage` 修复后：`lookup.TryGetAccountName(trimmedUser, out var accountName)` 通过后用 `accountName` 取 bitmap，外部行为（响应字段、状态码、错误文案）保持不变。
- 持久化路径仍是 `Explored/{worldId}/{key}.bin`，只是 `{key}` 内容由设备 UUID 字面变成账号名（经 `Path.GetInvalidFileNameChars` 清洗）。
- 不做数据迁移：旧 `{deviceUuid}.bin` 文件由用户在测试环境手动清理。

## Acceptance Criteria

- [ ] 同设备依次登录账号 A、账号 B，两者各自的 bitmap 完全独立，互不覆盖、互不读到对方数据。
- [ ] 同一账号 A 在设备 D1 探索后切到设备 D2 登入，bitmap 数据保留可加载。
- [ ] `GET /nextbot/users/{user}/map-image` 用账号名查询，返回的 bitmap 与该账号长期持久化数据一致；接口的响应结构（`fileName`、`base64`）、状态码、错误文案均无变化。
- [ ] `PlayerExplorationTracker` / `FileExplorationStorage` / `IUserAccountLookup` 的参数与方法命名不再出现 "uuid"（命名清晰度）。
- [ ] 现有 `PlayerExplorationTrackerTests` / `FileExplorationStorageTests` / `UserEndpointsTests` 调整后全部通过；新增至少 1 条测试覆盖 `IUserAccountLookup` 的 fake 在 `UserEndpoints.MapImage` 路径下按账号名取 bitmap。

## Definition of Done

- 单元测试新增 / 调整后全部通过，整套测试套件 green。
- `dotnet build` 无 warning，无 error。
- bitmap 持久化路径仍是 `Explored/{worldId}/{key}.bin`，文件名清洗逻辑保留。
- `docs/REST_API.md` 不需要更新（外部 API 行为不变）；如有注释 / 内部文档涉及 "UUID 是 bitmap key" 的描述，同步修正。

## Technical Approach

1. **接口与实现命名统一**
   - `IPlayerExplorationTracker`：`accountUuid` 参数 → `accountName`
   - `IExplorationStorage`：`accountUuid` 参数 → `accountName`
   - `PlayerExplorationTracker` / `FileExplorationStorage` 跟改字段、本地变量
   - `IUserAccountLookup.TryGetAccountUuid` → `TryGetAccountName`（返回 `account.Name`）
   - `TShockUserAccountLookup` 实现跟改

2. **3 处 plugin 事件回调改用 `Account.Name`**
   - `OnPlayerPostLogin`、`OnServerLeave`、`OnPlayerUpdate`

3. **REST 端点 `UserEndpoints.MapImage` 跟改**
   - `lookup.TryGetAccountName(trimmedUser, out var accountName)` → `tracker.GetBitmap(accountName)`
   - 错误响应、日志保持不变（user 标识仍写 `trimmedUser`，便于运维核对）

4. **测试调整**
   - `UserEndpointsTests.FakeAccountLookup`：实现新方法名，元组从 `(Name, Uuid)` 改成 `(Name, ResolvedName)`（虽然此处 `ResolvedName` 通常 == `Name`）
   - `PlayerExplorationTrackerTests` / `FileExplorationStorageTests`：fake 参数命名跟改（不强求，但保持一致更好）
   - 新增覆盖：`UserEndpoints.MapImage` 在用账号名查到 bitmap、未查到时返回正确响应（已有覆盖；只需确认重命名后语义未变）

## Decision (ADR-lite)

**Context**：bitmap 现以 `Account.UUID` 当 key，但 TShock 的该字段会被自动登录或本插件每次登录覆盖成当前客户端 UUID（设备指纹），引发同设备多账号污染、换设备记录丢失。

**Decision**：把存储 key 从 `Account.UUID` 改为 `Account.Name`，与本项目其他业务（在线时长、库存查询、REST `/users/{user}/...`）共用账号名作长期身份。同步把 `IUserAccountLookup.TryGetAccountUuid` 改成 `TryGetAccountName`、调用方与测试 fake 跟改，避免接口名继续误导。

**Consequences**：
- 优点：bitmap 与账号绑定，符合"每个账号一份探索记录"的语义；与项目里其他用账号名作 key 的服务保持一致；接口名表达准确。
- 缺点：账号改名场景（TShock 数据库直改）会让旧 bitmap 成孤儿；目前项目没有改名工具，此场景概率极低。
- 待评估：未来若出现账号大规模改名需求，可改用 `Account.ID`（数据库主键不变），目前不做。

## Out of Scope

- 不做数据迁移：旧 `{deviceUuid}.bin` 由用户在测试环境手动清理。
- 不改 REST 路由 / 响应结构 / 错误码 / 日志字段。
- 不改 reveal box / 插值 / 瞬移阈值等渲染参数。
- 不改 bitmap 持久化目录结构（仍是 `Explored/{worldId}/{key}.bin`）。
- 不引入新的"长期账号身份" concept（不切到 `Account.ID`，不增加额外身份映射表）。
- 不针对 `SetUserAccountUUID` 行为做任何改动（不抑制它对 `Account.UUID` 的覆盖）。

## Technical Notes

### 涉及文件

- 产品代码：
  - `NextBotAdapter/Plugin/NextBotAdapterPlugin.cs`（3 处事件回调改用 `Account.Name`）
  - `NextBotAdapter/Services/Exploration/IPlayerExplorationTracker.cs`（参数重命名）
  - `NextBotAdapter/Services/Exploration/PlayerExplorationTracker.cs`（实现跟改）
  - `NextBotAdapter/Services/Exploration/IExplorationStorage.cs`（参数重命名）
  - `NextBotAdapter/Services/Exploration/FileExplorationStorage.cs`（实现跟改）
  - `NextBotAdapter/Services/UserData/IUserAccountLookup.cs`（方法重命名为 `TryGetAccountName`）
  - `NextBotAdapter/Services/UserData/TShockUserAccountLookup.cs`（实现跟改，返回 `account.Name`）
  - `NextBotAdapter/Rest/UserEndpoints.cs`（`MapImage` 调用 `TryGetAccountName`）

- 测试：
  - `NextBotAdapter.Tests/UserEndpointsTests.cs`（FakeAccountLookup 跟改）
  - `NextBotAdapter.Tests/PlayerExplorationTrackerTests.cs`（fake 参数命名可选跟改）
  - `NextBotAdapter.Tests/FileExplorationStorageTests.cs`（参数命名可选跟改）

### 不需要改

- `docs/REST_API.md`：外部 API 表现不变。
- `CONFIGURATION.md` / 路由 / 权限常量：均无关。
- `MapRenderMutex` / `MapImageService` / `PlayerMapImageService`：无关。

### 文件名清洗

`FileExplorationStorage.SanitizeFileName` 已基于 `Path.GetInvalidFileNameChars()` 清洗。账号名通常受 TShock 校验，含非法字符的概率低；保留现有清洗逻辑即可。

### Future Evolution（不在本任务做）

- 若日后 TShock 改名场景增多，可考虑切到 `Account.ID`（数据库主键）作长期身份并维护 `Account.Name` 作展示标签。
- 若需要对 `Account.UUID` 设备指纹另作业务（例如多设备登录提醒），与本任务的 bitmap key 解耦，互不影响。
