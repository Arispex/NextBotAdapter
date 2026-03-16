# NextBotAdapter 日志系统轻量升级设计

日期：2026-03-16

## 背景

当前项目已经有统一日志入口 `NextBotAdapter/Services/PluginLogger.cs`，但现有日志系统仍有几个明显问题：

- 日志前缀格式较弱，仅有 `[NextBotAdapter][Category]`
- 业务日志大量使用英文，不符合当前项目希望统一为中文日志的要求
- 消息风格偏零散，尚未稳定遵循 Human-reading-first 的自然语言表达
- `Category` 需要业务代码手动维护，增加了调用负担，也导致风格不稳定
- 部分关键链路虽然已有日志，但回退、拒绝、失败等场景的等级与表达仍可进一步统一

本次重构目标是对现有日志系统做一次轻量升级，在保留统一入口的前提下，统一日志前缀、等级、颜色映射和文案风格，并补强配置热重载、白名单持久化和拒绝进入等关键链路的可观测性。

## 目标

### 1. 统一日志前缀

所有日志统一由 `PluginLogger` 生成以下前缀：

- `[timestamp] [LEVEL] [NextBotAdapter] <message>`

约束如下：

- 时间戳使用 ISO 8601 / RFC 3339 风格
- 时间精度固定到毫秒
- 时间戳必须包含明确时区偏移
- 日志等级统一使用 `INFO`、`WARN`、`ERROR`
- 保留 `[NextBotAdapter]`
- 删除现有 `[Category]`
- 业务代码中不得手写时间、等级或分类前缀

### 2. 统一 TShock 颜色语义

日志等级不仅影响文本前缀，也影响 TShock 控制台的颜色输出。统一入口必须保留这一语义：

- `Info` 调用 `TShock.Log?.ConsoleInfo(...)`，对应白色输出
- `Warn` 调用 `TShock.Log?.ConsoleWarn(...)`，对应黄色输出
- `Error` 调用 `TShock.Log?.ConsoleError(...)`，对应红色输出

这条规则是统一入口的核心约束之一，避免日志重构后丢失等级到颜色的映射关系。

### 3. 全量统一为中文日志

当前项目中的现有英文业务日志将统一改为中文日志。

统一规则：

- 主体文案使用中文
- 中文与英文、数字之间保留空格
- 失败原因中的异常消息保留原文，不做中文业务化改写
- 不改写 API 原始错误内容
- 动态内容写入日志前应做单行化处理，将换行、制表符等控制字符规整为空格或安全替代形式，避免破坏日志结构
- 如动态内容过长，应采用明确的截断策略，保证日志可读且不形成超长单行

### 4. 采用 Human-reading-first 风格

日志主消息以自然中文句子为主，优先便于人类在控制台或日志文件中直接阅读。

总体规则：

- 成功：`对象 + 动作 + 成功`
- 失败：`对象 + 动作 + 失败，原因：...`
- 拒绝 / 回退 / 降级：自然表达结果和上下文
- 避免空洞表达，例如“进入方法”“处理完成”
- 避免大段 `key=value` 风格堆砌
- 避免整对象、整请求、整响应的原样输出

例如：

- `白名单配置加载成功。`
- `白名单配置加载失败，将回退为默认配置，原因：...`
- `白名单数据保存成功，当前共有 3 个条目。`
- `玩家 Alice 已被拒绝进入服务器，原因：You are not on the whitelist.`
- `插件初始化完成，已注册 4 个 REST 端点。`

## 范围

本次重构聚焦后端关键链路，并统一相关模块的日志风格，涉及以下文件：

- `NextBotAdapter/Services/PluginLogger.cs`
- `NextBotAdapter/Services/WhitelistConfigService.cs`
- `NextBotAdapter/Services/PersistedWhitelistService.cs`
- `NextBotAdapter/Services/ConfigurationReloadService.cs`
- `NextBotAdapter/Rest/ConfigEndpoints.cs`
- `NextBotAdapter/Plugin/NextBotAdapterPlugin.cs`
- `NextBotAdapter.Tests/PluginLoggerTests.cs`

如果在同一批改动中发现其他散落英文日志或未统一走 `PluginLogger` 的调用点，也可以一并收敛，但不做与当前目标无关的大规模日志重构。

## 统一入口设计

### API 形态

`PluginLogger` 调整为简单统一的三级接口：

- `Info(string message)`
- `Warn(string message)`
- `Error(string message)`

现有的 `category` 参数将被移除。

### 统一职责

`PluginLogger` 内部负责：

1. 生成带毫秒与时区偏移的时间戳
2. 生成 `INFO` / `WARN` / `ERROR` 等级前缀
3. 保留 `[NextBotAdapter]` 前缀
4. 将日志按等级分发到 TShock 对应颜色输出 API

业务代码仅负责提供自然中文消息，不再关心日志格式和颜色分发。

### 时间方案

为保证输出稳定且满足前缀格式要求，`PluginLogger` 应统一生成带明确时区偏移的时间字符串。默认采用运行环境本地时间及其真实偏移，例如 `DateTimeOffset.Now` 对应的本地偏移，而不是强制写死 `+08:00`。这样可以与宿主机和其他系统日志保持一致，避免跨环境部署时出现误导性的时间信息。

测试中不应断言具体时刻值，而应断言时间戳格式是否满足毫秒精度和显式偏移要求。

## 分层日志职责

### Plugin 层

负责：

- 插件生命周期
- Hook 注册
- 玩家最终被拒绝进入等运行时外显结果

不负责：

- 配置文件读写细节
- 白名单文件持久化细节

### Service 层

负责：

- 业务入口与关键状态变化
- 回退、重载、持久化成功
- 业务可控失败

不负责：

- 重复记录接口边界层已经清晰表达的失败结果

### Endpoint 层

负责：

- 接口最终失败出口
- API 语义上的成功 / 失败映射

不负责：

- 细碎中间过程日志

### 失败日志归属规则

失败日志默认由“最终边界”或“最终决策点”记录；下层仅在自己已经完整处理该事件、并且上层不会再输出更清晰结果时记录失败。这样既避免重复，也能保证在调用方变化时仍有明确归属。

针对本次范围内链路，归属规则如下：

- 配置热重载接口失败：由 `ConfigEndpoints` 记录
- 配置文件损坏并回退默认值：由 `WhitelistConfigService.LoadSettings()` 记录
- 白名单文件损坏并回退为空白名单：由 `WhitelistConfigService.LoadWhitelist()` 记录
- 玩家被白名单拒绝进入：由 `NextBotAdapterPlugin.OnPlayerInfo(...)` 记录
- 白名单新增 / 删除业务失败：由 `PersistedWhitelistService` 记录

如果未来 `ConfigurationReloadService` 被非 REST 调用方复用，而该调用方不再承担统一失败日志职责，则应由新的最终调用边界记录失败，或在 service 层补充不重复的失败日志设计。

### 异常与动态内容处理规则

- 失败原因中的原始异常消息可以保留，但必须先做单行化处理
- 默认保留 `Exception.Message`，不强制记录堆栈
- 本次范围内不把完整堆栈写入常规日志；如未来需要堆栈记录，应作为独立规范补充
- 业务代码不应自行拼接多行异常文本
- 对来自 API、文件内容或外部输入的动态内容，统一执行单行化和必要截断，避免日志伪造、日志换行污染或超长内容影响可读性

### 等级规则

#### INFO

用于重要正常事件：

- 插件初始化和释放
- 配置 / 白名单文件创建、加载、保存成功
- 热重载请求开始与完成
- 白名单新增、删除、重载成功
- 当前有效配置摘要

#### WARN

用于可控但值得关注的情况：

- 配置损坏后回退默认值
- 白名单文件损坏后回退为空列表
- 玩家被白名单策略拒绝进入
- 白名单新增 / 删除因业务条件不满足而失败
- 功能未启用但系统仍继续运行

#### ERROR

用于异常或关键失败：

- 热重载接口执行失败
- 影响本次操作结果的未处理异常
- 没有受控回退路径的关键失败

## 逐文件改动设计

### `NextBotAdapter/Services/PluginLogger.cs`

改动目标：

- 移除 `category`
- 统一输出时间戳、等级和 `[NextBotAdapter]`
- 保留 TShock 的颜色分发

同时更新 `NextBotAdapter.Tests/PluginLoggerTests.cs`，将旧格式断言更新为新格式断言。

### `NextBotAdapter/Services/WhitelistConfigService.cs`

保留并统一这些日志语义：

- 默认配置文件创建成功
- 白名单配置加载成功
- 白名单配置加载失败并回退默认配置
- 白名单配置保存成功
- 默认白名单文件创建成功
- 白名单数据加载成功
- 白名单数据加载失败并回退空白名单
- 白名单数据保存成功

示例文案方向：

- `已创建默认白名单配置文件。`
- `白名单配置加载成功。当前启用状态为 True，区分大小写为 True。`
- `白名单配置加载失败，将回退为默认配置，原因：...`
- `白名单配置保存成功。当前启用状态为 True，区分大小写为 True。`
- `已创建默认白名单文件。`
- `白名单数据加载成功，当前共有 3 个条目。`
- `白名单数据加载失败，将回退为空白名单，原因：...`
- `白名单数据保存成功，当前共有 3 个条目。`

### `NextBotAdapter/Services/PersistedWhitelistService.cs`

保留并统一：

- 白名单新增成功
- 白名单新增失败
- 白名单删除成功
- 白名单删除失败
- 白名单状态重新加载成功

示例文案方向：

- `已将玩家 Alice 添加到白名单。`
- `将玩家 Alice 添加到白名单失败，原因：...`
- `已将玩家 Alice 从白名单移除。`
- `将玩家 Alice 从白名单移除失败，原因：...`
- `白名单状态重新加载成功。当前启用状态为 True，区分大小写为 True，当前共有 3 个条目。`

### `NextBotAdapter/Services/ConfigurationReloadService.cs`

保留入口和成功闭环日志：

- `配置热重载请求已收到。`
- `配置热重载已完成。`

不在该层重复记录失败，避免与 endpoint 层重叠。

### `NextBotAdapter/Rest/ConfigEndpoints.cs`

保留接口边界失败日志：

- `配置热重载接口执行失败，原因：...`

### `NextBotAdapter/Plugin/NextBotAdapterPlugin.cs`

统一插件生命周期和运行时结果日志：

- `插件正在初始化。`
- `白名单服务初始化完成。`
- `REST 端点注册完成，共 4 个。`
- `玩家信息校验钩子注册完成。`
- `当前白名单配置已生效。启用状态为 True，区分大小写为 True，当前共有 3 个条目。`
- `白名单功能未启用，玩家进入服务器时将不会进行白名单校验。`
- `插件正在释放资源。`
- `玩家 Alice 已被拒绝进入服务器，原因：You are not on the whitelist.`

## 不做的事情

本次不做以下内容：

- 不引入复杂日志门面或分场景专用日志类
- 不新增 debug / trace 级别
- 不强行把全部日志改造成结构化对象日志
- 不为每个业务动作补开始 / 处理中 / 结束三段式日志
- 不记录原始请求体、响应体或整份配置对象

## 实施顺序

1. 升级 `PluginLogger` 并更新相关测试
2. 统一 `WhitelistConfigService` 的日志文案和等级
3. 统一 `PersistedWhitelistService` 的日志文案
4. 统一 `ConfigurationReloadService` 与 `ConfigEndpoints`
5. 统一 `NextBotAdapterPlugin`
6. 运行测试并确认行为未受影响

## 测试约束

- `PluginLoggerTests` 不应断言具体的墙上时钟时间值，而应断言日志前缀是否满足预期格式
- 时间戳断言应覆盖：ISO 8601 / RFC 3339 风格、毫秒精度、显式时区偏移
- 日志格式测试应同时验证 `[INFO]` / `[WARN]` / `[ERROR]` 与 `[NextBotAdapter]` 前缀存在
- 行为测试不以具体中文日志文案为契约，除非该测试本身就是专门验证日志格式

## 验收标准

完成后应满足以下条件：

- 本次改动范围内的现有英文业务日志统一改为中文
- `[Category]` 已移除
- 所有日志统一带有时间戳、等级和 `[NextBotAdapter]`
- `INFO` / `WARN` / `ERROR` 与 TShock 白 / 黄 / 红控制台输出保持一致
- 配置加载 / 回退 / 热重载 / 白名单持久化 / 玩家拒绝进入等关键链路都有日志
- 回退和策略拒绝类场景使用 `WARN`
- 真正失败类场景使用 `ERROR`
- 日志文案符合 Human-reading-first，自然、可读、便于排查
