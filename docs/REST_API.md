# NextBotAdapter REST API

## 概述

NextBotAdapter 是一个用于适配 NextBot 获取 TShock 服务端信息的插件。

当前提供 3 个只读 REST API：

- `GET /nextbot/users/{user}/inventory`
- `GET /nextbot/users/{user}/stats`
- `GET /nextbot/world/progress`

## 权限节点

- `nextbot.users.inventory`
- `nextbot.users.stats`
- `nextbot.world.progress`

## 响应格式

### 成功响应

成功时，HTTP Body 使用以下结构：

```json
{
  "status": "200",
  "data": { ... }
}
```

说明：
- `status` 由 TShock `RestObject` 自动出现在响应体中
- `data` 为接口返回的业务数据

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

## HTTP 状态码语义

当前接口使用以下状态码：

- `200`：请求成功
- `400`：缺少必要路由参数，例如缺少 `user`
- `404`：用户不存在，或玩家数据不存在

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

**路径参数**:

- `user`: TShock 用户名

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

### 字段说明

#### `data.items[]`

- `slot`: 物品槽位索引
- `netId`: Terraria 物品 NetId
- `stack`: 物品数量
- `prefixId`: 物品前缀 ID

### 失败响应示例

#### 用户参数缺失

```json
{
  "status": "400",
  "error": {
    "code": "missing_user",
    "message": "Missing required route parameter 'user'."
  }
}
```

#### 用户不存在

```json
{
  "status": "404",
  "error": {
    "code": "user_not_found",
    "message": "User was not found."
  }
}
```

#### 玩家数据不存在

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

**路径参数**:

- `user`: TShock 用户名

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

### 字段说明

- `health`: 当前生命值
- `maxHealth`: 最大生命值
- `mana`: 当前魔力值
- `maxMana`: 最大魔力值
- `questsCompleted`: 渔夫任务完成数
- `deathsPve`: PVE 死亡次数
- `deathsPvp`: PVP 死亡次数

### 失败响应示例

#### 用户不存在

```json
{
  "status": "404",
  "error": {
    "code": "user_not_found",
    "message": "User was not found."
  }
}
```

#### 玩家数据不存在

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

### 字段说明

- `kingSlime`: 史莱姆王
- `eyeOfCthulhu`: 克苏鲁之眼
- `eaterOfWorldsOrBrainOfCthulhu`: 世界吞噬者或克苏鲁之脑
- `queenBee`: 蜂王
- `skeletron`: 骷髅王
- `deerclops`: 独眼巨鹿
- `wallOfFlesh`: 血肉墙 / 是否进入困难模式
- `queenSlime`: 史莱姆皇后
- `theTwins`: 双子魔眼
- `theDestroyer`: 毁灭者
- `skeletronPrime`: 机械骷髅王
- `plantera`: 世纪之花
- `golem`: 石巨人
- `dukeFishron`: 猪龙鱼公爵
- `empressOfLight`: 光之女皇
- `lunaticCultist`: 拜月教邪教徒
- `solarPillar`: 日耀柱
- `nebulaPillar`: 星云柱
- `vortexPillar`: 星旋柱
- `stardustPillar`: 星尘柱
- `moonLord`: 月亮领主

## 兼容性说明

当前 API 采用 TShock `RestObject` 返回结果，因此响应体中会保留 `status` 字段。这是当前插件对 TShock REST 机制的兼容设计。

## 示例请求

### 获取用户背包

```text
GET /nextbot/users/Arispex/inventory
```

### 获取用户状态

```text
GET /nextbot/users/Arispex/stats
```

### 获取世界进度

```text
GET /nextbot/world/progress
```
