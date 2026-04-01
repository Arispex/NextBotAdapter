# Config Read/Update REST API

## Goal
添加主配置文件的读取和更新 REST API，支持通过点号路径的 query string 部分更新嵌套字段。

## Requirements
- `GET /nextbot/config` — 返回完整 NextBotAdapter.json 配置
- `GET /nextbot/config/update?{path}={value}` — 部分更新，支持点号路径如 `whitelist.enabled=false`
- 类型自动推断：`true`/`false` → bool，纯数字 → number，其余 → string
- 只更新传入的字段，其余不动
- 更新后回写文件 + 内存热重载
- 后续新增配置字段自动支持，不需要改 API 代码

## API

### GET `/nextbot/config`
返回完整配置 JSON。

### GET `/nextbot/config/update`
部分更新配置字段。

参数：query string，key 为点号分隔路径，value 为新值。
示例：`?whitelist.enabled=false&loginConfirmation.detectUuid=false`

错误：
- 400 `No fields specified for update.` — 无参数
- 400 `Unknown config field '{path}'.` — 路径不存在

## Acceptance Criteria
- [ ] GET /nextbot/config 返回完整配置
- [ ] GET /nextbot/config/update 支持点号路径部分更新
- [ ] 类型推断正确（bool/number/string）
- [ ] 未传入的字段不被修改
- [ ] 更新后文件和内存同步
- [ ] 路径不存在时返回 400
- [ ] 无参数时返回 400

## Technical Notes
- 用 JsonNode 做通用路径解析和更新
- ConfigEndpoints 扩展新方法
- WhitelistConfigService 需要暴露读取原始 JSON 和写入 JsonNode 的能力
- 更新后调用现有 ReloadAll 热重载
