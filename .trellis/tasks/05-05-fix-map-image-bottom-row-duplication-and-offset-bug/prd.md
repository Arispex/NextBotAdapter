# Fix map image bottom row duplication and offset bug

## Goal

修复 REST `/world/map/image` 与 `/world/map/file` 两个端点渲染结果中的坐标错位 bug：图片底部出现重复区块且向右偏移。根因是 `LightUpWholeMap()` 中存在一次多余的写入，在按 x 主序遍历时覆盖了正确写入位置的数据，造成主体被向上向左平移 `Edge` 像素，仅底部 / 右侧 `Edge` 像素的 L 形条带保留正确坐标。

## What I already know

- `MapImageService.LightUpWholeMap()`（[NextBotAdapter/Services/World/MapImageService.cs:44-69](NextBotAdapter/Services/World/MapImageService.cs:44)）和 `MapFileService.LightUpWholeMap()`（[NextBotAdapter/Services/World/MapFileService.cs:49-65](NextBotAdapter/Services/World/MapFileService.cs:49)）含同一处 bug
- bug 的两处写入：
  - 第一次 `_tiles[x, y] = tile`（多余 + 有害）
  - 第二次 `_tiles[x+Edge, y+Edge] = tile`（正确）
- 由于 `for (x) for (y)` 主序，迭代到 `(x=rx+Edge, y=ry+Edge)` 时晚于 `(rx, ry)`，第一次写覆盖了第二次写在 `_tiles[rx+Edge, ry+Edge]` 处的正确值
- 仅当 `rx+Edge >= maxTilesX` 或 `ry+Edge >= maxTilesY` 时第一次写不会触发，所以右 / 底 Edge 条带保留正确坐标
- `Edge = WorldMap.BlackEdgeWidth`（Terraria 常量，约 40 像素）
- 模拟（maxTilesX=12, maxTilesY=8, Edge=3）验证了上述行为
- 现有测试 `MapEndpointsTests` / `WorldMapFileEndpointsTests` 用 Fake 实现仅覆盖 endpoint 输出层，没覆盖 `LightUpWholeMap` 的坐标对齐逻辑——这就是 bug 没被测出来的原因
- Backend quality 规范要求：行为变更要有自动化测试

## Requirements

- 修复 `MapImageService.LightUpWholeMap()` 中的坐标错位
- 修复 `MapFileService.LightUpWholeMap()` 中的同款坐标错位
- 保持现有 endpoint 公共契约不变（路由、响应字段、错误码不动）

## Acceptance Criteria

- [ ] `LightUpWholeMap()` 仅向 `_tiles[x+Edge, y+Edge]` 写入瓦片
- [ ] 通过 `/world/map/image` 拉取的 PNG 顶端到底端连续，无重复行 / 偏移
- [ ] 通过 `/world/map/file` 拉取的 `.map` 文件在 Terraria 客户端打开后无错位
- [ ] 现有 endpoint 行为契约测试仍然通过
- [ ] 涉及坐标对齐的回归测试（如有添加）通过

## Definition of Done

- 两处 `LightUpWholeMap` 已修复
- 测试套件通过（`dotnet test`）
- 手动验证：实际游戏环境拉取图片 / map 文件后视觉正确

## Out of Scope

- 不改 endpoint 路由 / 响应结构 / 错误码
- 不动 `PrepareMapEnvironment()` / `CreateWorkingMap()` 等周边逻辑
- 不动 `MapHelper.CreateMapTile` / `GetMapTileXnaColor` 调用方式
- 不动 REST `/world/map/*` 的鉴权 / 权限配置

## Technical Approach

1. 新增纯函数 `Services/World/MapTileGrid.cs`：

   ```csharp
   internal static class MapTileGrid
   {
       public static void Fill<T>(int worldWidth, int worldHeight, int edge,
                                  Func<int, int, T> factory, T[,] target)
       {
           var width = target.GetLength(0);
           var height = target.GetLength(1);
           for (var x = 0; x < worldWidth; x++)
           for (var y = 0; y < worldHeight; y++)
           {
               var rawX = x + edge;
               var rawY = y + edge;
               if ((uint)rawX < (uint)width && (uint)rawY < (uint)height)
                   target[rawX, rawY] = factory(x, y);
           }
       }
   }
   ```

   - 只写一处 `[x+edge, y+edge]`，根除原来"两次写"导致的覆盖
   - 不持有 Terraria 静态状态，可纯单测

2. `MapImageService.LightUpWholeMap()` 与 `MapFileService.LightUpWholeMap()` 改为调用 `MapTileGrid.Fill`：

   ```csharp
   private static void LightUpWholeMap()
   {
       Main.Map = CreateWorkingMap();
       MapTileGrid.Fill(
           Main.maxTilesX,
           Main.maxTilesY,
           Edge,
           (x, y) => MapHelper.CreateMapTile(x, y, byte.MaxValue, /* maybe arg */),
           Main.Map._tiles);
   }
   ```

   - 注意 `MapImageService` 调 4 参数 `CreateMapTile(x, y, byte.MaxValue, 0)`，`MapFileService` 调 3 参数 `CreateMapTile(x, y, byte.MaxValue)`——保持各自原样，只把循环骨架抽走
3. 新增 `NextBotAdapter.Tests/MapTileGridTests.cs`，覆盖：
   - 所有 `(rx, ry)` 在 `[0, worldWidth) × [0, worldHeight)` 的位置 `target[rx+edge, ry+edge]` 等于 `factory(rx, ry)` 的结果
   - 边界外的格子（`[0, edge)` 区域和 `[worldWidth+edge, width)` 区域）保持初始值未被写入
   - factory 调用次数 = `worldWidth * worldHeight`，每个 `(x, y)` 各一次
   - 用小尺寸（如 `worldWidth=12, worldHeight=8, edge=3`）让测试快速运行

## Decision (ADR-lite)

- **Context**：`LightUpWholeMap` 含坐标错位 bug，且在两个 service 中重复出现；`Main.Map._tiles` 等 Terraria 静态状态难以单测；后端规范要求行为变更要有自动化测试
- **Decision**：抽出纯函数 `MapTileGrid.Fill<T>` 承载坐标映射规则，两处 service 调用同一处实现；为纯函数补 xUnit 回归测试
- **Consequences**：
  - 优点：双处只改一处；坐标映射规则被测试守住，相同类型 bug 无法静默回归
  - 代价：增加一个内部工具类型 + 一个测试文件；service 多一层间接调用

## Open Questions

无

## Technical Notes

- 两处文件：
  - [NextBotAdapter/Services/World/MapImageService.cs](NextBotAdapter/Services/World/MapImageService.cs)
  - [NextBotAdapter/Services/World/MapFileService.cs](NextBotAdapter/Services/World/MapFileService.cs)
- `LightUpWholeMap` 操作 Terraria 静态状态（`Main.Map._tiles`、`MapHelper.CreateMapTile`），原状态下难以单测
- 若做坐标映射回归测试，可抽出一个纯函数版本，参数化 `(maxTilesX, maxTilesY, Edge, target[,])`，把 `tile` 用 `(x, y)` 占位，然后断言 `target[rx+Edge, ry+Edge] == (rx, ry)`
- Backend Quality 规范来源：[.trellis/spec/backend/quality-guidelines.md](.trellis/spec/backend/quality-guidelines.md)
