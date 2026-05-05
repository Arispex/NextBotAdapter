namespace NextBotAdapter.Services;

/// <summary>
/// Single process-wide mutex serializing every code path that reads or writes
/// the Terraria static map state (Main.Map, Main.MapFileMetadata, etc.).
///
/// Multiple REST endpoints (/world/map-image, /users/{user}/map-image,
/// /world/map-file) share these globals with no other synchronization. Without
/// this lock, two concurrent requests interleave their CreateWorkingMap +
/// fill + render steps and both return the LATER request's view.
///
/// Always wrap the entire rendering lifecycle (Prepare + fill + read + encode/
/// SaveMap) in a single lock(MapRenderMutex.Lock) — partial coverage still
/// races during PNG encoding or SaveMap.
/// </summary>
internal static class MapRenderMutex
{
    public static readonly object Lock = new();
}
