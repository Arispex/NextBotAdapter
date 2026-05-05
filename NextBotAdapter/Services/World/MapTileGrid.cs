namespace NextBotAdapter.Services;

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
