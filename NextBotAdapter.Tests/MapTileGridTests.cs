using NextBotAdapter.Services;

namespace NextBotAdapter.Tests;

public class MapTileGridTests
{
    [Fact]
    public void Fill_WritesEachWorldCoordinateToOffsetTarget()
    {
        const int worldWidth = 12;
        const int worldHeight = 8;
        const int edge = 3;
        var target = new (int X, int Y)?[worldWidth + edge * 2, worldHeight + edge * 2];

        MapTileGrid.Fill(worldWidth, worldHeight, edge, (x, y) => (x, y), target);

        for (var rx = 0; rx < worldWidth; rx++)
        for (var ry = 0; ry < worldHeight; ry++)
        {
            var actual = target[rx + edge, ry + edge];
            Assert.Equal((rx, ry), actual);
        }
    }

    [Fact]
    public void Fill_DoesNotWriteOutsideEdgeBands()
    {
        const int worldWidth = 12;
        const int worldHeight = 8;
        const int edge = 3;
        var width = worldWidth + edge * 2;
        var height = worldHeight + edge * 2;
        var target = new (int X, int Y)?[width, height];

        MapTileGrid.Fill(worldWidth, worldHeight, edge, (x, y) => (x, y), target);

        // Left band: [0, edge) x [0, height)
        for (var x = 0; x < edge; x++)
        for (var y = 0; y < height; y++)
            Assert.Null(target[x, y]);

        // Right band: [width - edge, width) x [0, height)
        for (var x = width - edge; x < width; x++)
        for (var y = 0; y < height; y++)
            Assert.Null(target[x, y]);

        // Top band: [0, width) x [0, edge)
        for (var x = 0; x < width; x++)
        for (var y = 0; y < edge; y++)
            Assert.Null(target[x, y]);

        // Bottom band: [0, width) x [height - edge, height)
        for (var x = 0; x < width; x++)
        for (var y = height - edge; y < height; y++)
            Assert.Null(target[x, y]);
    }

    [Fact]
    public void Fill_InvokesFactoryOncePerWorldCoordinate()
    {
        const int worldWidth = 12;
        const int worldHeight = 8;
        const int edge = 3;
        var target = new (int X, int Y)?[worldWidth + edge * 2, worldHeight + edge * 2];
        var seen = new HashSet<(int, int)>();
        var callCount = 0;

        MapTileGrid.Fill(worldWidth, worldHeight, edge, (x, y) =>
        {
            callCount++;
            Assert.True(seen.Add((x, y)), $"factory called more than once for ({x}, {y})");
            return (x, y);
        }, target);

        Assert.Equal(worldWidth * worldHeight, callCount);
        Assert.Equal(worldWidth * worldHeight, seen.Count);
        for (var x = 0; x < worldWidth; x++)
        for (var y = 0; y < worldHeight; y++)
            Assert.Contains((x, y), seen);
    }

    [Fact]
    public void Fill_SilentlySkipsCoordinatesOutsideTargetBounds()
    {
        const int worldWidth = 12;
        const int worldHeight = 8;
        const int edge = 3;
        var target = new (int X, int Y)?[5, 5];

        var exception = Record.Exception(() =>
            MapTileGrid.Fill(worldWidth, worldHeight, edge, (x, y) => (x, y), target));

        Assert.Null(exception);

        // Only coordinates whose offset position falls within the 5x5 target should be written.
        // rawX = x + 3 in [3, 4] -> x in [0, 1]; rawY = y + 3 in [3, 4] -> y in [0, 1].
        Assert.Equal((0, 0), target[3, 3]);
        Assert.Equal((1, 0), target[4, 3]);
        Assert.Equal((0, 1), target[3, 4]);
        Assert.Equal((1, 1), target[4, 4]);

        // Pre-offset region remains untouched.
        for (var x = 0; x < edge; x++)
        for (var y = 0; y < target.GetLength(1); y++)
            Assert.Null(target[x, y]);
        for (var x = 0; x < target.GetLength(0); x++)
        for (var y = 0; y < edge; y++)
            Assert.Null(target[x, y]);
    }
}
