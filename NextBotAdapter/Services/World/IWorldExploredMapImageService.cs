namespace NextBotAdapter.Services;

public interface IWorldExploredMapImageService
{
    /// <summary>
    /// Renders a map PNG that is the bitwise OR union of every TShock account's exploration bitmap.
    /// Tiles explored by any player are rendered with their real color; unexplored tiles render as opaque black.
    /// </summary>
    (string FileName, byte[] Content) Generate();
}
