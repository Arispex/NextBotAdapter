# NextBotAdapter Configuration

## 概述

NextBotAdapter 的运行时配置文件位于 TShock 保存目录下的 `NextBotAdapter` 文件夹中。

当前包含两个文件：

- `NextBotAdapter.json`
- `Whitelist.json`

## 1. NextBotAdapter.json

`NextBotAdapter.json` 是插件总配置文件。

```json
{
  "whitelist": {
    "enabled": true,
    "denyMessage": "You are not on the whitelist.",
    "caseSensitive": true
  }
}
```

### 字段说明

#### `whitelist.enabled`
- 是否启用白名单
- `true`：启用白名单
- `false`：关闭白名单

#### `whitelist.denyMessage`
- 玩家不在白名单内时显示的拒绝提示信息

#### `whitelist.caseSensitive`
- 玩家名称比较时是否区分大小写
- 默认值：`true`

## 2. Whitelist.json

`Whitelist.json` 用于保存白名单玩家名称列表。

示例：

```json
{
  "users": [
    "Arispex",
    "NextBot"
  ]
}
```

### 字段说明

#### `users`
- 白名单玩家名称数组
- 按玩家名称匹配

## 入服校验行为

当白名单启用时，玩家进入服务器后会立即按玩家名称进行白名单检查：

- 在白名单内：允许进入
- 不在白名单内：拒绝进入，并使用 `whitelist.denyMessage` 作为提示信息

名称比较是否区分大小写由 `whitelist.caseSensitive` 控制。

## 配置异常回退行为

### 文件缺失时
如果配置文件不存在：
- 会自动生成默认配置文件
- 插件继续正常运行

### 文件损坏时
如果配置文件存在但 JSON 内容损坏：
- 插件会记录错误日志
- 使用内存中的默认配置 / 空白名单继续运行
- **不会覆盖原有损坏文件**

这意味着你仍然可以手动检查和修复原文件。
