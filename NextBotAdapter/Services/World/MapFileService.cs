using System.Diagnostics.CodeAnalysis;
using Terraria;
using Terraria.IO;
using Terraria.Map;

namespace NextBotAdapter.Services;

[ExcludeFromCodeCoverage]
public class MapFileService : IMapFileService
{
    private const int Edge = WorldMap.BlackEdgeWidth;

    public MapFileService()
    {
        MapHelper.Initialize();
        Main.mapEnabled = true;
        Main.Map = CreateWorkingMap();
        Main.ActivePlayerFileData = new PlayerFileData
        {
            Name = "NextBotAdapter",
            _path = Main.GetPlayerPathFromName("NextBotAdapter", false)
        };
        Main.MapFileMetadata = FileMetadata.FromCurrentSettings(FileType.Map);
    }

    public (string FileName, byte[] Content) GetMapFile()
    {
        LightUpWholeMap();
        MapHelper.SaveMap();

        var playerPath = Main.playerPathName[..^4] + Path.DirectorySeparatorChar;
        var mapFileName = !Main.ActiveWorldFileData.UseGuidAsMapName
            ? Main.worldID + ".map"
            : Main.ActiveWorldFileData.UniqueId + ".map";
        var mapFilePath = Path.Combine(playerPath, mapFileName);

        if (!File.Exists(mapFilePath))
            throw new FileNotFoundException("Map file not found.", mapFilePath);

        return (Path.GetFileName(mapFilePath), File.ReadAllBytes(mapFilePath));
    }

    private static WorldMap CreateWorkingMap() =>
        new(Main.maxTilesX, Main.maxTilesY)
        {
            _tiles = new MapTile[Main.maxTilesX + Edge * 2, Main.maxTilesY + Edge * 2]
        };

    private static void LightUpWholeMap()
    {
        Main.Map = CreateWorkingMap();
        MapTileGrid.Fill(
            Main.maxTilesX,
            Main.maxTilesY,
            Edge,
            (x, y) => MapHelper.CreateMapTile(x, y, byte.MaxValue),
            Main.Map._tiles);
    }
}
