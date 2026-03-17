# NextBotAdapter REST API

## 概述

NextBotAdapter 当前提供 8 个 REST API：

- `GET /nextbot/users/{user}/inventory`
- `GET /nextbot/users/{user}/stats`
- `GET /nextbot/world/progress`
- `GET /nextbot/world/map-image`
- `GET /nextbot/whitelist`
- `POST /nextbot/whitelist/add/{user}`
- `POST /nextbot/whitelist/remove/{user}`
- `POST /nextbot/config/reload`

## 权限节点

- `nextbot.users.inventory`
- `nextbot.users.stats`
- `nextbot.world.progress`
- `nextbot.world.map_image`
- `nextbot.whitelist.view`
- `nextbot.whitelist.add`
- `nextbot.whitelist.remove`
- `nextbot.config.reload`

## 响应格式

### 成功响应

成功时，HTTP Body 使用以下结构：

```json
{
  "status": "200",
  "data": { ... }
}
```

### 错误响应

失败时，HTTP Body 使用以下结构：

```json
{
  "status": "404",
  "error": {
    "code": "user_not_found",
    "message": "User was not found."
  }
}
```

说明：
- `status` 由 TShock `RestObject` 自动出现在响应体中
- `error.code` 用于给 NextBot 做程序判断
- `error.message` 用于展示具体错误信息
- 成功响应不会返回面向前端展示的 `message`

## HTTP 状态码语义

当前接口使用以下状态码：

- `200`：请求成功
- `400`：缺少必要路由参数或参数不合法
- `404`：用户不存在、玩家数据不存在、或白名单用户不存在
- `409`：添加白名单时用户已存在
- `500`：配置重载失败，或地图图片生成失败

## 接口说明

---

## 1. 获取用户背包

**Method**: `GET`

**Path**:

```text
/nextbot/users/{user}/inventory
```

**权限**:

```text
nextbot.users.inventory
```

### 成功响应示例

```json
{
  "status": "200",
  "data": {
    "items": [
      {
        "slot": 0,
        "netId": 100,
        "stack": 2,
        "prefixId": 1
      },
      {
        "slot": 1,
        "netId": 200,
        "stack": 5,
        "prefixId": 3
      }
    ]
  }
}
```

### 失败响应示例

```json
{
  "status": "404",
  "error": {
    "code": "user_not_found",
    "message": "User was not found."
  }
}
```

---

## 2. 获取用户状态

**Method**: `GET`

**Path**:

```text
/nextbot/users/{user}/stats
```

**权限**:

```text
nextbot.users.stats
```

### 成功响应示例

```json
{
  "status": "200",
  "data": {
    "health": 120,
    "maxHealth": 400,
    "mana": 80,
    "maxMana": 200,
    "questsCompleted": 9,
    "deathsPve": 4,
    "deathsPvp": 2
  }
}
```

### 失败响应示例

```json
{
  "status": "404",
  "error": {
    "code": "user_data_not_found",
    "message": "Player data was not found."
  }
}
```

---

## 3. 获取世界进度

**Method**: `GET`

**Path**:

```text
/nextbot/world/progress
```

**权限**:

```text
nextbot.world.progress
```

### 成功响应示例

```json
{
  "status": "200",
  "data": {
    "kingSlime": true,
    "eyeOfCthulhu": false,
    "eaterOfWorldsOrBrainOfCthulhu": false,
    "queenBee": false,
    "skeletron": false,
    "deerclops": false,
    "wallOfFlesh": true,
    "queenSlime": false,
    "theTwins": false,
    "theDestroyer": false,
    "skeletronPrime": false,
    "plantera": false,
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
}
```

### 失败响应示例

当前该接口没有专门的业务失败分支；如果框架层或运行时异常发生，将返回 TShock 默认错误响应。

---

## 4. 生成世界地图图片

**Method**: `GET`

**Path**:

```text
/nextbot/world/map-image
```

**权限**:

```text
nextbot.world.map_image
```

### 行为说明

当前接口会：

- 生成当前世界的完整地图 PNG 图片
- 将生成结果保存到插件配置目录下的 `cache/` 子目录
- 在响应体中返回生成文件名和图片内容的 base64

### 成功响应示例

```json
{
  "status": "200",
  "data": {
    "fileName": "map-2026-03-17_20-30-00.png",
    "base64": "iVBORw0KGgoAAAANSUhEUgAA..."
  }
}
```

### 失败响应示例

```json
{
  "status": "500",
  "error": {
    "code": "map_image_generation_failed",
    "message": "map generation failed"
  }
}
```

---

## 5. 获取白名单列表

**Method**: `GET`

**Path**:

```text
/nextbot/whitelist
```

**权限**:

```text
nextbot.whitelist.view
```

### 成功响应示例

```json
{
  "status": "200",
  "data": {
    "users": [
      "Arispex",
      "NextBot"
    ]
  }
}
```

### 失败响应示例

当前该接口没有专门的业务失败分支；如果框架层或运行时异常发生，将返回 TShock 默认错误响应。

---

## 6. 添加白名单用户

**Method**: `POST`

**Path**:

```text
/nextbot/whitelist/add/{user}
```

**权限**:

```text
nextbot.whitelist.add
```

### 成功响应示例

```json
{
  "status": "200",
  "data": {
    "users": [
      "Arispex",
      "NextBot"
    ]
  }
}
```

### 失败响应示例

```json
{
  "status": "409",
  "error": {
    "code": "whitelist_user_exists",
    "message": "User already exists in whitelist."
  }
}
```

---

## 7. 删除白名单用户

**Method**: `POST`

**Path**:

```text
/nextbot/whitelist/remove/{user}
```

**权限**:

```text
nextbot.whitelist.remove
```

### 成功响应示例

```json
{
  "status": "200",
  "data": {
    "users": [
      "Arispex"
    ]
  }
}
```

### 失败响应示例

```json
{
  "status": "404",
  "error": {
    "code": "whitelist_user_not_found",
    "message": "User not found in whitelist."
  }
}
```

---

## 8. 重载全部配置

**Method**: `POST`

**Path**:

```text
/nextbot/config/reload
```

**权限**:

```text
nextbot.config.reload
```

### 行为说明

当前会重载插件的全部实际运行时配置，包括：

- `NextBotAdapter.json`
- `Whitelist.json`

重载后会立即影响：
- 玩家入服白名单校验
- 白名单 REST API 的查看 / 添加 / 删除行为

### 成功响应示例

```json
{
  "status": "200",
  "data": {
    "reloaded": true
  }
}
```

### 失败响应示例

```json
{
  "status": "500",
  "error": {
    "code": "config_reload_failed",
    "message": "reload failed"
  }
}
```

## 兼容性说明

当前 API 采用 TShock `RestObject` 返回结果，因此响应体中会保留 `status` 字段。这是当前插件对 TShock REST 机制的兼容设计。

## 示例请求

```text
GET /nextbot/users/Arispex/inventory
GET /nextbot/users/Arispex/stats
GET /nextbot/world/progress
GET /nextbot/world/map-image
GET /nextbot/whitelist
POST /nextbot/whitelist/add/Arispex
POST /nextbot/whitelist/remove/Arispex
POST /nextbot/config/reload
```
