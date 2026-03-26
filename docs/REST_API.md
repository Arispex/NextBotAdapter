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
  "deathsPvp": 3
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

## Config（配置）

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
