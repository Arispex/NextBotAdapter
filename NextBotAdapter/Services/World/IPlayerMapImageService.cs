using System.Collections;

namespace NextBotAdapter.Services;

public interface IPlayerMapImageService
{
    /// <summary>
    /// Renders a map PNG masked by the supplied per-player exploration bitmap.
    /// Tiles flagged true are rendered with their real color; tiles flagged false render as opaque black.
    /// </summary>
    (string FileName, byte[] Content) Generate(string accountName, BitArray bitmap);

    /// <summary>
    /// Renders a fully black PNG of world dimensions, used when the account exists but no exploration data has been recorded yet.
    /// </summary>
    (string FileName, byte[] Content) GenerateBlank(string accountName);
}
