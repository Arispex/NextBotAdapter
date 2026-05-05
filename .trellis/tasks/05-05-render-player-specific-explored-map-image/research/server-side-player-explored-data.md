# Server-Side Player Explored Data — Feasibility

- **Query**: TShock 6.1.0 / OTAPI server can it obtain a specific player's explored map bitmap?
- **Scope**: external (TShock / OTAPI / Terraria source) + internal (NextBotAdapter MapImageService)
- **Date**: 2026-05-05

## Conclusion (TL;DR)

**No, not directly.** Vanilla Terraria's per-player explored bitmap is a pure client-side concept (stored locally as `<player>.map`); the network protocol never carries it, the server never receives it, TShock 6.1.0 has no field/hook/handler for it, and OTAPI 3.3.11 exposes no event tied to it. The only practical workaround is to have the plugin **synthesize** a server-side per-player visibility bitmap by tracking each player's position over time (from `PacketTypes.PlayerUpdate` / 13) and accumulating a "seen" mask using Terraria's known visibility radius around each tick — i.e. emulate exploration server-side.

## Evidence

### TShock / TSAPI

- `TShockAPI.TSPlayer.Client.TileSections` is the only per-player tile-related state TShock surfaces. It is `bool[Main.maxSectionsX, Main.maxSectionsY]` — coarse 200×150-tile chunks, and only tracks **whether the server has sent that chunk to that client**, NOT what the client has actually walked over. Documented in `TShockAPI.xml`: *"Changes the values of the `Terraria.RemoteClient.TileSections` array."* Source: `TShockAPI/TSPlayer.cs:1819` (`UpdateSection`) — file pulled from `Pryaxis/TShock@general-devel`.
- `TShockAPI/GetDataHandlers.cs` registers handlers for **every** packet TShock cares about (lines 76-147 of `general-devel`). There is **no handler for any "MapData" / "MapHelp" / "MapSync" packet**. The only section-related handler is `HandleGetSection` (line 2919), which reads only `(x, y, team)` — the section coordinates the client wants — never any bitmap. There is no `PacketTypes.MapData` member used in TShock 6.x flow.
- `TShockAPI/TSPlayer.cs` does not contain `Explored`, `MapHelper`, or `MapTile` references in any per-player capacity.
- `TPlayer.position` (`Vector2`, world coords) is the closest signal — it tells you where the player currently is, not where they have been. Read at `HandlePlayerUpdate` (`GetDataHandlers.cs:3108`).

### OTAPI

- OTAPI 3.3.11 (`/Users/arispex/.nuget/packages/otapi.upcoming/3.3.11/lib/net9.0/OTAPI.dll`) hook event surface searched via `strings`. Map-related hooks present: `SendSection`, `SyncOnePlayer`, `GetSectionBounds`, `GetSectionX`, `GetSectionY`, `ResetSections`, `DrawPlayerMapIcon_CanBeSeen`, `CreateMapTile`, `GetMapTileXnaColor`, `UnlockMapTilePretty`, `UnlockMapSection`, `LoadMapVersion1/2/Compressed`, `InternalSaveMap`, `OldMapHelper`, `clearMap`, `unlockMap`, `updateMap`.
- **None** of these expose a per-player explored bitmap. They are: server→client packet dispatch hooks (`SendSection`, `SyncOnePlayer`), tile→color conversion (`CreateMapTile`/`GetMapTileXnaColor`, used purely for in-process rendering), local *.map file format read/write (`LoadMapVersion*`, `InternalSaveMap` — these target the local player's `.map` file, not a remote player's), and admin power tools (`unlockMap`, `clearMap`).
- `MapTile` struct itself (`Terraria.Map.MapTile`, fetched from a public mirror `Gothbr4dn/terraria-source-code:1.4.4.0/Terraria.Map/MapTile.cs`) has only three semantic fields: `Type` (ushort), `Light` (byte), `_extraData` (byte; encodes `IsChanged` + `Color`). The `Light` byte doubles as visibility — `WorldMap.IsRevealed(x,y)` returns `_tiles[x,y].Light > 0`. There is no per-player MapTile array; it is a singleton on `Main.Map`.

### Terraria Network Protocol

Reviewed `Pryaxis/TShock` wiki `Multiplayer-Packet-Structure-(v1.4.5.6).md` (full table of contents 1..158). Map / section relevant packets:

| ID  | Name                          | Direction | Carries explored bitmap?                                                                                                  |
|-----|-------------------------------|-----------|----------------------------------------------------------------------------------------------------------------------------|
| 8   | Request Essential Tiles       | C → S     | No. Payload = `(SpawnX:i32, SpawnY:i32, TeamSpawn:u8)` only.                                                                 |
| 10  | Send Section                  | S → C     | No. Server pushes raw tile data; not echoed back.                                                                            |
| 11  | Section Tile Frame            | S → C     | No.                                                                                                                          |
| 13  | Player Update (move tick)     | C → S     | No. Carries position+velocity+controls. Server learns *where the player IS*, not what they have *seen*.                      |
| 159 | Request Section               | C → S     | No. Payload = `(SectionX:u16, SectionY:u16)`.                                                                                |

Result: **the protocol has zero packets in either direction that contain explored / fog-of-war / per-tile visibility data.** Exploration is a strictly client-local concept — the client persists it in `<player>.map` and never tells the server about it.

### Third-party plugins

- `Controllerdestiny/TShockAdapter` (`MorMorAdapter/Utils.cs:709-729`) renders a server-side map image by **forcing `light=byte.MaxValue` for every tile**, the *exact* same workaround NextBotAdapter already uses. The author of this Chinese-community Adapter (which wraps very similar functionality) did **not** find any way to access per-player explored data either.
- `UnrealMultiple/TShockPlugin` (RecipesBrowser, SpawnInfra), `UnrealMultiple/VortexQ`, `hufang360/TShockWorldModify`, `1242509682/SpawnInfra`, `Lagrange.XocMat.Adapter` — all use `MapHelper.CreateMapTile(x, y, byte.MaxValue)` + `MapHelper.GetMapTileXnaColor(...)` for tile-color lookups. None of them touches per-player explored data.
- `AnzhelikaO/FakeProvider` is the only TShock plugin that comes close to per-player tile manipulation; it provides per-player fake tile views (sending different tiles to different clients via `SendSection`/`Tile` packets) but again **does not** read back any explored state from clients — it's a one-way push.
- Searched: `tshock map sync`, `tshock player explored`, `tshock fov`, `tshock explored bitmap`. No hits implementing this feature.

### Why the server can't have it (architectural reason)

`Terraria.Map.MapUpdateQueue.Add` (1.4.5 source via `JonataOliveiraa/Terraria1.4.5:Map/MapUpdateQueue.cs`):

```csharp
public static void Add(int x, int y)
{
    if (Main.dedServ || WorldGen.generatingWorld || !Main.mapEnabled || !Main.Map.QueueUpdate(x, y))
        return;
    ...
}
```

`Main.Map` (the `WorldMap`) exists as a static field on a dedicated server, but every code path that would populate its `MapTile.Light` is short-circuited by `Main.dedServ` (or `!Main.mapEnabled`). Lighting and map-update are pure client systems. `MapImageService` confirms this: it manually `new WorldMap(...)` + `LightUpWholeMap()` + `byte.MaxValue` precisely because the server's `Main.Map` is empty by design.

The `Lighting` static is similarly client-only. Thousands of mod sources show the standard guard `if (Main.dedServ) return;` right before any `Main.Map.UpdateLighting`/`Lighting.AddLight` call.

## Implementation Implications

### What is NOT achievable

- Reading the actual fog-of-war as the player saw it (e.g. peeked through a sliver via vine teleport): not possible. The server has no record.
- Replicating the exact `Light` value (which the client uses for the gradient between bright and just-discovered): not possible without re-running the client lighting engine on the server.

### Workaround: synthesize server-side visibility

The plugin can build its own per-player "explored" bitmap by sampling on each `PacketTypes.PlayerUpdate` (~60Hz):

1. Maintain `Dictionary<TSPlayer, BitArray (Main.maxTilesX × Main.maxTilesY)>` (or paged variants — full HD world is 8400×2400 ≈ 2.5MB per player; large world is ~6700×1800 / Crimson). Memory is acceptable per-player.
2. On `GetDataHandlers.PlayerUpdate` event (or OTAPI `PacketTypes.PlayerUpdate` hook), convert `TPlayer.position` → tile coords `(tx, ty) = (position.X / 16, position.Y / 16)`.
3. Mark a Manhattan / Euclidean disk around `(tx, ty)` as explored. Vanilla Terraria reveals roughly a 41×41-tile rectangle around the player on screen; using a half-extent of about 50 tiles is a safe approximation. Tune to match observable client behavior.
4. To render player-specific map: re-use the existing `MapImageService.CreateMapImage` flow but replace `light = byte.MaxValue` with `light = bitmap.Get(x, y) ? byte.MaxValue : (byte)0`. Then `MapHelper.GetMapTileXnaColor` will return `(0,0,0,0)` for unexplored tiles (need to check; if it doesn't, render those tiles as black manually).
5. Persist the bitmap to disk per (UUID, world) so it survives restarts. Mirror semantics of vanilla `<player>.map`.

### Caveats of the workaround

- Diverges from real client `<player>.map`: anything the player sees through teleports, dressers, mirrors, magic conch, etc. that moves them outside the synthesized "walked through here" disk will not be marked as explored. Conversely, the simulated reveal radius is independent of real client view distance / zoom and may over-reveal compared to the client.
- Requires running for the entire session; a player's history before the plugin is enabled is not recoverable.
- Server cannot distinguish between "this player explored these tiles in this character's lifetime" vs "since plugin started" without persistent storage.

### Alternative: ask the client

Theoretically a paired client-side mod could ship `<player>.map` to the server when the player joins. tModLoader plugins can do that. Out of scope for a pure TShock server-side plugin running against vanilla 1.4.4.9 / 1.4.5 clients.

## References

- TShock source (used to verify packet handler list and `TileSections` semantics): `https://github.com/Pryaxis/TShock/blob/general-devel/TShockAPI/GetDataHandlers.cs`, `https://github.com/Pryaxis/TShock/blob/general-devel/TShockAPI/TSPlayer.cs`
- TShock multiplayer protocol wiki (1.4.5.6): `https://raw.githubusercontent.com/wiki/Pryaxis/TShock/Multiplayer-Packet-Structure-(v1.4.5.6).md`
- `MapTile` struct & `WorldMap.IsRevealed`: `https://raw.githubusercontent.com/Gothbr4dn/terraria-source-code/main/1.4.4.0/Terraria.Map/MapTile.cs`, `https://raw.githubusercontent.com/Gothbr4dn/terraria-source-code/main/1.4.4.0/Terraria.Map/WorldMap.cs`
- Server-side short-circuit: `https://github.com/JonataOliveiraa/Terraria1.4.5/blob/main/Map/MapUpdateQueue.cs` (`if (Main.dedServ ...) return;` in both `Add(Rectangle)` and `Add(int,int)`).
- Existing analogous TShock plugin using same forced-light workaround: `https://github.com/Controllerdestiny/TShockAdapter/blob/master/MorMorAdapter/Utils.cs` (`CreateMapImage`).
- OTAPI 3.3.11 binary at `/Users/arispex/.nuget/packages/otapi.upcoming/3.3.11/lib/net9.0/OTAPI.dll` (hook surface inspected via `strings`).
- TShock 6.1.0 XML doc at `/Users/arispex/.nuget/packages/tshock/6.1.0/lib/net9.0/TShockAPI.xml`.
- NextBotAdapter current implementation: `/Users/arispex/RiderProjects/NextBotAdapter/NextBotAdapter/Services/World/MapImageService.cs` (lines 38-71 — confirms the forced-light pattern).
