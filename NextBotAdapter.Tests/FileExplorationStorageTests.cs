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

        Assert.True(storage.Save("uuid-1", bitmap));
        var result = storage.Load("uuid-1", Width * Height);

        Assert.NotNull(result.Bitmap);
        Assert.False(result.FileMissing);
        Assert.True(result.Bitmap!.Get(0));
        Assert.True(result.Bitmap.Get(42));
        Assert.True(result.Bitmap.Get(Width * Height - 1));
        Assert.False(result.Bitmap.Get(1));
    }

    [Fact]
    public void Load_ShouldReportFileMissingWhenFileNotPresent()
    {
        var storage = CreateStorage();

        var result = storage.Load("uuid-missing", Width * Height);

        Assert.Null(result.Bitmap);
        // Confirmed missing: caller may safely populate the negative cache.
        Assert.True(result.FileMissing);
    }

    [Fact]
    public void Load_ShouldNotReportMissingWhenFileSizeMismatch()
    {
        var storage = CreateStorage();
        var bitmap = new BitArray(Width * Height);
        storage.Save("uuid-corrupt", bitmap);

        var corruptPath = Path.Combine(_tempDir, WorldId.ToString(), "uuid-corrupt.bin");
        File.WriteAllBytes(corruptPath, [0x01, 0x02, 0x03]);

        var result = storage.Load("uuid-corrupt", Width * Height);

        Assert.Null(result.Bitmap);
        // Corrupt / partial-write: file exists, so FileMissing must NOT be true.
        // Caller must not negative-cache; a future overwrite may make it readable.
        Assert.False(result.FileMissing);
    }

    [Fact]
    public void Load_ShouldNotReportMissingWhenAccountNameIsBlank()
    {
        var storage = CreateStorage();

        var blank1 = storage.Load(string.Empty, Width * Height);
        var blank2 = storage.Load(" ", Width * Height);

        Assert.Null(blank1.Bitmap);
        Assert.False(blank1.FileMissing);
        Assert.Null(blank2.Bitmap);
        Assert.False(blank2.FileMissing);
    }

    [Fact]
    public void Save_ShouldReturnTrueOnSuccessAndCreateWorldSubdirectory()
    {
        var storage = CreateStorage();
        var bitmap = new BitArray(Width * Height);

        var saved = storage.Save("uuid-mkdir", bitmap);

        Assert.True(saved);
        Assert.True(File.Exists(Path.Combine(_tempDir, WorldId.ToString(), "uuid-mkdir.bin")));
    }

    [Fact]
    public void Save_ShouldReturnFalseWhenAccountNameIsBlank()
    {
        var storage = CreateStorage();
        var bitmap = new BitArray(Width * Height);

        Assert.False(storage.Save(string.Empty, bitmap));
        Assert.False(storage.Save(" ", bitmap));
    }

    private FileExplorationStorage CreateStorage() => new(_tempDir, () => WorldId);
}
