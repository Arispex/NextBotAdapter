# Auto-complete Missing Config Fields on Startup

## Goal
插件启动时检测配置文件（NextBotAdapter.json）是否缺少代码模型中定义的字段，缺少的字段自动用默认值补全并回写到文件，记录日志提示已补全。

## Requirements
- 读取配置文件后，将反序列化结果与默认配置对比
- 缺少的字段用默认值填充
- 回写到原文件（保持 JSON 格式化）
- 记录一条日志说明已补全配置文件

## Acceptance Criteria
- [ ] 缺少顶层字段（如 `loginConfirmation`）时自动补全
- [ ] 缺少嵌套字段（如 `loginConfirmation.emptyUuidMessage`）时自动补全
- [ ] 已有字段的值不被覆盖
- [ ] 配置文件完整时不触发回写
- [ ] 补全后记录日志

## Technical Notes
- 改动集中在 `WhitelistConfigService`（读写配置文件的地方）
- 利用 System.Text.Json 的 JsonNode 做结构对比，或直接序列化默认值后对比
