# NextBotAdapter 日志升级实现计划

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 升级项目的后端日志系统，使目标链路统一使用带时间戳的 `PluginLogger`、符合 Human-reading-first 且遵循“动作 + 对象 + 结果 + 关键上下文”的中文日志文案，以及与 TShock 控制台颜色一致的日志等级。

**Architecture:** 保留 `NextBotAdapter/Services/PluginLogger.cs` 作为唯一日志入口，移除基于 category 的格式，改由统一入口负责生成时间戳、等级和插件前缀。然后更新配置、白名单、热重载和插件生命周期中的日志调用点，统一改为新 API，并按已批准的消息顺序与等级规则重写目标日志文案。

**Tech Stack:** C# / .NET 9、xUnit、TShock API、TerrariaApi.Server

---

## 文件映射

- 修改：`NextBotAdapter/Services/PluginLogger.cs`
  - 将基于 category 的格式替换为 `[timestamp] [LEVEL] [NextBotAdapter] <message>`。
  - 保留 `Info` / `Warn` / `Error` 到 `ConsoleInfo` / `ConsoleWarn` / `ConsoleError` 的映射。
  - 增加最小必要的内部辅助逻辑，用于时间戳格式化、动态内容单行化，以及超长动态内容的明确截断。
- 修改：`NextBotAdapter.Tests/PluginLoggerTests.cs`
  - 用时间戳、等级、插件前缀、单行化和截断行为的断言，替换旧的 category 格式断言。
- 修改：`NextBotAdapter/Services/WhitelistConfigService.cs`
  - 去掉 category 参数。
  - 将目标日志改为中文，并遵循“动作 + 对象 + 结果 + 关键上下文”。
  - 将回退类日志从 `Error` 调整为 `Warn`。
- 修改：`NextBotAdapter/Services/PersistedWhitelistService.cs`
  - 去掉 category 参数。
  - 将白名单新增、删除、重载日志统一改为中文。
- 修改：`NextBotAdapter/Services/ConfigurationReloadService.cs`
  - 去掉 category 参数。
  - 将请求开始和成功闭环日志统一改为中文。
- 修改：`NextBotAdapter/Rest/ConfigEndpoints.cs`
  - 去掉 category 参数。
  - 将接口失败日志统一改为中文。
- 修改：`NextBotAdapter/Plugin/NextBotAdapterPlugin.cs`
  - 去掉 category 参数。
  - 将生命周期、当前配置摘要、白名单未启用提示和玩家拒绝日志统一改为中文。
- 仅在需要时检查：`NextBotAdapter.Tests/ConfigEndpointsTests.cs`
  - 除非误改接口响应行为，否则预期不需要改动。
- 仅在需要时检查：`NextBotAdapter.Tests/WhitelistConfigServiceTests.cs`
  - 除非误改配置回退行为，否则预期不需要改动。

## 执行前提

- 所有命令都在当前 worktree 的仓库根目录下执行。
- 使用当前 worktree 内的相对路径，不写死主工作目录的绝对路径。
- 除非用户明确要求，否则不要创建 git commit。
- 验证阶段的内容搜索使用 `Grep` 工具，不使用 `rg` shell 命令。

## Chunk 1：升级统一日志入口与直接测试

### Task 1：将基于 category 的格式替换为带时间戳的统一日志格式

**Files:**
- 修改：`NextBotAdapter/Services/PluginLogger.cs:5-24`
- 测试：`NextBotAdapter.Tests/PluginLoggerTests.cs`

- [ ] **Step 1：先写失败中的 logger 格式测试**

更新 `NextBotAdapter.Tests/PluginLoggerTests.cs`，不再断言 `[NextBotAdapter][Config] ...`，改成更聚焦的格式测试，例如：

```csharp
using System.Text.RegularExpressions;
using NextBotAdapter.Services;

namespace NextBotAdapter.Tests;

public sealed class PluginLoggerTests
{
    [Fact]
    public void Format_ShouldIncludeTimestampLevelAndPluginPrefix()
    {
        var formatted = PluginLogger.Format("INFO", "加载白名单配置成功。");

        Assert.Matches(
            new Regex(@"^\[\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}(Z|[+-]\d{2}:\d{2})\] \[INFO\] \[NextBotAdapter\] 加载白名单配置成功。$"),
            formatted);
    }

    [Fact]
    public void Format_ShouldNormalizeControlCharactersIntoSingleLine()
    {
        var formatted = PluginLogger.Format("WARN", "处理配置热重载请求失败，原因：line1\nline2\tline3");

        Assert.DoesNotContain('\n', formatted);
        Assert.DoesNotContain('\r', formatted);
        Assert.DoesNotContain('\t', formatted);
        Assert.Contains("line1 line2 line3", formatted);
    }

    [Fact]
    public void Format_ShouldTruncateOverlongDynamicContent()
    {
        var formatted = PluginLogger.Format("ERROR", new string('x', 600));

        Assert.True(formatted.Length < 600 + 80);
        Assert.Contains("...", formatted);
    }
}
```

如果最终截断标记不是 `...`，请保持测试与选定的明确截断规则一致。

- [ ] **Step 2：运行 logger 测试，确认它先失败**

运行：

```bash
dotnet test NextBotAdapter.Tests/NextBotAdapter.Tests.csproj --filter "FullyQualifiedName~PluginLoggerTests"
```

预期：FAIL，因为当前 `PluginLogger.Format` 仍然接受 `(category, message)`，并且仍返回旧的 `[NextBotAdapter][Category]` 格式。

- [ ] **Step 3：编写最小实现**

更新 `NextBotAdapter/Services/PluginLogger.cs`，例如：

```csharp
using System;
using TShockAPI;

namespace NextBotAdapter.Services;

public static class PluginLogger
{
    public static string Format(string level, string message)
        => $"[{DateTimeOffset.Now:yyyy-MM-ddTHH:mm:ss.fffzzz}] [{level}] [NextBotAdapter] {Normalize(message)}";

    public static void Info(string message)
        => TShock.Log?.ConsoleInfo(Format("INFO", message));

    public static void Warn(string message)
        => TShock.Log?.ConsoleWarn(Format("WARN", message));

    public static void Error(string message)
        => TShock.Log?.ConsoleError(Format("ERROR", message));

    private static string Normalize(string message)
    {
        var singleLine = message
            .Replace("\r", " ")
            .Replace("\n", " ")
            .Replace("\t", " ");

        return singleLine.Length <= 300
            ? singleLine
            : singleLine[..297] + "...";
    }
}
```

保持 helper 尽量简单。不要把 category 支持加回来。如果需要额外折叠重复空格，也应该放在 helper 里统一处理，而不是由每个调用点自己处理。如果你选择的截断长度不是 `300`，请在实现注释或测试命名里明确说明，并保持测试同步。

- [ ] **Step 4：再次运行 logger 测试，确认它通过**

运行：

```bash
dotnet test NextBotAdapter.Tests/NextBotAdapter.Tests.csproj --filter "FullyQualifiedName~PluginLoggerTests"
```

预期：PASS。

- [ ] **Step 5：把 commit 留给用户明确授权的步骤**

这里不要创建 commit。如果用户之后要求提交，再只 stage 本 chunk 的改动，并按 Conventional Commits 创建提交。

## Chunk 2：重写配置与白名单持久化日志

### Task 2：更新 `WhitelistConfigService` 的日志等级和消息文案

**Files:**
- 修改：`NextBotAdapter/Services/WhitelistConfigService.cs:26-86`
- 测试：`NextBotAdapter.Tests/WhitelistConfigServiceTests.cs`

- [ ] **Step 1：仅在确有需要时补一个聚焦的回退安全测试**

在修改行为前，先确认 helper 提取或日志相关调整是否会影响文件回退语义。如果会，就在 `NextBotAdapter.Tests/WhitelistConfigServiceTests.cs` 里补一个相邻测试；否则保持现有测试不变，直接依赖现有回退覆盖。

只有在确有需要时，才添加类似这样的最小测试：

```csharp
[Fact]
public void LoadWhitelist_ShouldKeepInvalidFileContentsWhenFallbackOccurs()
{
    var service = CreateService();
    const string invalidJson = "{invalid json}";
    File.WriteAllText(service.WhitelistFilePath, invalidJson);

    var store = service.LoadWhitelist();

    Assert.Equal(WhitelistStore.Empty, store);
    Assert.Equal(invalidJson, File.ReadAllText(service.WhitelistFilePath));
}
```

- [ ] **Step 2：改实现前先跑配置服务测试**

运行：

```bash
dotnet test NextBotAdapter.Tests/NextBotAdapter.Tests.csproj --filter "FullyQualifiedName~WhitelistConfigServiceTests"
```

预期：PASS，用来建立回退行为的基线。

- [ ] **Step 3：按批准的风格重写配置服务日志**

更新 `NextBotAdapter/Services/WhitelistConfigService.cs`，去掉 category 参数，并改成类似下面的消息：

```csharp
PluginLogger.Info("创建默认白名单配置文件成功。");
PluginLogger.Info($"加载白名单配置成功。当前启用状态为 {configSettings.Enabled}，区分大小写为 {configSettings.CaseSensitive}。");
PluginLogger.Warn($"加载白名单配置失败，将回退为默认配置，原因：{ex.Message}");
PluginLogger.Info($"保存白名单配置成功。当前启用状态为 {settings.Enabled}，区分大小写为 {settings.CaseSensitive}。");
PluginLogger.Info("创建默认白名单文件成功。");
PluginLogger.Info($"加载白名单数据成功，当前共有 {whitelist.Users.Count} 个条目。");
PluginLogger.Warn($"加载白名单数据失败，将回退为空白名单，原因：{ex.Message}");
PluginLogger.Info($"保存白名单数据成功，当前共有 {store.Users.Count} 个条目。");
```

实现注意事项：
- 回退时保持无效文件内容不被覆盖。
- 不要在 `EnsureDirectory()` 里加日志。
- 如果为了记录日志需要引入局部变量保存已加载的 settings，保持实现简单即可。

- [ ] **Step 4：改完后重新跑配置服务测试**

运行：

```bash
dotnet test NextBotAdapter.Tests/NextBotAdapter.Tests.csproj --filter "FullyQualifiedName~WhitelistConfigServiceTests"
```

预期：PASS。

- [ ] **Step 5：把 commit 留给用户明确授权的步骤**

这里不要创建 commit。如果用户之后要求提交，再只 stage 本 chunk 的改动，并按 Conventional Commits 创建提交。

### Task 3：更新 `PersistedWhitelistService` 的业务日志

**Files:**
- 修改：`NextBotAdapter/Services/PersistedWhitelistService.cs:23-69`
- 验证：修改后跑完整测试集，因为当前仓库没有专门覆盖 `PersistedWhitelistService` 的测试文件

- [ ] **Step 1：先确认当前没有专门的 persistence-wrapper 测试可改**

检查测试树，确认当前没有现成的 `PersistedWhitelistService` 测试文件。不要把 `WhitelistServiceTests` 误当成 wrapper 的覆盖。

- [ ] **Step 2：将持久化白名单日志改成中文**

更新 `NextBotAdapter/Services/PersistedWhitelistService.cs`，例如：

```csharp
PluginLogger.Info($"添加玩家 {user} 到白名单成功。");
PluginLogger.Warn($"添加玩家 {user} 到白名单失败，原因：{error.Message}");
PluginLogger.Info($"将玩家 {user} 移出白名单成功。");
PluginLogger.Warn($"将玩家 {user} 移出白名单失败，原因：{error.Message}");
PluginLogger.Info($"重新加载白名单状态成功。当前启用状态为 {_inner.Settings.Enabled}，区分大小写为 {_inner.Settings.CaseSensitive}，当前共有 {_inner.GetAll().Count} 个条目。");
```

保持持久化行为不变。不要新增“允许进入”的日志，也不要在这里重复记录玩家拒绝进入的结果。

- [ ] **Step 3：修改后跑完整测试集**

运行：

```bash
dotnet test NextBotAdapter.Tests/NextBotAdapter.Tests.csproj
```

预期：PASS。

- [ ] **Step 4：把 commit 留给用户明确授权的步骤**

这里不要创建 commit。如果用户之后要求提交，再只 stage 本 chunk 的改动，并按 Conventional Commits 创建提交。

## Chunk 3：重写热重载接口与插件生命周期日志

### Task 4：更新热重载 service 与 endpoint 的失败日志

**Files:**
- 修改：`NextBotAdapter/Services/ConfigurationReloadService.cs:3-10`
- 修改：`NextBotAdapter/Rest/ConfigEndpoints.cs:14-28`
- 测试：`NextBotAdapter.Tests/ConfigEndpointsTests.cs`

- [ ] **Step 1：改实现前先跑 endpoint 测试**

运行：

```bash
dotnet test NextBotAdapter.Tests/NextBotAdapter.Tests.csproj --filter "FullyQualifiedName~ConfigEndpointsTests"
```

预期：PASS。

- [ ] **Step 2：重写热重载日志，但不要改接口行为**

更新两个文件，使它们使用：

```csharp
PluginLogger.Info("开始处理配置热重载请求。");
PluginLogger.Info("处理配置热重载请求成功。");
PluginLogger.Error($"处理配置热重载请求失败，原因：{ex.Message}");
```

实现注意事项：
- 失败日志只保留在 `ConfigEndpoints`。
- 不要修改 `ErrorCodes.ConfigReloadFailed`。
- 不要修改响应 payload 的结构。

- [ ] **Step 3：改完后重新跑 endpoint 测试**

运行：

```bash
dotnet test NextBotAdapter.Tests/NextBotAdapter.Tests.csproj --filter "FullyQualifiedName~ConfigEndpointsTests"
```

预期：PASS。

- [ ] **Step 4：把 commit 留给用户明确授权的步骤**

这里不要创建 commit。如果用户之后要求提交，再只 stage 本 chunk 的改动，并按 Conventional Commits 创建提交。

### Task 5：更新插件生命周期与玩家拒绝日志

**Files:**
- 修改：`NextBotAdapter/Plugin/NextBotAdapterPlugin.cs:25-75`
- 测试：修改后跑完整测试集

- [ ] **Step 1：编辑前先确认只存在编译层面的风险**

通读 `NextBotAdapter/Plugin/NextBotAdapterPlugin.cs`，确认计划中的改动仅限于日志调用签名和日志文本。不要改变 hook 注册顺序、endpoint 注册顺序或玩家断开连接行为。

- [ ] **Step 2：按批准的顺序将插件日志改成中文**

更新插件文件，例如：

```csharp
PluginLogger.Info("开始初始化插件。");
PluginLogger.Info("初始化白名单服务完成。");
PluginLogger.Info($"注册 REST 端点完成，共 {EndpointRegistrar.CreateCommands().Count} 个。");
PluginLogger.Info("注册玩家信息校验钩子完成。");
PluginLogger.Info($"应用当前白名单配置完成。启用状态为 {_whitelistService.Settings.Enabled}，区分大小写为 {_whitelistService.Settings.CaseSensitive}，当前共有 {_whitelistService.GetAll().Count} 个条目。");
PluginLogger.Warn("检测到白名单功能未启用，玩家进入服务器时将不会进行白名单校验。");
PluginLogger.Info("开始释放插件资源。");
PluginLogger.Warn($"拒绝玩家 {args.Name} 进入服务器，原因：{denialReason ?? "You are not on the whitelist."}");
```

注意：REST 端点数量必须使用 `EndpointRegistrar.CreateCommands().Count` 的实际运行值，不要把 spec 示例中的 `4` 写死。

不要新增 allow-path 日志。保持 `Disconnect` 使用的原有提示消息不变。

- [ ] **Step 3：改完插件后跑完整测试集**

运行：

```bash
dotnet test NextBotAdapter.Tests/NextBotAdapter.Tests.csproj
```

预期：PASS。

- [ ] **Step 4：把 commit 留给用户明确授权的步骤**

这里不要创建 commit。如果用户之后要求提交，再只 stage 本 chunk 的改动，并按 Conventional Commits 创建提交。

## Chunk 4：最终验证与收尾

### Task 6：执行完整验证

**Files:**
- 验证前面所有被修改的文件

- [ ] **Step 1：确认 logger 调用点不再使用 category 参数**

使用 `Grep` 工具，在 `NextBotAdapter/**/*.cs` 范围内搜索 `PluginLogger.Info(`、`PluginLogger.Warn(` 和 `PluginLogger.Error(`，然后逐个确认调用点现在都只传一个消息参数。

预期：所有剩余调用都使用新的单参数 logger API。

- [ ] **Step 2：确认 `[Category]` 假设已经消失**

使用 `Grep` 工具，在整个仓库里搜索字面量模式 `\[NextBotAdapter\]\[`。

预期：source 和 tests 中都不再有匹配。

- [ ] **Step 3：跑完整测试集**

运行：

```bash
dotnet test NextBotAdapter.Tests/NextBotAdapter.Tests.csproj
```

预期：PASS。

- [ ] **Step 4：检查当前工作树 diff 是否出现范围漂移**

运行：

```bash
git diff --stat
git diff -- NextBotAdapter/Services/PluginLogger.cs NextBotAdapter/Services/WhitelistConfigService.cs NextBotAdapter/Services/PersistedWhitelistService.cs NextBotAdapter/Services/ConfigurationReloadService.cs NextBotAdapter/Rest/ConfigEndpoints.cs NextBotAdapter/Plugin/NextBotAdapterPlugin.cs NextBotAdapter.Tests/PluginLoggerTests.cs NextBotAdapter.Tests/WhitelistConfigServiceTests.cs
```

预期：只包含日志格式、日志文案、日志等级，以及直接相关的 logger 测试更新。

- [ ] **Step 5：询问用户要不要提交 commit 或直接交付**

不要自动提交。如果工作树状态清晰且验证通过，总结修改过的文件，并询问用户是否希望你创建一个或多个符合 Conventional Commits 的提交。
