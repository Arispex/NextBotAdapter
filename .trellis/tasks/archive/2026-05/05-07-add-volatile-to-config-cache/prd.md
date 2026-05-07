# fix: PluginConfigService._cached 加 volatile（ARM64 双检锁安全）

## Goal

给 `PluginConfigService._cached` 字段加 `volatile` 修饰符，符合 C# 双检锁标准模式，防止在弱内存模型架构（ARM64 / Apple Silicon）上的潜在内存可见性问题。

## What I already know

### 当前代码（V-P1 修复后）

`Services/Configuration/PluginConfigService.cs:21`：
```csharp
private NextBotAdapterConfig? _cached;
```

`Load()` 第 68 行 lock-free 读：
```csharp
public NextBotAdapterConfig Load()
{
    var existing = _cached;          // ← lock-free 读，无 acquire barrier
    if (existing is not null) return existing;
    lock (_cacheLock)
    {
        if (_cached is not null) return _cached;
        _cached = LoadFromDisk();    // ← lock 内写
        return _cached;
    }
}
```

### 风险（理论）

C# 双检锁的标准要求：lock-free 读取的字段必须用 `volatile` 修饰，否则在弱内存模型 CPU（ARM64 / Apple Silicon）上理论上可能：
- 看到 `_cached != null`（reference 写已可见）
- 但读到对象**字段**还未完全初始化（写重排导致 reference 比构造完成更早 publish）

`volatile` 加 acquire/release barrier，使得：
- volatile 写：所有 prior 写入 happen-before 该写入
- volatile 读：所有 subsequent 读取 happen-after 该读取

### 实际命中场景

- ❌ x86 / x64：TSO 内存模型几乎不会触发
- ⚠️ ARM64 / Apple Silicon：理论存在；实际触发概率仍低（NextBotAdapterConfig 是 record，构造已 strong publish）

属于 defense-in-depth。修法极简：单字符串加 `volatile` 修饰符。

## Decision (ADR-lite)

**Context**：V-P1 引入 `_cached` 缓存字段 + 双检锁。当前没加 `volatile`，理论上违反 C# 双检锁内存模型最佳实践。最终性能 audit 发现并标为 medium（confidence 72，仅 ARM64 风险）。

**Decision**：加 `volatile`。修法零代价、符合标准、防御性编程。

**Consequences**：
- 优点：双检锁内存可见性保证；ARM64 部署完全安全；零运行时开销（volatile 在 x64 上是 no-op）
- 缺点：无
- 待评估：无

## Requirements

- `PluginConfigService._cached` 字段加 `volatile` 修饰符
- 不动 `Load` / `InvalidateCache` / `Save` / `Reload` / `EnsureConfigComplete` / `TryUpdateConfig` 任何业务逻辑
- 现有 PluginConfigServiceTests 全部 pass

## Acceptance Criteria

- [ ] `_cached` 字段声明为 `private volatile NextBotAdapterConfig? _cached;`
- [ ] `dotnet build` 0 警告 0 错误
- [ ] `dotnet test` 全部通过（340/340 baseline 全部 pass，无新增测试需求——是单字符修饰符变化，行为不变）
- [ ] 不动其他文件

## Definition of Done

- 测试 green、build 干净
- 行为契约零变化（volatile 只影响内存可见性，运行时行为不变）

## Technical Approach

### 单文件 1 字段改动

`NextBotAdapter/Services/Configuration/PluginConfigService.cs:21`：

```csharp
// 改前
private NextBotAdapterConfig? _cached;

// 改后
private volatile NextBotAdapterConfig? _cached;
```

完。

### 不需要测试

`volatile` 修饰符不改变 single-threaded 行为，现有 cache invalidation 测试已经覆盖正确性。`volatile` 的内存模型保证无法用单元测试断言（需要 ARM64 + 大量并发 + 时间窗口才可能观察到差异）。

## Out of Scope

- 不引入 `Lazy<T>` / `LazyInitializer.EnsureInitialized` 替代当前模式（原模式正确，加 `volatile` 即可符合标准）
- 不改其他类似双检锁模式（项目里目前仅此一处双检锁）
- 不改 REST / 持久化 / 业务逻辑

## Technical Notes

### 涉及文件

- 产品代码：`NextBotAdapter/Services/Configuration/PluginConfigService.cs`（单字段加 `volatile`）

### 不需要改

- 测试、其他 service、Plugin、REST 端点、docs 全部不动
