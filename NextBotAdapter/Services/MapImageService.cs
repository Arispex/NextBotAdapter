using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Terraria;
using Terraria.IO;
using Terraria.Map;

namespace NextBotAdapter.Services;

public sealed class MapImageService : IMapImageService
{
    private const int Edge = WorldMap.BlackEdgeWidth;
    private readonly string _cacheDirectoryPath;

    public MapImageService(string cacheDirectoryPath)
    {
        _cacheDirectoryPath = cacheDirectoryPath;
    }

    public string CacheDirectoryPath => _cacheDirectoryPath;

    public (string FileName, string FilePath, byte[] Content) GenerateAndCache()
    {
        PrepareMapEnvironment();

        using var image = CreateMapImage();
        var fileName = $"map-{DateTimeOffset.Now:yyyy-MM-dd_HH-mm-ss}.png";
        var filePath = Path.Combine(_cacheDirectoryPath, fileName);

        image.SaveAsPng(filePath);
        var content = File.ReadAllBytes(filePath);
        return (fileName, filePath, content);
    }

    private static void PrepareMapEnvironment()
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

    private static WorldMap CreateWorkingMap()
        => new(Main.maxTilesX, Main.maxTilesY)
        {
            _tiles = new MapTile[Main.maxTilesX + (Edge * 2), Main.maxTilesY + (Edge * 2)]
        };

    private static void LightUpWholeMap()
    {
        Main.Map = CreateWorkingMap();
        var width = Main.Map._tiles.GetLength(0);
        var height = Main.Map._tiles.GetLength(1);

        for (var x = 0; x < Main.maxTilesX; x++)
        {
            for (var y = 0; y < Main.maxTilesY; y++)
            {
                var tile = MapHelper.CreateMapTile(x, y, byte.MaxValue, 0);

                if ((uint)x < (uint)width && (uint)y < (uint)height)
                {
                    Main.Map._tiles[x, y] = tile;
                }

                var rawX = x + Edge;
                var rawY = y + Edge;
                if ((uint)rawX < (uint)width && (uint)rawY < (uint)height)
                {
                    Main.Map._tiles[rawX, rawY] = tile;
                }
            }
        }
    }

    private static Image<Rgba32> CreateMapImage()
    {
        var image = new Image<Rgba32>(Main.maxTilesX, Main.maxTilesY);
        LightUpWholeMap();

        for (var x = 0; x < Main.maxTilesX; x++)
        {
            for (var y = 0; y < Main.maxTilesY; y++)
            {
                var tile = Main.Map._tiles[x + Edge, y + Edge];
                var color = MapHelper.GetMapTileXnaColor(tile);
                image[x, y] = new Rgba32(color.R, color.G, color.B, color.A);
            }
        }

        return image;
    }
}
