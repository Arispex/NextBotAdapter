using System.Diagnostics.CodeAnalysis;
using Terraria;

namespace NextBotAdapter.Services;

[ExcludeFromCodeCoverage]
public class WorldFileService : IWorldFileService
{
    public (string FileName, byte[] Content) GetWorldFile()
    {
        var path = Main.worldPathName;
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var bytes = new byte[stream.Length];
        stream.ReadExactly(bytes);
        return (Path.GetFileName(path), bytes);
    }
}
