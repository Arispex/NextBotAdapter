# NextBotAdapter Configuration

配置文件位于 TShock 保存目录下的 `NextBotAdapter/` 文件夹中，包含四个文件：

```
tshock/
└── NextBotAdapter/
    ├── NextBotAdapter.json      # 插件配置
    └── Data/
        ├── Whitelist.json       # 白名单数据
        ├── Blacklist.json       # 黑名单数据
        └── OnlineTime.json      # 玩家在线时长数据
```

首次启动时所有文件均会自动创建。

---

## NextBotAdapter.json

插件主配置文件。

```json
{
  "nextbot": {
    "baseUrl": "",
    "token": ""
  },
  "whitelist": {
    "enabled": true,
    "denyMessage": "你不在白名单中，请在 QQ 群发送「注册账号 {playerName}」后重新连接。",
    "caseSensitive": true
  },
  "blacklist": {
    "enabled": true,
    "denyMessage": "你已被封禁，原因：{reason}。如有疑问，请联系管理员。"
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

### `nextbot`

| 字段       | 类型   | 默认值 | 说明                                    |
|------------|--------|--------|-----------------------------------------|
| `baseUrl`  | string | `""`   | NextBot 上游服务的基础 URL              |
| `token`    | string | `""`   | 调用 NextBot 上游服务使用的鉴权 token   |

### `whitelist`

| 字段            | 类型    | 默认值                              | 说明                                         |
|-----------------|---------|-------------------------------------|----------------------------------------------|
| `enabled`       | boolean | `true`                              | 是否启用白名单。`false` 时所有玩家均可入服   |
| `denyMessage`   | string  | `"你不在白名单中，请在 QQ 群发送「注册账号 {playerName}」后重新连接。"` | 玩家不在白名单时的拒绝提示。`{playerName}` 会被替换为当前尝试入服的玩家用户名 |
| `caseSensitive` | boolean | `true`                              | 玩家名称比较是否区分大小写                   |

### `blacklist`

| 字段          | 类型    | 默认值                                                | 说明                                                                         |
|---------------|---------|-------------------------------------------------------|------------------------------------------------------------------------------|
| `enabled`     | boolean | `true`                                               | 是否启用黑名单。`false` 时跳过黑名单校验                                    |
| `denyMessage` | string  | `"你已被封禁，原因：{reason}。如有疑问，请联系管理员。"`    | 被封禁玩家入服时的拒绝提示。`{reason}` 会被替换为该玩家的封禁原因            |

### `loginConfirmation`

| 字段                    | 类型    | 默认值                                                                         | 说明                                                               |
|-------------------------|---------|--------------------------------------------------------------------------------|--------------------------------------------------------------------|
| `enabled`               | boolean | `true`                                                                         | 是否启用 UUID/IP 变更二次确认。`false` 时跳过所有检测             |
| `detectUuid`            | boolean | `true`                                                                         | 是否检测 UUID 变更                                                 |
| `detectIp`              | boolean | `true`                                                                         | 是否检测 IP 变更                                                   |
| `autoLogin`             | boolean | `false`                                                                        | 玩家进入服务器后，自动登入与其用户名匹配的 TShock 账号，无需手动 `/login`。详见下方"autoLogin 安全说明" |
| `emptyUuidMessage`      | string  | `"无法获取你的 UUID，请联系管理员。"`                                          | UUID 为空时的拒绝提示                                              |
| `changeDetectedMessage` | string  | `"你的 {changed} 发生变化，请在 QQ 群发送「允许登入」后重新连接。"`               | UUID/IP 变化时的拒绝提示。`{changed}` 会被替换为 `UUID`、`IP` 或 `UUID 和 IP` |
| `deviceMismatchMessage` | string  | `"该账号已通过登入确认，但当前设备与确认时不一致，请使用原设备登入。"`         | 已有审批但设备不匹配时的拒绝提示                                   |
| `pendingExistsMessage`  | string  | `"该账号已有待确认的登入请求，请等待其过期后再试。"`                           | 已有待确认请求时的拒绝提示                                         |

UUID 或 IP 发生变化时，玩家登录会被拒绝，需通过 `GET /nextbot/security/confirm-login/{user}` 完成二次确认。

### autoLogin 安全说明

启用 `autoLogin` 后，密码不再参与鉴权，**账号鉴权完全依赖设备指纹**（Terraria 客户端 UUID + 账号 `KnownIps` 中最近一次登录 IP）。插件在登入成功后会同步调用 TShock 的 `SetUserAccountUUID` / `UpdateLogin`，更新账号 UUID 和 IP 基线。

**生效前置条件（由插件强制）**：

- `enabled` 必须为 `true`
- `detectUuid` 和 `detectIp` 至少一个为 `true`

任一条件不满足时，`autoLogin` 会被静默跳过，玩家正常进服后需手动 `/login`，以避免出现"任何人只要用目标用户名连入就能登入"的裸奔场景。

**已知风险**：

- Terraria UUID 是客户端可控、非秘密的标识，同机器 / 同局域网 / 服务器日志都可能泄漏；一旦泄漏，攻击者可以在同 IP 下冒充
- 账号第一次被成功 `autoLogin` 的设备，会被 `SetUserAccountUUID` / `UpdateLogin` 写入为新的信任基线；这意味着**任一次鉴权失误都会被沉淀为合法凭据**

**建议**：

- 与 `loginConfirmation.enabled=true` + `detectUuid=true` + `detectIp=true` 同时使用，提高攻破门槛

---

## Data/Whitelist.json

白名单玩家名称列表，由插件通过 API 自动维护，通常不需要手动编辑。

```json
{
  "users": [
    "Arispex",
    "NextBot"
  ]
}
```

| 字段    | 类型            | 说明           |
|---------|-----------------|----------------|
| `users` | array of string | 白名单玩家名称 |

---

## Data/Blacklist.json

黑名单数据，由插件通过 API 自动维护，通常不需要手动编辑。

```json
{
  "entries": [
    { "username": "BadPlayer", "reason": "使用外挂" }
  ]
}
```

| 字段                 | 类型   | 说明           |
|----------------------|--------|----------------|
| `entries`            | array  | 黑名单条目列表 |
| `entries[].username` | string | 被封禁的用户名 |
| `entries[].reason`   | string | 封禁原因       |

---

## Data/OnlineTime.json

玩家在线时长累计数据，由插件自动维护，通常不需要手动编辑。

```json
{
  "records": {
    "Arispex": 36000,
    "NextBot": 7200
  }
}
```

| 字段      | 类型                   | 说明                          |
|-----------|------------------------|-------------------------------|
| `records` | object (string → long) | 键为玩家用户名，值为累计在线秒数 |

---

## 白名单校验

启用白名单后，玩家连接时会按名称进行检查：

- 在白名单内 → 允许入服
- 不在白名单内 → 断开连接，提示 `denyMessage`

名称比较是否区分大小写由 `caseSensitive` 控制。

---

## 黑名单校验

启用黑名单后，玩家连接时会在白名单校验通过后进行黑名单检查：

- 不在黑名单内 → 允许入服
- 在黑名单内 → 断开连接，提示 `denyMessage`（其中 `{reason}` 替换为该玩家的封禁原因）

黑名单名称比较不区分大小写。

---

## 配置热重载

通过 API 可在不重启服务器的情况下重新加载配置：

```
GET /nextbot/config/reload?token=<token>
```

四个文件均会重新从磁盘读取。

---

## 异常回退

| 情况           | 行为                                                     |
|----------------|----------------------------------------------------------|
| 文件不存在     | 自动创建默认文件，插件正常运行                           |
| 文件 JSON 损坏 | 记录警告日志，使用默认配置 / 空白名单 / 空黑名单运行，**原文件不会被覆盖** |
