# MapTile / MapHelper.CreateMapTile Semantics

- **Query**: `Terraria.Map.MapHelper.CreateMapTile` / `MapTile` / `GetMapTileXnaColor` 的精确语义
- **Scope**: external (decompiled OTAPI 3.3.11, the runtime that TSAPI 6.1.0 depends on)
- **Date**: 2026-05-05
- **Source assembly**: `/Users/arispex/.nuget/packages/otapi.upcoming/3.3.11/lib/net9.0/OTAPI.dll`
  (transitively pulled in by `TSAPI 6.1.0` → `OTAPI.Upcoming 3.3.11`; this is the assembly the project actually links against, not the `TerrariaServer.dll` shipped in the TSAPI package, which only contains `TerrariaApi.*` types)

## Conclusion (TL;DR)

Two distinct conditions both make `GetMapTileXnaColor` return RGBA(0, 0, 0, 0) — i.e. a fully transparent / black pixel — and both are valid representations of "unexplored":

1. **`default(MapTile)`** (`Type=0, Light=0, _extraData=0`). `colorLookup[0]` is `Color.Transparent`, no `MapColor` recolor is applied (because `Color == 0`), and `Light=0` keeps RGB at 0 after the `Light/255` multiplication. Output: `Color(0, 0, 0, 0)`.
2. **`CreateMapTile(x, y, 0, 0)`** when `Main.tile[x, y]` ends up resolving to `baseType == 0` (no foreground tile, no wall, no liquid, and `GetBackgroundType` returns 0). In that case the method calls `MapTile.Create(0, 0, 0)`, whose `Type` is 0 → same path as above.

For the render pipeline that is **independent of `Main.tile` content** (e.g. rendering a per-player explored mask on top of a precomputed `Main.Map._tiles`), the cleanest "未探索" representation is to write `default(MapTile)` into the cell (or skip writing and rely on the fact that the array slot is already zero-initialized). The PNG output should be RGBA(0, 0, 0, 0); if the encoder is configured to emit RGB without alpha, it will be solid black.

`Light=0` and "not explored" are **not the same thing in general**: a `CreateMapTile(x, y, 0, ...)` call on a tile that has a real type (e.g. solid stone) will return that real `Type` with the stone color, then the post-multiply by `0/255` will black out RGB but the alpha channel stays at `colorLookup[Type].A` (which for non-`Transparent` entries is 255). Result: `Color(0, 0, 0, 255)` — solid opaque black, **not** transparent. So `Light=0` produces "real type, fully dark" — same RGB as unexplored, but different alpha. If the PNG drops alpha or you only compare RGB, it is indistinguishable from unexplored; if you keep alpha, it is distinguishable.

## Method Signatures

### `MapHelper.CreateMapTile`

OTAPI 3.3.11 exposes a single overload (the 3-arg call site uses the default value for the optional 4th parameter):

```csharp
public static MapTile CreateMapTile(int i, int j, byte Light, int backgroundOverride = 0)
```

- `i, j` — tile coordinates into `Main.tile`.
- `Light` — light value (0..255) embedded directly into the resulting `MapTile.Light` (after passing through `GetTileType` / `GetBackgroundType`, which may override it: `fullbrightBlock`/`fullbrightWall` force `255`; `Main.remixWorld` sky background forces `5`; surface non-remix sky forces `255`; etc.).
- `backgroundOverride` — when the cell is "no foreground, no wall, no liquid", `GetBackgroundType(i, j, ref newLight)` is called and its return value becomes `baseType`, **unless** `backgroundOverride != 0`, in which case `backgroundOverride` is used directly. The current `MapImageService.cs` passes `0`, which means "use the natural sky/dirt/rock/hell background based on `j` / world layer". Passing a non-zero value forces the background look (sky / underground / cavern / hell) for empty cells. There is no `(int, int, byte)`-only overload; the 3-arg call is the 4-arg method with `backgroundOverride = 0`.

Behaviour summary (decompiled body, paraphrased):

```csharp
ITile tile = Main.tile[i, j];
if (tile == null) return default(MapTile);                  // off-world / unloaded → all zeros

int newColor = 0, newLight = Light, baseType = 0, baseOption = 0;
GetTileType(i, j, tile, ref newColor, ref newLight, ref baseType, ref baseOption);
if (baseType == 0)
    GetWallType(i, j, tile, ref newColor, ref newLight, ref baseType, ref baseOption);
if (baseType == 0) {
    newColor = 0;
    newLight = Light;                                       // light reset from the original arg
    baseType = (backgroundOverride == 0)
        ? GetBackgroundType(i, j, ref newLight)
        : backgroundOverride;
}
return MapTile.Create((ushort)(baseType + baseOption), (byte)newLight, (byte)newColor);
```

Important consequence: when the input `Light=0` and the cell has no foreground/wall/liquid, the `newLight` is reset to `Light` (= 0) before being handed to `GetBackgroundType`, which may again override it (sky → 255, remix → 5). So the returned `MapTile.Light` is **not necessarily 0** even when you pass `Light=0`.

### `MapHelper.GetMapTileXnaColor`

```csharp
public static Color GetMapTileXnaColor(MapTile tile)
{
    Color oldColor = colorLookup[tile.Type];
    byte color = tile.Color;
    if (color > 0)
        MapColor(tile.Type, ref oldColor, color);          // paint recolor, preserves alpha
    if (tile.Light == byte.MaxValue) return oldColor;       // shortcut: full bright

    float num = tile.Light / 255f;
    oldColor.R = (byte)(oldColor.R * num);
    oldColor.G = (byte)(oldColor.G * num);
    oldColor.B = (byte)(oldColor.B * num);
    return oldColor;                                        // alpha NOT scaled
}
```

Key facts:

- `colorLookup` is a flat XNA `Color[]` keyed by `MapTile.Type`. `colorLookup[0] = Color.Transparent` (RGBA 0,0,0,0). All other indices (tiles, walls, liquids, sky, dirt, rock, hell gradients) are opaque with `A == 255`.
- The `Light` factor scales **only RGB**. `A` is never multiplied. So:
  - `Type != 0, Light = 0` → `(0, 0, 0, 255)` (solid opaque black — "real tile, completely unlit").
  - `Type == 0, Light = anything` → `(0, 0, 0, 0)` (transparent — "unexplored / off-world").
- `tile.Color` (paint, 0..31) only triggers `MapColor` recolor when `> 0`. It never produces transparency on its own.
- `MapColor` mutates RGB but always leaves `A` unchanged (`A` is whatever `colorLookup[tile.Type].A` was, i.e. 0 for `Type=0`, 255 otherwise).

## `MapTile` Structure

`Terraria.Map.MapTile` is a `public struct` (16 bits effective payload across 3 fields, 4 bytes including struct alignment):

| Field | Type | Notes |
|---|---|---|
| `Type` | `ushort` | Index into `MapHelper.colorLookup`. `0` is the transparent / "unknown" sentinel. |
| `Light` | `byte` | 0..255. Multiplies RGB only (alpha untouched). `255` is the fast-path full-bright shortcut. |
| `_extraData` | `byte` | Bit-packed: `bit 7` = `IsChanged`, `bit 6` = `UpdateQueued`, `bits 0..4` = `Color` (paint, 0..31). Bit 5 unused. |

Properties / methods:

- `bool IsChanged` ↔ `_extraData & 0x80`
- `bool UpdateQueued` ↔ `_extraData & 0x40`
- `byte Color` ↔ `_extraData & 0x1F`
- `MapTile(ushort type, byte light, byte extraData)` — raw ctor.
- `static MapTile Create(ushort type, byte light, byte color)` — sets `_extraData = (color | 0x80)`, i.e. always marks `IsChanged = true`. This is what `CreateMapTile` returns.
- `MapTile WithLight(byte light)` — returns a new tile with the given light and `IsChanged = true`, preserving `Type` and other extra-data bits.
- `void Clear()` — zeroes `Type`, `Light`, `_extraData`. After `Clear()`, `IsChanged` is false (different from `Create(0, 0, 0)`, which sets `IsChanged = true`).
- `bool Equals(MapTile other)` — compares `Type`, `Light`, `Color` (ignores `IsChanged` / `UpdateQueued`).
- `bool EqualsWithoutLight(MapTile other)` — compares `Type` and `Color`.

`default(MapTile)` is therefore `Type=0, Light=0, _extraData=0` (`IsChanged=false`, `UpdateQueued=false`, `Color=0`), and rendering it via `GetMapTileXnaColor` yields `Color.Transparent`.

## How Vanilla Represents "Unexplored"

The actual "is this cell explored?" state is stored inline in `WorldMap._tiles`: an unexplored cell is just a `default(MapTile)` slot. The OTAPI server stub of `WorldMap` exposes:

```csharp
public class WorldMap
{
    public const int BlackEdgeWidth = 40;
    public MapTile[,] _tiles;
    public MapTile this[int x, int y] => default(MapTile);   // server stub returns default
    public bool IsRevealed(int x, int y);
    public void SetTile(int x, int y, ref MapTile tile);
    public bool UpdateType(int x, int y);
    public bool UpdateType(int x, int y, ref MapTile mapTile);
    public bool UpdateLighting(int x, int y, byte light);
    public void UnlockMapSection(int sectionX, int sectionY);
    public void UnlockMapTilePretty(int x, int y);
    public void Clear();
    // ...
}
```

There is no separate "explored" bit on `MapTile`. The convention is:

- A cell that has never been revealed: `_tiles[x, y]` stays at its zero-initialized `default(MapTile)` value → `Type=0` → renders as `Transparent`.
- A cell that is revealed: a real `MapTile` produced by `CreateMapTile(...)` with `Type != 0` is written via `SetTile` / `UpdateType`. `IsChanged` ends up `true` (because `CreateMapTile` goes through `MapTile.Create`, which sets the high bit).
- "Revealed but currently dark" (e.g. deep cavern with low light) is represented by `Type != 0, Light = small` — `GetMapTileXnaColor` returns `(small*R/255, small*G/255, small*B/255, 255)`, which is dim color with full alpha, distinguishable from unexplored only via the alpha channel.

So `IsChanged == true` is essentially the "this cell has been revealed at least once" flag, and `Type != 0` is the visual proxy for the same.

## How to Render "Unexplored" in This Project

Given the renderer is

```csharp
var tile = MapHelper.CreateMapTile(x, y, byte.MaxValue, 0);
Main.Map._tiles[x + Edge, y + Edge] = tile;
var color = MapHelper.GetMapTileXnaColor(tile);
image[x, y] = new Rgba32(color.R, color.G, color.B, color.A);
```

Recommended representation for "未探索" cells:

- **Preferred — render path bypass**: when the per-player explored mask says cell `(x, y)` is not revealed, do not call `CreateMapTile` / `GetMapTileXnaColor` at all. Either:
  - leave the corresponding `_tiles` slot as `default(MapTile)` and feed `default(MapTile)` to `GetMapTileXnaColor` (returns `Color.Transparent` → `Rgba32(0, 0, 0, 0)`); or
  - directly write `image[x, y] = new Rgba32(0, 0, 0, 255)` (solid black, opaque) without going through `GetMapTileXnaColor`. This is the cleanest if the PNG must not have alpha holes.

- **Avoid — `Light = 0`**: passing `Light = 0` to `CreateMapTile` does **not** universally yield a "black" tile.
  - For solid foreground/wall/liquid cells it does produce RGB(0,0,0) but `A = 255` (opaque black), which is fine if the PNG is RGB.
  - For empty cells (no fg, no wall, no liquid) `CreateMapTile` calls `GetBackgroundType` which can override `Light` (sky → 255, remix sky → 5), so the resulting pixel will be a sky / dirt / rock / hell tone, not black. So `Light = 0` is **not** a universal "unexplored" knob.

- **Distinguishing "explored but dark" from "unexplored"**: if the PNG keeps alpha:
  - explored-and-dark → `(0, 0, 0, 255)`
  - unexplored → `(0, 0, 0, 0)`
  If the PNG is RGB-only, the two are visually identical (both render as black in viewers that ignore alpha), which is acceptable for a "fog of war" look.

## References

- Decompiled `Terraria.Map.MapHelper.CreateMapTile` and `GetMapTileXnaColor` from `OTAPI.dll` 3.3.11 (full body shown above).
- Decompiled `Terraria.Map.MapTile` struct from the same assembly (full body shown above).
- Decompiled `Terraria.Map.WorldMap` (server stub) — confirms `_tiles : MapTile[,]`, `BlackEdgeWidth = 40`, `IsRevealed(x, y)` exists.
- `colorLookup` initializer (`MapHelper.Initialize`): `colorLookup[0] = Color.Transparent;` is the very first assignment, before `tilePosition`, `wallPosition`, `liquidPosition`, `skyPosition`, `dirtPosition`, `rockPosition`, `hellPosition` ranges are filled.
- Existing call sites in the project that this analysis applies to:
  - `NextBotAdapter/Services/World/MapImageService.cs:51,64-65`
  - `NextBotAdapter/Services/World/MapFileService.cs:56`
