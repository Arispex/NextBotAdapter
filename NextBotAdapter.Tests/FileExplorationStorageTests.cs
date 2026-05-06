using System.Collections;
using System.IO;
using NextBotAdapter.Services;

namespace NextBotAdapter.Tests;

public sealed class FileExplorationStorageTests : IDisposable
{
    private readonly string _tempDir;
    private const int WorldId = 12345;
    private const int Width = 100;
    private const int Height = 60;

    public FileExplorationStorageTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "NextBotAdapterTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Fact]
    public void SaveAndLoad_ShouldRoundTripBitmapBytes()
    {
        var storage = CreateStorage();
        var bitmap = new BitArray(Width * Height);
        bitmap.Set(0, true);
        bitmap.Set(Width * Height - 1, true);
        bitmap.Set(42, true);

        storage.Save("uuid-1", bitmap);
        var loaded = storage.Load("uuid-1", Width * Height);

        Assert.NotNull(loaded);
        Assert.True(loaded!.Get(0));
        Assert.True(loaded.Get(42));
        Assert.True(loaded.Get(Width * Height - 1));
        Assert.False(loaded.Get(1));
    }

    [Fact]
    public void Load_ShouldReturnNullWhenFileMissing()
    {
        var storage = CreateStorage();

        var loaded = storage.Load("uuid-missing", Width * Height);

        Assert.Null(loaded);
    }

    [Fact]
    public void Load_ShouldReturnNullWhenFileSizeMismatch()
    {
        var storage = CreateStorage();
        var bitmap = new BitArray(Width * Height);
        storage.Save("uuid-corrupt", bitmap);

        var corruptPath = Path.Combine(_tempDir, WorldId.ToString(), "uuid-corrupt.bin");
        File.WriteAllBytes(corruptPath, [0x01, 0x02, 0x03]);

        var loaded = storage.Load("uuid-corrupt", Width * Height);

        Assert.Null(loaded);
    }

    [Fact]
    public void Load_ShouldReturnNullWhenAccountNameIsBlank()
    {
        var storage = CreateStorage();

        Assert.Null(storage.Load(string.Empty, Width * Height));
        Assert.Null(storage.Load(" ", Width * Height));
    }

    [Fact]
    public void Save_ShouldCreateWorldSubdirectory()
    {
        var storage = CreateStorage();
        var bitmap = new BitArray(Width * Height);

        storage.Save("uuid-mkdir", bitmap);

        Assert.True(File.Exists(Path.Combine(_tempDir, WorldId.ToString(), "uuid-mkdir.bin")));
    }

    private FileExplorationStorage CreateStorage() => new(_tempDir, () => WorldId);
}
