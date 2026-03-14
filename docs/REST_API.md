# NextBotAdapter REST API

## 概述

NextBotAdapter 是一个用于适配 NextBot 获取 TShock 服务端信息的插件。

当前提供 6 个 REST API：

- `GET /nextbot/users/{user}/inventory`
- `GET /nextbot/users/{user}/stats`
- `GET /nextbot/world/progress`
- `GET /nextbot/whitelist`
- `POST /nextbot/whitelist/add/{user}`
- `POST /nextbot/whitelist/remove/{user}`

## 权限节点

- `nextbot.users.inventory`
- `nextbot.users.stats`
- `nextbot.world.progress`
- `nextbot.whitelist.view`
- `nextbot.whitelist.add`
- `nextbot.whitelist.remove`

## 白名单配置文件

插件运行时会在 TShock 保存目录下使用 `NextBotAdapter` 配置文件夹。

### 1. NextBotAdapter.json

用于保存白名单相关配置，例如：

```json
{
  "enabled": true,
  "denyMessage": "You are not on the whitelist.",
  "caseSensitive": true
}
```

字段说明：
- `enabled`: 是否启用白名单
- `denyMessage`: 不在白名单时踢出提示
- `caseSensitive`: 名称比较是否区分大小写，默认 `true`

### 2. whitelist.json

用于保存白名单玩家名称列表，例如：

```json
{
  "users": [
    "Arispex",
    "NextBot"
  ]
}
```

## 入服校验行为

当白名单启用时，玩家进入服务器后会立即按玩家名称进行白名单检查：

- 在白名单内：允许进入
- 不在白名单内：拒绝进入，并使用 `denyMessage` 作为提示信息

名称比较是否区分大小写由 `caseSensitive` 控制。

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

## HTTP 状态码语义

当前接口使用以下状态码：

- `200`：请求成功
- `400`：缺少必要路由参数或参数不合法
- `404`：用户不存在、玩家数据不存在、或白名单用户不存在
- `409`：添加白名单时用户已存在

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

---

## 4. 获取白名单列表

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

---

## 5. 添加白名单用户

**Method**: `POST`

**Path**:

```text
/nextbot/whitelist/add/{user}
```

**权限**:

```text
nextbot.whitelist.add
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

## 6. 删除白名单用户

**Method**: `POST`

**Path**:

```text
/nextbot/whitelist/remove/{user}
```

**权限**:

```text
nextbot.whitelist.remove
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

## 兼容性说明

当前 API 采用 TShock `RestObject` 返回结果，因此响应体中会保留 `status` 字段。这是当前插件对 TShock REST 机制的兼容设计。

## 示例请求

```text
GET /nextbot/users/Arispex/inventory
GET /nextbot/users/Arispex/stats
GET /nextbot/world/progress
GET /nextbot/whitelist
POST /nextbot/whitelist/add/Arispex
POST /nextbot/whitelist/remove/Arispex
```
