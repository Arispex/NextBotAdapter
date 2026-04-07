# NextBotAdapter Configuration

配置文件位于 TShock 保存目录下的 `NextBotAdapter/` 文件夹中，包含三个文件：

```
tshock/
└── NextBotAdapter/
    ├── NextBotAdapter.json      # 插件配置
    └── Data/
        ├── Whitelist.json       # 白名单数据
        └── OnlineTime.json      # 玩家在线时长数据
```

首次启动时所有文件均会自动创建。

---

## NextBotAdapter.json

插件主配置文件。

```json
{
  "whitelist": {
    "enabled": true,
    "denyMessage": "You are not on the whitelist.",
    "caseSensitive": true
  },
  "loginConfirmation": {
    "enabled": true,
    "detectUuid": true,
    "detectIp": true,
    "emptyUuidMessage": "无法获取你的 UUID，请联系管理员。",
    "changeDetectedMessage": "你的 {changed} 发生变化，请在 QQ 群发送「登入」后重新连接。",
    "deviceMismatchMessage": "该账号已通过登入确认，但当前设备与确认时不一致，请使用原设备登入。",
    "pendingExistsMessage": "该账号已有待确认的登入请求，请等待其过期后再试。"
  }
}
```

### `whitelist`

| 字段            | 类型    | 默认值                              | 说明                                         |
|-----------------|---------|-------------------------------------|----------------------------------------------|
| `enabled`       | boolean | `true`                              | 是否启用白名单。`false` 时所有玩家均可入服   |
| `denyMessage`   | string  | `"You are not on the whitelist."`   | 玩家不在白名单时的拒绝提示                   |
| `caseSensitive` | boolean | `true`                              | 玩家名称比较是否区分大小写                   |

### `loginConfirmation`

| 字段                    | 类型    | 默认值                                                                         | 说明                                                               |
|-------------------------|---------|--------------------------------------------------------------------------------|--------------------------------------------------------------------|
| `enabled`               | boolean | `true`                                                                         | 是否启用 UUID/IP 变更二次确认。`false` 时跳过所有检测             |
| `detectUuid`            | boolean | `true`                                                                         | 是否检测 UUID 变更                                                 |
| `detectIp`              | boolean | `true`                                                                         | 是否检测 IP 变更                                                   |
| `emptyUuidMessage`      | string  | `"无法获取你的 UUID，请联系管理员。"`                                          | UUID 为空时的拒绝提示                                              |
| `changeDetectedMessage` | string  | `"你的 {changed} 发生变化，请在 QQ 群发送「登入」后重新连接。"`               | UUID/IP 变化时的拒绝提示。`{changed}` 会被替换为 `UUID`、`IP` 或 `UUID 和 IP` |
| `deviceMismatchMessage` | string  | `"该账号已通过登入确认，但当前设备与确认时不一致，请使用原设备登入。"`         | 已有审批但设备不匹配时的拒绝提示                                   |
| `pendingExistsMessage`  | string  | `"该账号已有待确认的登入请求，请等待其过期后再试。"`                           | 已有待确认请求时的拒绝提示                                         |

UUID 或 IP 发生变化时，玩家登录会被拒绝，需通过 `GET /nextbot/security/confirm-login/{user}` 完成二次确认。

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

## 配置热重载

通过 API 可在不重启服务器的情况下重新加载配置：

```
GET /nextbot/config/reload?token=<token>
```

三个文件均会重新从磁盘读取。

---

## 异常回退

| 情况           | 行为                                                     |
|----------------|----------------------------------------------------------|
| 文件不存在     | 自动创建默认文件，插件正常运行                           |
| 文件 JSON 损坏 | 记录警告日志，使用默认配置 / 空白名单运行，**原文件不会被覆盖** |
