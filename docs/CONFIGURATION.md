# NextBotAdapter Configuration

## 概述

NextBotAdapter 的运行时配置文件位于 TShock 保存目录下的 `NextBotAdapter` 文件夹中。

当前包含两个文件和一个缓存目录：

- `NextBotAdapter.json`
- `Whitelist.json`
- `cache/`

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

## 3. cache/

`cache/` 用于保存插件运行时生成的缓存文件。

当前会保存：

- 地图图片缓存文件（PNG）

### 创建时机
- 插件初始化配置目录时会自动创建 `cache/`
- 即使当前目录下还没有缓存文件，该目录也会预先存在

### 当前用途
- `GET /nextbot/world/map-image` 在生成地图图片时，会将 PNG 文件保存到该目录下，并在响应中返回生成结果

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
