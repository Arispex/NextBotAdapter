# NextBotAdapter REST API

所有端点遵循 TShock REST API 规范：

- 所有请求均需携带 `token` 查询参数（有效的 TShock REST 令牌）
- 查询操作直接返回平铺字段
- 写入操作返回 `{ "response": "..." }`
- 错误响应返回 `{ "error": "..." }`

---

## 鉴权

所有端点均需携带具有对应权限的 TShock REST 令牌，通过查询参数传递：

```
GET /nextbot/users/Arispex/inventory?token=<token>
```

---

## Users（用户）

### GET `/nextbot/users/{user}/inventory`

返回指定用户的存档背包数据。

**权限：** `nextbot.users.inventory`

**参数**

| 名称   | 位置       | 说明             |
|--------|------------|------------------|
| `user` | 路由参数   | 要查询的用户名   |

**响应 200**

```json
{
  "items": [
    {
      "slot": 0,
      "netId": 4,
      "stack": 1,
      "prefixId": 82
    }
  ]
}
```

| 字段              | 类型    | 说明                              |
|-------------------|---------|-----------------------------------|
| `items`           | array   | 背包物品列表                      |
| `items[].slot`    | integer | 背包槽位索引                      |
| `items[].netId`   | integer | 物品 net ID（Terraria 物品类型）  |
| `items[].stack`   | integer | 物品数量                          |
| `items[].prefixId`| integer | 物品前缀 ID（修饰语）             |

**错误**

| 状态码 | `error`                                    | 原因                             |
|--------|--------------------------------------------|----------------------------------|
| 400    | `Missing required route parameter 'user'.` | `{user}` 为空                   |
| 400    | `User was not found.`                      | 未找到该用户名对应的注册账号     |
| 400    | `Player data was not found.`               | 账号存在但无存档角色数据         |

---

### GET `/nextbot/users/{user}/stats`

返回指定用户的存档角色属性数据。

**权限：** `nextbot.users.stats`

**参数**

| 名称   | 位置       | 说明             |
|--------|------------|------------------|
| `user` | 路由参数   | 要查询的用户名   |

**响应 200**

```json
{
  "health": 400,
  "maxHealth": 500,
  "mana": 100,
  "maxMana": 200,
  "questsCompleted": 7,
  "deathsPve": 12,
  "deathsPvp": 3,
  "onlineSeconds": 36000
}
```

| 字段              | 类型    | 说明                       |
|-------------------|---------|----------------------------|
| `health`          | integer | 当前生命值                 |
| `maxHealth`       | integer | 最大生命值                 |
| `mana`            | integer | 当前魔力值                 |
| `maxMana`         | integer | 最大魔力值                 |
| `questsCompleted` | integer | 渔夫任务完成次数           |
| `deathsPve`       | integer | PvE 死亡次数               |
| `deathsPvp`       | integer | PvP 死亡次数               |
| `onlineSeconds`   | integer | 累计在线时长（秒）；若玩家当前在线则包含本次会话已用时 |

**错误**

| 状态码 | `error`                                    | 原因                             |
|--------|--------------------------------------------|----------------------------------|
| 400    | `Missing required route parameter 'user'.` | `{user}` 为空                   |
| 400    | `User was not found.`                      | 未找到该用户名对应的注册账号     |
| 400    | `Player data was not found.`               | 账号存在但无存档角色数据         |

---

## World（世界）

### GET `/nextbot/world/progress`

返回当前世界的 Boss 与事件击杀状态。

**权限：** `nextbot.world.progress`

**响应 200**

```json
{
  "kingSlime": true,
  "eyeOfCthulhu": true,
  "eaterOfWorldsOrBrainOfCthulhu": false,
  "queenBee": false,
  "skeletron": true,
  "deerclops": false,
  "wallOfFlesh": true,
  "queenSlime": false,
  "theTwins": true,
  "theDestroyer": true,
  "skeletronPrime": true,
  "plantera": true,
  "golem": false,
  "dukeFishron": false,
  "empressOfLight": false,
  "lunaticCultist": false,
  "solarPillar": false,
  "nebulaPillar": false,
  "vortexPillar": false,
  "stardustPillar": false,
  "moonLord": false
}
```

所有字段均为 `boolean`，`true` 表示该 Boss 或事件在当前世界已被击败。

---

### GET `/nextbot/world/map-image`

实时生成当前世界地图的 PNG 图片并以 Base64 返回，每次请求均实时生成。

**权限：** `nextbot.world.map_image`

**响应 200**

```json
{
  "fileName": "map-2025-03-24_10-30-00.png",
  "base64": "<base64-encoded PNG>"
}
```

| 字段       | 类型   | 说明                         |
|------------|--------|------------------------------|
| `fileName` | string | 带时间戳的建议文件名         |
| `base64`   | string | Base64 编码的 PNG 图片数据   |

**错误**

| 状态码 | `error`               | 原因             |
|--------|-----------------------|------------------|
| 500    | `<异常信息>`          | 地图生成失败     |

---

### GET `/nextbot/world/world-file`

读取当前世界的 `.wld` 文件并以 Base64 返回。

**权限：** `nextbot.world.world_file`

**响应 200**

```json
{
  "fileName": "MyWorld.wld",
  "base64": "<base64-encoded .wld file>"
}
```

| 字段       | 类型   | 说明                          |
|------------|--------|-------------------------------|
| `fileName` | string | 世界文件名                    |
| `base64`   | string | Base64 编码的 `.wld` 文件数据 |

**错误**

| 状态码 | `error`               | 原因               |
|--------|-----------------------|--------------------|
| 500    | `<异常信息>`          | 世界文件读取失败   |

---

### GET `/nextbot/world/map-file`

生成当前世界的 `.map` 文件（Terraria 小地图数据）并以 Base64 返回，生成前会完整点亮地图。

**权限：** `nextbot.world.map_file`

**响应 200**

```json
{
  "fileName": "1.map",
  "base64": "<base64-encoded .map file>"
}
```

| 字段       | 类型   | 说明                          |
|------------|--------|-------------------------------|
| `fileName` | string | 地图文件名                    |
| `base64`   | string | Base64 编码的 `.map` 文件数据 |

**错误**

| 状态码 | `error`               | 原因                       |
|--------|-----------------------|----------------------------|
| 500    | `<异常信息>`          | 地图文件生成或读取失败     |

---

## Whitelist（白名单）

### GET `/nextbot/whitelist`

返回当前白名单中的所有用户。

**权限：** `nextbot.whitelist.view`

**响应 200**

```json
{
  "users": ["Arispex", "NextBot"]
}
```

| 字段    | 类型            | 说明             |
|---------|-----------------|------------------|
| `users` | array of string | 当前白名单条目   |

---

### GET `/nextbot/whitelist/add/{user}`

将用户添加到白名单。

**权限：** `nextbot.whitelist.add`

**参数**

| 名称   | 位置       | 说明           |
|--------|------------|----------------|
| `user` | 路由参数   | 要添加的用户名 |

**响应 200**

```json
{
  "response": "User 'Arispex' has been added to the whitelist."
}
```

**错误**

| 状态码 | `error`                             | 原因                   |
|--------|-------------------------------------|------------------------|
| 400    | `Whitelist user is invalid.`        | `{user}` 为空          |
| 400    | `User already exists in whitelist.` | 用户已在白名单中       |

---

### GET `/nextbot/whitelist/remove/{user}`

将用户从白名单中移除。

**权限：** `nextbot.whitelist.remove`

**参数**

| 名称   | 位置       | 说明           |
|--------|------------|----------------|
| `user` | 路由参数   | 要移除的用户名 |

**响应 200**

```json
{
  "response": "User 'Arispex' has been removed from the whitelist."
}
```

**错误**

| 状态码 | `error`                        | 原因                   |
|--------|--------------------------------|------------------------|
| 400    | `Whitelist user is invalid.`   | `{user}` 为空          |
| 400    | `User not found in whitelist.` | 用户不在白名单中       |

---

## Leaderboards（排行榜）

### GET `/nextbot/leaderboards/deaths`

返回所有注册玩家的死亡排行榜，按死亡总数（PvE + PvP）降序排列。

**权限：** `nextbot.leaderboards.deaths`

**响应 200**

```json
{
  "entries": [
    { "username": "Arispex", "deaths": 15 },
    { "username": "NextBot", "deaths": 8 }
  ]
}
```

| 字段                   | 类型    | 说明                          |
|------------------------|---------|-------------------------------|
| `entries`              | array   | 排行榜条目列表                |
| `entries[].username`   | string  | 玩家用户名                    |
| `entries[].deaths`     | integer | 死亡总次数（PvE + PvP）       |

**说明**

- 覆盖所有注册玩家，不限于在线玩家
- 无角色存档数据的玩家不计入排行榜
- 结果按 `deaths` 降序排列

---

### GET `/nextbot/leaderboards/online-time`

返回所有有在线记录的玩家的在线时长排行榜，按 `onlineSeconds` 降序排列。

**权限：** `nextbot.leaderboards.online_time`

**响应 200**

```json
{
  "entries": [
    { "username": "Arispex", "onlineSeconds": 36000 },
    { "username": "NextBot", "onlineSeconds": 7200 }
  ]
}
```

| 字段                      | 类型    | 说明                                         |
|---------------------------|---------|----------------------------------------------|
| `entries`                 | array   | 排行榜条目列表                               |
| `entries[].username`      | string  | 玩家用户名                                   |
| `entries[].onlineSeconds` | integer | 累计在线时长（秒）；包含当前会话已用时       |

**说明**

- 只包含至少登录过一次的玩家
- 当前在线的玩家时长实时计算，无需重启即可反映
- 在线时长持久化于 `OnlineTime.json`

---

### GET `/nextbot/leaderboards/fishing-quests`

返回所有注册玩家的渔夫任务完成数排行榜，按 `questsCompleted` 降序排列。

**权限：** `nextbot.leaderboards.fishing_quests`

**响应 200**

```json
{
  "entries": [
    { "username": "Arispex", "questsCompleted": 42 },
    { "username": "NextBot", "questsCompleted": 7 }
  ]
}
```

| 字段                        | 类型    | 说明                          |
|-----------------------------|---------|-------------------------------|
| `entries`                   | array   | 排行榜条目列表                |
| `entries[].username`        | string  | 玩家用户名                    |
| `entries[].questsCompleted` | integer | 渔夫任务完成次数              |

**说明**

- 覆盖所有注册玩家，不限于在线玩家
- 无角色存档数据的玩家不计入排行榜
- 结果按 `questsCompleted` 降序排列

---

## Security（安全）

### GET `/nextbot/security/confirm-login/{user}`

为指定玩家创建一次性登录预批准，有效期 5 分钟。

当玩家的 UUID 或 IP 与 TShock 数据库中的上次记录不一致时，插件会拒绝其登录并提示在 QQ 群发送「允许登入」。外部 Bot 收到消息后调用此接口完成二次确认，玩家在预批准窗口内重新连接即可正常登录。

**权限：** `nextbot.security.confirm_login`

**参数**

| 名称   | 位置     | 说明           |
|--------|----------|----------------|
| `user` | 路由参数 | 要批准的用户名 |

**响应 200**

```json
{
  "response": "User 'Arispex' has been approved for next login."
}
```

**错误**

| 状态码 | `error`                                                          | 原因                                     |
|--------|------------------------------------------------------------------|------------------------------------------|
| 400    | `Missing required route parameter 'user'.`                       | `{user}` 为空                            |
| 400    | `User was not found.`                                            | 找不到该账号                             |
| 400    | `No pending login request found for user '{user}'.`              | 该玩家尚未被拦截，或拦截记录已过期（5 分钟）|
| 400    | `An active approval already exists for user '{user}'.`           | 该玩家已有有效的预批准，无需重复确认     |

---

### GET `/nextbot/security/reject-login/{user}`

拒绝指定玩家当前待确认（pending）的登入请求。**仅作用于 pending 状态**——若该用户已经被 confirm（存在有效 approval），此端点不会撤销 approval，而是返回 400。

**权限：** `nextbot.security.reject_login`

**参数**

| 名称   | 位置     | 说明             |
|--------|----------|------------------|
| `user` | 路由参数 | 要拒绝的用户名   |

**响应 200**

```json
{
  "response": "User 'Arispex' login request has been rejected."
}
```

**错误**

| 状态码 | `error`                                                  | 原因                                                      |
|--------|----------------------------------------------------------|-----------------------------------------------------------|
| 400    | `Missing required route parameter 'user'.`               | `{user}` 为空                                             |
| 400    | `User was not found.`                                    | 找不到该账号                                              |
| 400    | `No pending login request found for user '{user}'.`     | 无待处理 pending（不存在 / 已过期 / 已被 confirm 消耗）   |

---

## Config（配置）

### GET `/nextbot/config`

返回完整的插件配置。

**权限：** `nextbot.config.read`

**响应 200**

```json
{
  "nextbot": {
    "baseUrl": "",
    "token": ""
  },
  "whitelist": {
    "enabled": true,
    "denyMessage": "你不在白名单中，请在 QQ 群发送「注册账号 {playerName}」后重新连接",
    "caseSensitive": true
  },
  "loginConfirmation": {
    "enabled": true,
    "detectUuid": true,
    "detectIp": true,
    "autoLogin": false,
    "emptyUuidMessage": "无法获取你的 UUID，请联系管理员。",
    "changeDetectedMessage": "你的 {changed} 发生变化，请在 QQ 群发送「允许登入」后重新连接。",
    "deviceMismatchMessage": "该账号已通过登入确认，但当前设备与确认时不一致，请使用原设备登入。",
    "pendingExistsMessage": "该账号已有待确认的登入请求，请等待其过期后再试。"
  }
}
```

---

### GET `/nextbot/config/update`

部分更新配置字段，使用点号分隔路径。更新后自动热重载。

**权限：** `nextbot.config.update`

**参数**

通过 query string 传递，key 为点号分隔的字段路径，value 为新值。

示例：`?whitelist.enabled=false&loginConfirmation.detectUuid=false&nextbot.baseUrl=https://example.com`

类型自动推断：`true`/`false` → bool，纯数字 → number，其余 → string。

**响应 200**

```json
{
  "response": "Updated 2 field(s) successfully."
}
```

**错误**

| 状态码 | `error`                              | 原因                 |
|--------|--------------------------------------|----------------------|
| 400    | `No fields specified for update.`    | 未提供任何更新字段   |
| 400    | `Unknown config field '{path}'.`     | 字段路径不存在       |

---

### GET `/nextbot/config/reload`

从磁盘重新加载插件配置与白名单。

**权限：** `nextbot.config.reload`

**响应 200**

```json
{
  "response": "Configuration reloaded successfully."
}
```

**错误**

| 状态码 | `error`               | 原因           |
|--------|-----------------------|----------------|
| 500    | `<异常信息>`          | 配置重载失败   |

---

### GET `/nextbot/config/verify-nextbot`

按需触发一次与 NextBot 上游的连接验证，调用 `POST {baseUrl}/webui/api/session` 探测 token 是否有效。插件启动时也会自动执行一次同样的验证并写入日志。

**权限：** `nextbot.config.verify_nextbot`

**响应 200**

| 字段          | 类型           | 说明                                                                                              |
|---------------|----------------|---------------------------------------------------------------------------------------------------|
| `probeStatus` | string         | `Ok` / `Skipped` / `Unauthorized` / `InvalidToken` / `Unreachable`                                |
| `message`     | string         | 结果描述                                                                                          |
| `baseUrl`     | string         | 当前配置的 NextBot baseUrl                                                                        |
| `httpStatus`  | number \| 缺失 | 上游返回的 HTTP 状态码；`Skipped` 和部分 `Unreachable`（如非法 URL / 网络异常）场景下不返回此字段 |

成功示例：

```json
{
  "probeStatus": "Ok",
  "message": "上游返回 201 Created，token 有效",
  "baseUrl": "https://example.com",
  "httpStatus": 201
}
```

未配置示例：

```json
{
  "probeStatus": "Skipped",
  "message": "未配置 baseUrl 或 token",
  "baseUrl": ""
}
```

Token 错误示例：

```json
{
  "probeStatus": "Unauthorized",
  "message": "token 错误",
  "baseUrl": "https://example.com",
  "httpStatus": 401
}
```

网络不可达示例：

```json
{
  "probeStatus": "Unreachable",
  "message": "网络异常：No such host is known.",
  "baseUrl": "https://not-exist.example"
}
```

**错误**

| 状态码 | `error`      | 原因                   |
|--------|--------------|------------------------|
| 500    | `<异常信息>` | 读取配置或调用探针失败 |
