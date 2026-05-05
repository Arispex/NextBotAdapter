using System.Collections;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using Terraria;
using Terraria.IO;
using Terraria.Map;

namespace NextBotAdapter.Services;

public sealed class PlayerMapImageService : IPlayerMapImageService
{
    private const int Edge = WorldMap.BlackEdgeWidth;

    public (string FileName, byte[] Content) Generate(string accountName, BitArray bitmap)
    {
        lock (MapRenderMutex.Lock)
        {
            PrepareMapEnvironment();

            var width = Main.maxTilesX;
            var height = Main.maxTilesY;

            FillMaskedTiles(bitmap, width, height);

            using var image = new Image<Rgba32>(width, height);
            for (var x = 0; x < width; x++)
            {
                for (var y = 0; y < height; y++)
                {
                    var index = (y * width) + x;
                    var explored = index < bitmap.Length && bitmap.Get(index);
                    if (!explored)
                    {
                        image[x, y] = new Rgba32(0, 0, 0, 255);
                        continue;
                    }

                    var tile = Main.Map._tiles[x + Edge, y + Edge];
                    var color = MapHelper.GetMapTileXnaColor(tile);
                    if (color.A == 0)
                    {
                        image[x, y] = new Rgba32(0, 0, 0, 255);
                    }
                    else
                    {
                        image[x, y] = new Rgba32(color.R, color.G, color.B, color.A);
                    }
                }
            }

            return EncodePng(accountName, image);
        }
    }

    public (string FileName, byte[] Content) GenerateBlank(string accountName)
    {
        lock (MapRenderMutex.Lock)
        {
            PrepareMapEnvironment();

            var width = Main.maxTilesX;
            var height = Main.maxTilesY;
            using var image = new Image<Rgba32>(width, height);
            var black = new Rgba32(0, 0, 0, 255);
            for (var x = 0; x < width; x++)
            {
                for (var y = 0; y < height; y++)
                {
                    image[x, y] = black;
                }
            }

            return EncodePng(accountName, image);
        }
    }

    private static (string FileName, byte[] Content) EncodePng(string accountName, Image<Rgba32> image)
    {
        using var stream = new MemoryStream();
        image.Save(stream, PngFormat.Instance);
        var safeName = string.IsNullOrWhiteSpace(accountName) ? "player" : accountName;
        var fileName = $"map-{safeName}-{DateTimeOffset.Now:yyyy-MM-dd_HH-mm-ss}.png";
        return (fileName, stream.ToArray());
    }

    private static void FillMaskedTiles(BitArray bitmap, int worldWidth, int worldHeight)
    {
        Main.Map = CreateWorkingMap();
        MapTileGrid.Fill(
            worldWidth,
            worldHeight,
            Edge,
            (x, y) =>
            {
                var index = (y * worldWidth) + x;
                if (index >= bitmap.Length || !bitmap.Get(index))
                {
                    return default;
                }
                return MapHelper.CreateMapTile(x, y, byte.MaxValue, 0);
            },
            Main.Map._tiles);
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
}
