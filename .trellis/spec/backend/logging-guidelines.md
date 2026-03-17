# Logging Guidelines

> How logging is done in this project.

---

## Overview

All plugin logs should go through `PluginLogger`, which applies the project's shared format:

```text
[timestamp] [LEVEL] [NextBotAdapter] message
```

Examples:
- `NextBotAdapter/Services/PluginLogger.cs`
- `NextBotAdapter.Tests/PluginLoggerTests.cs`

Do not handcraft log prefixes in business code. Call `PluginLogger.Info(...)`, `PluginLogger.Warn(...)`, or `PluginLogger.Error(...)` and let the logger normalize the final output.

The existing codebase primarily writes log messages in Chinese and follows an action-first style such as:

- `开始初始化插件。`
- `加载白名单配置成功。当前启用状态为 ...`
- `处理配置热重载请求失败，原因：...`

---

## Log Levels

### `Info`

Use for successful lifecycle and state-change events.

Current examples:
- plugin startup and teardown: `NextBotAdapter/Plugin/NextBotAdapterPlugin.cs`
- config file creation, loading, saving: `NextBotAdapter/Services/WhitelistConfigService.cs`
- whitelist reload and successful mutations: `NextBotAdapter/Services/PersistedWhitelistService.cs`

### `Warn`

Use when the plugin can continue safely but behavior is degraded or a request is rejected.

Current examples:
- whitelist feature disabled during startup: `NextBotAdapter/Plugin/NextBotAdapterPlugin.cs`
- malformed config fallback: `NextBotAdapter/Services/WhitelistConfigService.cs`
- failed whitelist add / remove attempts: `NextBotAdapter/Services/PersistedWhitelistService.cs`
- denied player join: `NextBotAdapter/Plugin/NextBotAdapterPlugin.cs`

### `Error`

Use when an operation fails unexpectedly and the failure cannot be treated as a normal business branch.

Current example:
- config reload exception handling: `NextBotAdapter/Rest/ConfigEndpoints.cs`

---

## Structured Logging

The project does not use a JSON logging framework, but it still enforces a stable structure through `PluginLogger`.

`PluginLogger` currently guarantees:

- timestamp with offset
- normalized single-line message body
- no embedded newlines or tabs
- truncation of overlong dynamic content to 300 characters
- stable plugin prefix `[NextBotAdapter]`

Examples:
- implementation: `NextBotAdapter/Services/PluginLogger.cs`
- expected behavior: `NextBotAdapter.Tests/PluginLoggerTests.cs`

### Message style

Prefer:

- action + result
- include concrete reason on failure
- include small, high-value dynamic context such as user name, enabled flags, counts

Examples:
- `添加玩家 {user} 到白名单成功。`
- `将玩家 {user} 移出白名单失败，原因：{error.Message}`
- `拒绝玩家 {args.Name} 进入服务器，原因：{denialReason}`

---

## What to Log

Log state changes and operationally important boundaries:

- plugin initialization and disposal
- REST endpoint registration
- hook registration
- config file creation, loading, saving, and reload
- whitelist mutations
- degraded-mode fallbacks after malformed file input
- request failures caused by unexpected exceptions
- access control decisions that affect player connection flow

Good examples:
- `NextBotAdapter/Plugin/NextBotAdapterPlugin.cs`
- `NextBotAdapter/Services/WhitelistConfigService.cs`
- `NextBotAdapter/Services/PersistedWhitelistService.cs`
- `NextBotAdapter/Rest/ConfigEndpoints.cs`

---

## What NOT to Log

- Do not call `TShock.Log` directly from ordinary business code when `PluginLogger` can be used.
- Do not log raw file contents, full JSON payloads, or full request dumps.
- Do not log secrets or future credential-like configuration values.
- Do not emit multiline stack-trace-shaped blobs as part of the message string.
- Do not spam logs for ordinary successful reads that add no operational value.
- Do not hand-format timestamps, levels, or plugin names outside `PluginLogger`.
