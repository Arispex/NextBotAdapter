# NextBotAdapter 游戏内指令

插件注册的游戏内聊天指令，通过 TShock 权限系统控制访问。

所有子指令均以 `/nb` 为前缀。

**权限：** `nextbot.admin.reload`

---

## `/nb help`

显示所有可用的子指令列表。直接输入 `/nb`（不带参数）也会显示帮助信息。

**用法**

```
/nb
/nb help
```

---

## `/nb reload`

重载插件的所有配置文件和数据文件，效果等同于 REST API 的 `GET /nextbot/config/reload`。

**用法**

```
/nb reload
```

**说明**

执行后会从磁盘重新读取以下文件：

- `NextBotAdapter.json`（插件配置）
- `Data/Whitelist.json`（白名单数据）
- `Data/Blacklist.json`（黑名单数据）
- `Data/OnlineTime.json`（在线时长数据）

重载成功后会在聊天中提示"NextBotAdapter 配置与数据文件已重载。"，同时在服务器日志中记录重载过程。
