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
        var width = Main.Map._tiles.GetLength(0);
        var height = Main.Map._tiles.GetLength(1);
        for (var x = 0; x < Main.maxTilesX; x++)
        for (var y = 0; y < Main.maxTilesY; y++)
        {
            var tile = MapHelper.CreateMapTile(x, y, byte.MaxValue);
            if ((uint)x < (uint)width && (uint)y < (uint)height)
                Main.Map._tiles[x, y] = tile;
            var rawX = x + Edge;
            var rawY = y + Edge;
            if ((uint)rawX < (uint)width && (uint)rawY < (uint)height)
                Main.Map._tiles[rawX, rawY] = tile;
        }
    }
}
