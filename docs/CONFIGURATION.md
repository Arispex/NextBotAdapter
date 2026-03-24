# NextBotAdapter Configuration

配置文件位于 TShock 保存目录下的 `NextBotAdapter/` 文件夹中，包含两个文件：

```
tshock/
└── NextBotAdapter/
    ├── NextBotAdapter.json   # 插件配置
    └── Whitelist.json        # 白名单数据
```

首次启动时两个文件均会自动创建。

---

## NextBotAdapter.json

插件主配置文件。

```json
{
  "whitelist": {
    "enabled": true,
    "denyMessage": "You are not on the whitelist.",
    "caseSensitive": true
  }
}
```

### `whitelist`

| 字段            | 类型    | 默认值                              | 说明                                         |
|-----------------|---------|-------------------------------------|----------------------------------------------|
| `enabled`       | boolean | `true`                              | 是否启用白名单。`false` 时所有玩家均可入服   |
| `denyMessage`   | string  | `"You are not on the whitelist."`   | 玩家不在白名单时的拒绝提示                   |
| `caseSensitive` | boolean | `true`                              | 玩家名称比较是否区分大小写                   |

---

## Whitelist.json

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

两个文件均会重新从磁盘读取。

---

## 异常回退

| 情况           | 行为                                                     |
|----------------|----------------------------------------------------------|
| 文件不存在     | 自动创建默认文件，插件正常运行                           |
| 文件 JSON 损坏 | 记录警告日志，使用默认配置 / 空白名单运行，**原文件不会被覆盖** |
