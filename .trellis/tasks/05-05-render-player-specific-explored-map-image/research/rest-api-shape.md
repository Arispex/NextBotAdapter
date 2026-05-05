# REST API Shape — Player-Filtered Map Image

- **Query**: 在 `/world/map/image` 上加玩家视角维度，应该用 query param、新路由（玩家命名空间）、还是新独立 endpoint？
- **Scope**: mixed（项目内部惯例 + 业界最佳实践）
- **Date**: 2026-05-05

> **路由前缀澄清**：本插件所有路由都挂在 `/nextbot/...` 下，资源段统一 kebab-case 单词（`map-image`、`map-file`、`fishing-quests`、`online-time`、`verify-nextbot`），不是用户问题里写的 `/world/map/image`。下文 URL 都按项目实际形态写。

## Conclusion (TL;DR)

**推荐候选 A 的变体：在现有 `GET /nextbot/world/map-image` 上加可选 query param `player`，以"全图 vs 玩家视角"作为同一资源的过滤维度。**

理由：地图图片是单一资源（"当前世界这一刻的地图渲染产物"），玩家视角只是渲染时叠加的一个 *filter*，不是新资源；这与项目里 `/whitelist?...`、`/blacklist?reason=...`、`/config/update?<dotted.path>=<v>` 这一系列"用 query 传过滤 / 修饰参数"的既有惯例一致，且向后兼容。

最终建议形态：

```
GET /nextbot/world/map-image                    # 现状：服务端全图视角（保持 200，不破坏）
GET /nextbot/world/map-image?player={user}      # 新增：以 {user} 探索数据作为可见性掩码
```

## Project Internal Conventions

依据 `NextBotAdapter/Infrastructure/EndpointRoutes.cs:1-26`、`NextBotAdapter/Rest/EndpointRegistrar.cs:1-42`、`docs/REST_API.md`，归纳出本项目的路由风格：

### 1. 资源段统一 kebab-case，不嵌套二级斜杠

所有"复合名词"用一段 kebab-case，而不是 `/world/map/image` 这种连续两段：

| 路由 | 来源 |
|---|---|
| `/nextbot/world/map-image` | `EndpointRoutes.WorldMapImage` (line 8) |
| `/nextbot/world/map-file` | `EndpointRoutes.WorldMapFile` (line 10) |
| `/nextbot/world/world-file` | `EndpointRoutes.WorldFile` (line 9) |
| `/nextbot/world/progress` | `EndpointRoutes.WorldProgress` (line 7) |
| `/nextbot/leaderboards/fishing-quests` | line 22 |
| `/nextbot/leaderboards/online-time` | line 23 |
| `/nextbot/config/verify-nextbot` | line 20 |

**含义**：候选 C（`/world/map/image/player/{username}`）和用户问题里写的 `/world/map/image` 都不符合这个风格。新路由若用，应是 `/nextbot/world/map-image-explored` 这种单段 kebab，而不是再拆斜杠。

### 2. `{user}` 路由参数仅用于"以玩家为主资源"的端点

下列端点把玩家放在 path 上，因为玩家本身就是被操作 / 查询的主资源：

| 路由 | 主资源含义 |
|---|---|
| `/nextbot/users/{user}/inventory` | "这个玩家的背包"（背包属于玩家） |
| `/nextbot/users/{user}/stats` | "这个玩家的属性"（属性属于玩家） |
| `/nextbot/whitelist/add/{user}` | 对该玩家执行 add 动作 |
| `/nextbot/whitelist/remove/{user}` | 对该玩家执行 remove 动作 |
| `/nextbot/blacklist/add/{user}` | 同上 |
| `/nextbot/blacklist/remove/{user}` | 同上 |
| `/nextbot/security/confirm-login/{user}` | 对该玩家的登入请求执行确认 |
| `/nextbot/security/reject-login/{user}` | 对该玩家的登入请求执行拒绝 |

**注意**：`/users/{user}/inventory` 和 `/users/{user}/stats` 返回的是**玩家的存档数据**（背包数组、属性整数），不是"以玩家为视角的世界资源"。地图图片不属于玩家——它是世界的快照——只是渲染时按玩家的探索掩码裁剪可见性，所以不天然贴合 `/users/{user}/...` 的语义。

### 3. Query param 用于附加过滤 / 修饰

```
GET  /nextbot/blacklist/add/{user}?reason=使用外挂&token=...   # blacklist add 必带 reason
GET  /nextbot/config/update?whitelist.enabled=false&...         # 多字段过滤更新
GET  /nextbot/<any>?token=<token>                               # 全局：所有端点用 token query 鉴权
```

`docs/REST_API.md` 显式声明（line 3-8）：

> 所有请求均需携带 `token` 查询参数；查询操作直接返回平铺字段。

这说明项目接受 query 作为正式参数通道（不是只能用 path），并且查询响应是**平铺**而非 envelope。

### 4. 响应平铺、动作返回 `{response}`、错误返回 `{error}`

`MapEndpoints.cs:21-31` 现有响应是 `{ "fileName": "...", "base64": "..." }` 平铺两字段，**不带** `data` envelope。新接口若加 player 过滤，应保持同一 schema（玩家视角的图也是 `fileName + base64`），不要为玩家视角改 envelope。

## Industry Best Practices

### Microsoft REST API Guidelines（[github.com/microsoft/api-guidelines](https://github.com/microsoft/api-guidelines)）

- **Filtering 用 query param，不用新路径**：`GET /products?status=active`，而不是 `/products/active`
- **可选维度 → 加 query 参数；维度本身就是新资源 → 加 sub-path**
- 玩家视角是"对同一张图加可见性掩码"，是过滤而不是新资源 → 推荐 query

### Stripe API（[stripe.com/docs/api](https://stripe.com/docs/api)）

- 资源始终是名词复数：`/v1/customers`、`/v1/charges/{id}/refunds`
- 列表用 query 过滤：`/v1/charges?customer=cus_xxx&created[gte]=...`
- 同一个资源（charges）在不同场景共用一个 endpoint，靠 query 切片，**不**为每个 customer 单独建一个新路径

### GitHub REST API（[docs.github.com/en/rest](https://docs.github.com/en/rest)）

- 当 actor 真的拥有 / 创建了资源 → sub-resource：`/users/{user}/repos`（仓库本身归这个用户）
- 当只是"按某 actor 过滤一个全局资源" → query：`/search/issues?q=author:{user}+state:open`

地图图片归属"world"，不归属玩家。它更像 GitHub 里的 `/search/...?author={user}`（按玩家裁剪可见性），而不是 `/users/{user}/repos`（玩家拥有的仓库）。

### `api-design` skill（`~/.claude/skills/api-design/SKILL.md`）

直接引用相关规则：

- 第 25-41 行："Resources are nouns" + "Sub-resources for relationships" + "Actions that don't map to CRUD use verbs sparingly"
- 第 252-265 行 "Filtering"：明确推荐 `GET /api/v1/orders?status=active&customer_id=abc-123` 用 query。`customer_id` 是"按 customer 过滤 orders"——和"按 player 过滤 map-image"是同构问题。
- 反例：`/api/v1/getUsers`（动词在 URL）、`/api/v1/users/123/getOrders`（嵌套路径里塞动词）。候选 C 的 `/world/map/image/player/{username}` 接近这种"路径里塞维度名词"的反模式，因为 "player" 在这里不是子资源而是过滤键。

## Comparison

| Approach | URL（修正成项目风格） | Pros | Cons | Fit with project |
|---|---|---|---|---|
| **A** Query param on existing endpoint | `GET /nextbot/world/map-image?player={user}` | 向后兼容（不带 player → 现有全图行为）；与 blacklist/add `?reason=` 和 config/update `?path=v` 的 query 用法一致；前端只需在同一 SDK 方法上加可选参数；Microsoft / Stripe / api-design skill 都把"按 X 过滤同一资源"归到 query | 没有显式声明"全图 vs 玩家视角"是两种渲染管线，调用方需读文档才能知道行为差异；缓存层若按 URL hash 缓存，需要把 query 也纳入 cache key（HTTP 缓存层默认会，自定义层要确认） | **最佳**：完全贴合现有路由风格 + 现有 query 习惯 + 不破坏既有契约 |
| **B** Sub-resource on user | `GET /nextbot/users/{user}/map-image` | 路径直观说明"这是该玩家相关的图"；与 `/users/{user}/inventory`、`/users/{user}/stats` 形态对称；权限自然能挂到 `nextbot.users.map_image` | 语义偏差：地图图片不归玩家，只是用玩家数据做掩码——和 inventory / stats（玩家自己的存档数据）不是同一类资源；未来要支持"无玩家全图"时仍要保留 `/world/map-image`，等于双入口；不便扩展到多玩家并集（`/users/{user1}/map-image` 怎么塞 user2？） | **次佳**：风格上贴合项目对玩家子资源的命名，但语义上把"world 的渲染产物"绑到 user 命名空间，长期会别扭 |
| **C** Player-namespace under map | `GET /nextbot/world/map-image/player/{user}`<br>or `/nextbot/world/map-image-by-player/{user}` | 显式表明这是"玩家视角的 map-image" | 不符合项目"复合名词用单段 kebab + 不再嵌套斜杠"的惯例（map-image 之后再拆 `/player/...` 两段，没有任何同类先例）；路径里出现 "player" 当过滤维度名，接近 api-design skill 列出的反模式（`/users/123/getOrders` 把动作 / 维度名塞进路径）；列表入口 `/world/map-image/player` 没有合理语义（不可能列出"所有玩家视角的图"） | **不推荐**：与现有命名风格冲突，且没有 query 形态拿不到的好处 |
| **D** New independent endpoint | `GET /nextbot/world/map-image-explored?player={user}` | 显式区分"全图"与"已探索图"，未来若两条管线分歧很大（不同响应字段、不同权限粒度）时清晰；权限可拆 `nextbot.world.map_image` vs `nextbot.world.map_image_explored` | 多一条路由要维护；调用方要选两个 endpoint；但响应 schema 完全相同（`fileName + base64`）→ 没有拆分价值；与候选 A 的差异只剩"是否复用同一 endpoint"，本质是同一资源 | **可作为 fallback**：仅当未来响应 schema 真的要分裂时才升级到 D；当前没有这种分裂迹象 |

## Side concerns

### 1. URL encoding / 特殊字符

- 当前已上线端点 `/users/{user}/inventory` 等都是把玩家名直接放 path，TShock REST 框架本身负责 percent-decode。Terraria 玩家名允许字符相对受限（无 `/`、无控制字符），实测项目里也没出现因为玩家名特殊字符导致路由匹配失败的报告。
- 把玩家名放 query (`?player=...`) 反而**更安全**：`?` 后的内容不参与路径匹配，路径遍历 (`../`) 类风险天然不存在；只需做 URL decode 即可。
- 结论：候选 A 在编码安全性上 **≥** 候选 B/C。

### 2. 客户端 SDK 代码生成 / 缓存

- 候选 A：单一方法 `getMapImage(player?: string)`，optional 参数贴合 SDK 风格；HTTP 缓存按完整 URL（含 query）作 key，标准行为，无需特殊配置。
- 候选 B：单独方法 `getUserMapImage(user)`，调用方要决定调哪一个；但同样可工作。
- 候选 C：要么手写、要么生成出形态怪异的方法名；不推荐。

### 3. 权限粒度

- 候选 A 推荐复用 `nextbot.world.map_image`（已存在，见 `EndpointRoutes.WorldMapImage` + `Permissions.WorldMapImage`），因为是"同一资源的过滤"。
- 若想让 "看自己探索图" 与 "看全图" 是不同权限（产品上合理：上帝视角全图属于运维，玩家视角属于玩家自己/Bot 转发），可以在同一 endpoint 内根据是否带 player 走不同权限检查，或拆成候选 D。这一点单独决策，不阻塞路由形态。

### 4. 可扩展性场景

| 未来需求 | 候选 A 的扩展形态 | 其他候选的代价 |
|---|---|---|
| 多玩家视角并集 ("Alice ∪ Bob 探索过的") | `?player=Alice,Bob`（同 api-design skill 第 261 行 "Multiple values comma-separated"） | B 路径里只能放一个 user；C 同；D 也得退化成 query |
| 反向（"哪些玩家探索过坐标 (x,y)"） | 这是**完全不同的资源**（"覆盖了某坐标的玩家集合"），应用新 endpoint 如 `/nextbot/world/explored-by?x=...&y=...`，不论本期选哪个候选都不影响 | 同 |
| 探索掩码导出（不渲染颜色，只导出 bitmap） | 新 endpoint `/nextbot/world/explored-mask?player={user}`；与本期端点正交 | 同 |

候选 A 在扩展性上对"多玩家并集"场景天然友好（comma-separated query），其他候选要么改路径、要么也退化成 query。

## Recommendation

**采用候选 A**：

```
GET /nextbot/world/map-image                  # 既有：全图（保持兼容）
GET /nextbot/world/map-image?player={user}    # 新增：玩家视角
```

实现要点（路由层，仅决定接口形状）：

1. `EndpointRoutes.WorldMapImage` 不动（line 8 不变）。
2. `MapEndpoints.Image` 读取 query 中可选的 `player`：
   - 不带 → 走现有 `Service.Generate()`，全图行为不变
   - 带 → 走新增 `Service.GenerateForPlayer(user)`（具体可行性见 `prd.md` Open Question #1/#2 的后续 research）
3. `Permissions.WorldMapImage` 复用；如产品要求拆权限再拆。
4. `docs/REST_API.md` 在 `### GET /nextbot/world/map-image` 段补充 `player` 参数表格行，以及"带 player 时未探索区域返回黑色"的语义说明，错误表新增 `User was not found.`。
5. 玩家名从 query 读取后必须 trim + 非空检查（参见 `UserEndpoints.cs:16-19` 的现有模式 `EndpointResponseFactory.MissingUser()`），确保与 `/users/{user}/inventory` 一致的错误行为。

仅当后续发现"玩家视角"和"全图"在响应字段上发生分裂（例如玩家视角要额外返回探索百分比、覆盖坐标边界）时，再升级到候选 D（独立 endpoint）。当前无此迹象。

## References

- `NextBotAdapter/Infrastructure/EndpointRoutes.cs:1-26`（项目所有路由常量）
- `NextBotAdapter/Rest/EndpointRegistrar.cs:1-42`（路由注册）
- `NextBotAdapter/Rest/MapEndpoints.cs:11-32`（现有 map-image 实现）
- `NextBotAdapter/Rest/UserEndpoints.cs:11-58`（玩家 sub-resource 模式参考）
- `docs/REST_API.md`（对外契约风格、查询响应平铺约定 line 3-8、map-image 文档 line 157-181）
- `~/.claude/skills/api-design/SKILL.md` line 25-56（URL Structure / Naming Rules）、line 252-265（Filtering）
- Microsoft REST API Guidelines: <https://github.com/microsoft/api-guidelines/blob/vNext/Guidelines.md>
- Stripe API conventions: <https://stripe.com/docs/api>
- GitHub REST API: <https://docs.github.com/en/rest>

## Caveats / Not Found

- 是否真的能拿到指定玩家的 explored bitmap（vanilla server / TShock 是否持久化）—— 这是 `prd.md` 的 Open Question #1，**不在本研究范围内**。本研究只回答"如果能做出来，接口应该长什么样"。
- 权限是否拆分（`map_image` vs `map_image.player`）属于产品策略问题，不是路由形态问题，留给主 agent 与用户协商。
- 没有专门的 spec 文件约束 REST 路由形态（`.trellis/spec/backend/` 下没有 `api-design.md`）；本研究的项目惯例完全是从 `EndpointRoutes.cs` + `REST_API.md` 反推归纳的。
