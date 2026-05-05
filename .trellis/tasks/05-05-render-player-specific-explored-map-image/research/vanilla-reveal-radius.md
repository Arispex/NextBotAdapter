# Vanilla Terraria Map Reveal Radius

- **Query**: Vanilla 客户端在玩家移动时实际把哪些 tile 标记为 explored，精确公式是什么？
- **Scope**: external (vanilla Terraria 1.4.4 反编译源码)
- **Date**: 2026-05-05

## Conclusion (TL;DR)

**Vanilla 客户端没有"以玩家为中心的固定 reveal 半径"。** 客户端每帧把整个**屏幕视口**（按 tile 计算就是 `ScaledSize / 16`）作为 reveal 区域写入 `WorldMap._tiles`。具体由 `LightingEngine.ExportToMiniMap()` 调用 `Main.Map.UpdateLighting(i, j, light)` 完成，区域 = `Main.screenPosition + GameViewMatrix.Translation` ⇒ 顶点起点，宽高 = `Main.screenWidth − 2·Translation.X`、`Main.screenHeight − 2·Translation.Y`。Light > 0 的 tile 即为 revealed（`WorldMap.IsRevealed(x, y) => _tiles[x, y].Light > 0`）。

⇒ **reveal 区域跟着 zoom 和窗口分辨率走，不是常量**。在常见的 1920×1080、zoom = 1.0 下，半范围约为 **60 tile 宽 × 34 tile 高**（半 extent，非边长），中心在屏幕中心而非玩家精确位置（玩家居中但有相机滞后）。当前代码里的 `RevealHalfExtent = 20`（41×41）远小于 vanilla 真实值，用户反馈"实际探索区域比 41×41 大至少 2 倍多"完全成立。

**推荐服务端近似（默认 1920×1080，zoom = 1）：**
- `RevealHalfExtentX = 70`（≈ 1920/16/2 + 余量 → 141 tile 宽）
- `RevealHalfExtentY = 40`（≈ 1080/16/2 + 余量 → 81 tile 高）

也可以设计成同时向玩家四周给一个稍大的方形（例如 `RevealHalfExtent = 70`，给一个 141×141 的方形），代价是纵向多覆盖一些 tile（不影响正确性，只是更"宽容"）。

---

## Reveal Trigger

调用链（Color/默认光照模式，1.4.4）：

```
Main.Update (tick)
 └─ Main.DoLightTiles()
     └─ Main.GetAreaToLight(out firstTileX, out lastTileX, out firstTileY, out lastTileY)
         ├─ vector  = Camera.ScaledPosition  // = Main.screenPosition + GameViewMatrix.Translation
         └─ vector2 = Camera.ScaledSize      // = (screenWidth, screenHeight) − 2·Translation
     └─ Lighting.LightTiles(firstTileX, lastTileX, firstTileY, lastTileY)
         └─ _activeEngine.ProcessArea(rect)   // _activeEngine == LightingEngine
             ├─ state == Scan       → ProcessScan(area)             // area.Inflate(28, 28)
             ├─ state == Blur       → ProcessBlur() + Present()
             └─ state == MinimapUpdate → ExportToMiniMap()           // ★ 写 Map
```

`LightingEngine` 是一个 4 阶段状态机（`MinimapUpdate → ExportMetrics → Scan → Blur`，每帧推进一阶段），所以**每 4 帧** `ExportToMiniMap()` 才被调用一次，但每次都把整个 `_activeProcessedArea`（即上一轮 Scan 时的区域）一次性写入 `WorldMap`。

`ExportToMiniMap()` 内部（`Terraria.Graphics.Light.LightingEngine.cs:240-270`）：

```csharp
private void ExportToMiniMap()
{
    if (!Main.mapEnabled || _activeProcessedArea.Width <= 0 || _activeProcessedArea.Height <= 0) return;
    // 把 Scan 时 Inflate(+28) 的 padding 减掉，回到屏幕原始范围
    Rectangle area = new Rectangle(
        _activeProcessedArea.X + 28, _activeProcessedArea.Y + 28,
        _activeProcessedArea.Width - 56, _activeProcessedArea.Height - 56);
    Rectangle value = new Rectangle(0, 0, Main.maxTilesX, Main.maxTilesY);
    value.Inflate(-40, -40);                     // 跳过世界边缘 40 tile
    area = Rectangle.Intersect(area, value);
    Main.mapMinX = area.Left; Main.mapMinY = area.Top;
    Main.mapMaxX = area.Right; Main.mapMaxY = area.Bottom;
    FastParallel.For(area.Left, area.Right, (start, end, _) => {
        for (int i = start; i < end; i++)
            for (int j = area.Top; j < area.Bottom; j++) {
                Vector3 v = _activeLightMap[i - _activeProcessedArea.X, j - _activeProcessedArea.Y];
                float num = Math.Max(Math.Max(v.X, v.Y), v.Z);
                byte light = (byte)Math.Min(255, (int)(num * 255f));
                Main.Map.UpdateLighting(i, j, light);   // ← 真正的 reveal 写入
            }
    }, null);
    Main.updateMap = true;
}
```

`WorldMap.UpdateLighting`（`Terraria.Map/WorldMap.cs:48-62`）只在 `light > 0 || existing.Light > 0` 时实际写入；`IsRevealed(x, y) => _tiles[x, y].Light > 0`。所以只要那一帧某 tile 的合成光照亮度 > 0（即 RGB max ≥ 1/255）就会被 reveal。

> Retro / White / Trippy 模式走 `LegacyLighting.ProcessArea`，行为大同小异：把 `area` 再 `Inflate(Lighting.OffScreenTiles, …)` 后扫描，并在 `b > 18 || existing.Light > 0` 条件下写 `Main.Map.UpdateLighting`。Color 模式 `OffScreenTiles = 35` / Old 模式 `45`（参见 `Terraria/Lighting.cs:17, 43`）。

---

## Range Formula

精确公式（`Terraria/Main.cs:60422-60434`）：

```csharp
public static void GetAreaToLight(out int firstTileX, out int lastTileX,
                                  out int firstTileY, out int lastTileY)
{
    Vector2 vector  = Camera.ScaledPosition;            // 默认（新光照）
    Vector2 vector2 = Camera.ScaledSize;
    if (!Lighting.UsingNewLighting) {                   // Retro/White/Trippy
        vector  = Camera.UnscaledPosition;
        vector2 = Camera.UnscaledSize;
    }
    firstTileX = (int)Math.Floor( vector.X              / 16f) - 1;
    lastTileX  = (int)Math.Floor((vector.X + vector2.X) / 16f) + 2;
    firstTileY = (int)Math.Floor( vector.Y              / 16f) - 1;
    lastTileY  = (int)Math.Floor((vector.Y + vector2.Y) / 16f) + 2;
}
```

`Camera`（`Terraria.Graphics/Camera.cs`）：

```csharp
public Vector2 UnscaledPosition => Main.screenPosition;
public Vector2 UnscaledSize     => new Vector2(Main.screenWidth, Main.screenHeight);
public Vector2 ScaledPosition   => UnscaledPosition + GameViewMatrix.Translation;
public Vector2 ScaledSize       => UnscaledSize - GameViewMatrix.Translation * 2f;
```

注意 `LightingEngine.ProcessScan()` 又会 `area.Inflate(28, 28)` 把 light scan 区域扩到屏幕外 +28 tile（用来给屏幕边缘的 tile 保留正确光照梯度），但 `ExportToMiniMap` 把这 +28 padding 去掉了，所以**reveal 范围 ≡ `(firstTileX, firstTileY, lastTileX − firstTileX, lastTileY − firstTileY)`**，即 `ScaledSize / 16` ≈ 屏幕视口对应的 tile 数。

### 实例（vanilla 关键常量）

| 常量 / 字段 | 默认值 | 来源 |
|---|---|---|
| `Main.screenWidth` (默认) | 1152 | `Main.cs:1755` |
| `Main.screenHeight` (默认) | 864 | `Main.cs:1757` |
| `Main.LogicCheckScreenWidth` | 1920 | `Main.cs:1930` |
| `Main.LogicCheckScreenHeight` | 1200 | `Main.cs:1932` |
| `Main.GameZoomTarget` | 1.0（clamp 1.0–2.0） | `Main.cs:225, 15461` |
| `Lighting.OffScreenTiles` (Color) | 35 | `Lighting.cs:43` |
| `Lighting.OffScreenTiles` (Old/Legacy) | 45 默认 | `Lighting.cs:17` |
| `LightingEngine.AREA_PADDING` | 28 | `LightingEngine.cs:33` |
| `WorldMap.BlackEdgeWidth` | 40 | `WorldMap.cs:15` |
| `Main.offScreenRange` | 200 px (≈ 12.5 tile) | `Main.cs:872` (跟 reveal 无关，sprite culling 用) |

### 不同分辨率下的 reveal 视口估算（zoom=1）

| 分辨率 | tile 宽 (`screenWidth / 16`) | tile 高 (`screenHeight / 16`) | 半 extent X | 半 extent Y |
|---|---|---|---|---|
| 1152×864（默认） | 72 | 54 | 36 | 27 |
| 1920×1080 | 120 | 67–68 | 60 | 34 |
| 2560×1440 | 160 | 90 | 80 | 45 |
| 3840×2160 | 240 | 135 | 120 | 67 |

zoom = 2.0 时 `ScaledSize` 缩半，覆盖区减半。

> 当前 `RevealHalfExtent = 20`（41×41）≈ vanilla 默认 1152×864 时纵向半 extent (27) 的 0.74 倍、横向半 extent (36) 的 0.55 倍；对 1920×1080 玩家则严重低估（1/3 ~ 1/2）。

---

## Dependencies on Client-Only State

服务端**完全无法直接获取**这些字段（它们都是客户端运行时实例字段，从未通过 Terraria 协议同步）：

- `Main.screenPosition` (Vector2 px)
- `Main.screenWidth`, `Main.screenHeight` (int px)
- `Main.GameViewMatrix.Zoom`, `Main.GameViewMatrix.Translation`
- `Main.GameZoomTarget`
- `Camera.*`
- `_activeProcessedArea` / `_activeLightMap`（LightingEngine 私有实例字段）

服务端唯一能拿到的是：玩家逻辑位置（`PlayerUpdate` 包里的 `position`、`velocity`），玩家是否使用望远镜、night vision 等 buff（影响 vanilla 光照衰减但不影响 reveal 区域大小，只影响 light 强度阈值）。

⇒ 任何想"精确"复现客户端 reveal 的尝试都要求服务端假定一个分辨率 + zoom 组合。

---

## Lighting vs Reveal

| 阶段 | 区域 | 用途 |
|---|---|---|
| `Lighting.LightTiles(area)` 输入 | `(firstTileX..lastTileX, firstTileY..lastTileY)` ≈ 屏幕视口 + 1 tile padding | scan 入口 |
| `LightingEngine.ProcessScan` 内部 | 上述 `Inflate(+28)` | 让屏幕边缘亮度梯度可计算 |
| `ExportToMiniMap` 实际 reveal 写入 | scan 区域 `Deflate(28)` ⇒ **屏幕视口本身** | 写 `WorldMap.UpdateLighting` |
| `WorldMap.IsRevealed(x, y)` 判定 | `_tiles[x, y].Light > 0` | 任何亮度 > 0 即 reveal |

所以："**lighting 范围 = reveal 范围 + 28 tile padding（计算用）**"，但 reveal 落到 map 上的还是屏幕视口大小。

---

## Source Code Snippets

### 1. WorldMap reveal 判定（`Terraria.Map/WorldMap.cs:43-62`）

```csharp
public bool IsRevealed(int x, int y)
{
    return _tiles[x, y].Light > 0;
}

public bool UpdateLighting(int x, int y, byte light)
{
    MapTile other = _tiles[x, y];
    if (light == 0 && other.Light == 0) return false;
    MapTile mapTile = MapHelper.CreateMapTile(x, y, Math.Max(other.Light, light));
    if (mapTile.Equals(ref other)) return false;
    _tiles[x, y] = mapTile;
    return true;
}
```

### 2. Camera scaled rectangle（`Terraria.Graphics/Camera.cs`）

```csharp
public Vector2 UnscaledPosition => Main.screenPosition;
public Vector2 UnscaledSize     => new Vector2(Main.screenWidth, Main.screenHeight);
public Vector2 ScaledPosition   => UnscaledPosition + GameViewMatrix.Translation;
public Vector2 ScaledSize       => UnscaledSize - GameViewMatrix.Translation * 2f;
public Vector2 Center           => UnscaledPosition + UnscaledSize * 0.5f;
```

### 3. `GetAreaToLight`（`Terraria/Main.cs:60422-60434`）

```csharp
public static void GetAreaToLight(out int firstTileX, out int lastTileX,
                                  out int firstTileY, out int lastTileY)
{
    Vector2 vector  = Camera.ScaledPosition;
    Vector2 vector2 = Camera.ScaledSize;
    if (!Lighting.UsingNewLighting) { vector = Camera.UnscaledPosition; vector2 = Camera.UnscaledSize; }
    firstTileX = (int)Math.Floor( vector.X              / 16f) - 1;
    lastTileX  = (int)Math.Floor((vector.X + vector2.X) / 16f) + 2;
    firstTileY = (int)Math.Floor( vector.Y              / 16f) - 1;
    lastTileY  = (int)Math.Floor((vector.Y + vector2.Y) / 16f) + 2;
}
```

### 4. `ExportToMiniMap`（`Terraria.Graphics.Light/LightingEngine.cs:240-270`）

```csharp
private void ExportToMiniMap()
{
    if (!Main.mapEnabled || _activeProcessedArea.Width <= 0 || _activeProcessedArea.Height <= 0) return;
    Rectangle area = new Rectangle(_activeProcessedArea.X + 28, _activeProcessedArea.Y + 28,
                                   _activeProcessedArea.Width - 56, _activeProcessedArea.Height - 56);
    Rectangle value = new Rectangle(0, 0, Main.maxTilesX, Main.maxTilesY);
    value.Inflate(-40, -40);
    area = Rectangle.Intersect(area, value);
    Main.mapMinX = area.Left;  Main.mapMinY = area.Top;
    Main.mapMaxX = area.Right; Main.mapMaxY = area.Bottom;
    FastParallel.For(area.Left, area.Right, (start, end, _) => {
        for (int i = start; i < end; i++)
            for (int j = area.Top; j < area.Bottom; j++) {
                Vector3 v = _activeLightMap[i - _activeProcessedArea.X, j - _activeProcessedArea.Y];
                float num = Math.Max(Math.Max(v.X, v.Y), v.Z);
                byte light = (byte)Math.Min(255, (int)(num * 255f));
                Main.Map.UpdateLighting(i, j, light);
            }
    }, null);
    Main.updateMap = true;
}
```

### 5. 旧光照 (`Terraria.Graphics.Light/LegacyLighting.cs:295-325`)

```csharp
public void ProcessArea(Rectangle area)
{
    ...
    if (IsColorOrWhiteMode) { _offScreenTiles2 = 34; Lighting.OffScreenTiles = 40; }
    else                    { _offScreenTiles2 = 18; Lighting.OffScreenTiles = 23; }
    _requestedRectLeft  = area.Left;  _requestedRectRight  = area.Right;
    _requestedRectTop   = area.Top;   _requestedRectBottom = area.Bottom;
    _expandedRectLeft   = _requestedRectLeft   - Lighting.OffScreenTiles;
    _expandedRectTop    = _requestedRectTop    - Lighting.OffScreenTiles;
    _expandedRectRight  = _requestedRectRight  + Lighting.OffScreenTiles;
    _expandedRectBottom = _requestedRectBottom + Lighting.OffScreenTiles;
    ...
}
```

旧光照在 `_expandedRect` 内 `b > 18 || Map[i,j].Light > 0` 时调 `Main.Map.UpdateLighting(i, j, b)`，所以**Retro/White 模式下 reveal 范围 = 屏幕视口 + ~40 tile padding 每一边**（比新光照大）。

### 6. WorldMap 边缘 padding（`Terraria.Map/WorldMap.cs:11-12, 162-192`）

```csharp
public const int BlackEdgeWidth = 40;
// ClearEdges() 把 _tiles 在世界四个边的 40 tile 厚边带永久清零。
```

⇒ 世界最外层 40 tile（每边）永远不会 revealed。这是对 reveal 区域的硬上限，需要在服务端实现里同样夹住，否则会污染地图边缘。

---

## Recommended Server-Side Approximation

### 主推方案：固定半 extent（按 1920×1080 / zoom=1 取值，留余量）

```csharp
public static class VanillaRevealApproximation
{
    /// 按 1920×1080 主流分辨率，zoom = 1.0，加 ~10% 余量。
    /// 1920/16 = 120 → 半 60 + 10 余量 = 70。
    /// 1080/16 ≈ 67 → 半 34 + 6  余量 = 40。
    public const int RevealHalfExtentX = 70;
    public const int RevealHalfExtentY = 40;

    /// WorldMap 边缘 40 tile 永远不会 reveal（参考 WorldMap.BlackEdgeWidth）。
    public const int WorldEdgePadding   = 40;
}
```

⇒ 矩形 reveal box = 141 tile 宽 × 81 tile 高，是当前 41×41 的 ~6.8 倍面积，符合用户"实际探索范围比 41×41 大至少 2 倍多"的反馈，并对常见 1080p 玩家近似 1:1。

### 备选方案：保守上界（覆盖 4K 用户）

```csharp
public const int RevealHalfExtentX = 130;   // 3840/16/2 + 余量 = 120+10
public const int RevealHalfExtentY = 75;    // 2160/16/2 + 余量 = 67+8
```

代价：服务端会比 4K 玩家实际看到的多 reveal 一点 tile，这只会让玩家"早一点知道附近的地形"，不会暴露任何隐私或破坏机制，因为服务端写的是该玩家自己的 explored map。

### 需要在写入逻辑里同时实现的"硬限制"

1. `tileX / tileY` 必须 clamp 到 `[0, maxTilesX-1]` / `[0, maxTilesY-1]`。
2. 应该再把整体 reveal 矩形和 `[40, maxTilesX-40] × [40, maxTilesY-40]` 取交集，因为 vanilla `WorldMap.ClearEdges()` 永远不会留下边缘 40 tile 的 reveal。
3. 玩家位置应使用 `position + halfPlayerSize`（**player 中心**，不是脚下），因为 vanilla 摄像机 ≈ 居中跟随玩家中心点。`PlayerUpdate` 里的 `position` 是玩家**左上角 px**，应该 +11 px (X) 和 +21 px (Y) 转 tile（playerWidth=22, playerHeight=42）。
4. 因为 vanilla 没有"以玩家为中心"的 reveal（它跟相机走，相机有 `cameraLerp` / `CurrentPan` 滞后，最大 ±约 screenWidth/2 px ≈ 60 tile），上面 X 半 extent 70 tile 已经足够包含 zoom=1 / 1080p 的 panning 偏移。

### 进阶方案（不推荐 MVP）

如果想真正 1:1 模拟，需要：
- 让客户端 mod 把 `Main.screenWidth/Height/screenPosition/GameViewMatrix.Zoom` 周期上报给服务端。
- 服务端按上报的 viewport 算 reveal area。
- 但这需要客户端协作，背离插件"纯服务端模拟"的目标。

---

## References

### Decompiled vanilla Terraria 1.4.4 source (from Gothbr4dn/terraria-source-code GitHub mirror)

- `https://raw.githubusercontent.com/Gothbr4dn/terraria-source-code/main/1.4.4.0/Terraria.Map/WorldMap.cs` (本地缓存 `/tmp/WorldMap_1440.cs`)
- `https://raw.githubusercontent.com/Gothbr4dn/terraria-source-code/main/1.4.4.0/Terraria.Map/MapHelper.cs` (`/tmp/MapHelper_1440.cs`)
- `https://raw.githubusercontent.com/Gothbr4dn/terraria-source-code/main/1.4.4.0/Terraria/Lighting.cs` (`/tmp/Lighting_1440.cs`)
- `https://raw.githubusercontent.com/Gothbr4dn/terraria-source-code/main/1.4.4.0/Terraria.Graphics.Light/LightingEngine.cs` (`/tmp/LightingEngine_1440.cs`)
- `https://raw.githubusercontent.com/Gothbr4dn/terraria-source-code/main/1.4.4.0/Terraria.Graphics.Light/LegacyLighting.cs` (`/tmp/LegacyLighting_1440.cs`)
- `https://raw.githubusercontent.com/Gothbr4dn/terraria-source-code/main/1.4.4.0/Terraria.Graphics/Camera.cs` (`/tmp/Camera_1440.cs`)
- `https://raw.githubusercontent.com/Gothbr4dn/terraria-source-code/main/1.4.4.0/Terraria/Main.cs` (`/tmp/Main_1440.cs`)，关键引用行：
  - `1755-1757`: `screenWidth = 1152`, `screenHeight = 864` 默认
  - `872`: `offScreenRange = 200`（与 reveal 无关）
  - `1930-1932`: `LogicCheckScreenWidth = 1920`, `LogicCheckScreenHeight = 1200`
  - `225, 15461`: `GameZoomTarget` clamp 1.0–2.0
  - `60413-60434`: `DoLightTiles` / `GetAreaToLight`
  - `51797-51975`: `DrawToMap()` 把 `Main.Map` 写到 minimap 渲染目标

### OTAPI 反编译（服务端 stub，方法体被剥离）

- `/Users/arispex/RiderProjects/NextBotAdapter/NextBotAdapter.Tests/bin/Debug/net9.0/OTAPI.dll`
- 反编译输出 `/tmp/otapi_decomp/Terraria.Map.WorldMap.decompiled.cs` — 仅保留方法签名，证实 `WorldMap.UpdateLighting / IsRevealed / SetTile` 等公开 API 在服务端构建里仍然存在但**方法体为空**（OTAPI server build 会去掉客户端逻辑）。

### 当前实现位置（用于对照）

- `/Users/arispex/RiderProjects/NextBotAdapter/NextBotAdapter/Services/Exploration/PlayerExplorationTracker.cs:8-11`：当前注释"reveals roughly a 41x41 tile rectangle"，`RevealHalfExtent = 20`，与 vanilla 实际 reveal 行为不符（已被本研究证伪）。
