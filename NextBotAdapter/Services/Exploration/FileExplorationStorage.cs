using System.Collections;
using System.IO;

namespace NextBotAdapter.Services;

public sealed class FileExplorationStorage : IExplorationStorage
{
    private readonly string _rootDirectory;
    private readonly Func<int> _worldIdProvider;

    public FileExplorationStorage(string rootDirectory, Func<int> worldIdProvider)
    {
        _rootDirectory = rootDirectory;
        _worldIdProvider = worldIdProvider;
    }

    public BitArray? Load(string accountName, int expectedBitCount)
    {
        if (string.IsNullOrWhiteSpace(accountName) || expectedBitCount <= 0)
        {
            return null;
        }

        var filePath = ResolveFilePath(accountName);
        if (!File.Exists(filePath))
        {
            return null;
        }

        try
        {
            var bytes = File.ReadAllBytes(filePath);
            var expectedByteCount = (expectedBitCount + 7) / 8;
            if (bytes.Length != expectedByteCount)
            {
                PluginLogger.Warn($"加载玩家探索数据失败，原因：文件大小不匹配，accountName={accountName}，expected={expectedByteCount}，actual={bytes.Length}");
                return null;
            }

            var bitmap = new BitArray(bytes)
            {
                Length = expectedBitCount
            };
            return bitmap;
        }
        catch (Exception ex)
        {
            PluginLogger.Warn($"加载玩家探索数据失败，accountName={accountName}，原因：{ex.Message}");
            return null;
        }
    }

    public void Save(string accountName, BitArray bitmap)
    {
        if (string.IsNullOrWhiteSpace(accountName) || bitmap.Length <= 0)
        {
            return;
        }

        var filePath = ResolveFilePath(accountName);
        try
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var byteCount = (bitmap.Length + 7) / 8;
            var bytes = new byte[byteCount];
            bitmap.CopyTo(bytes, 0);
            File.WriteAllBytes(filePath, bytes);
        }
        catch (Exception ex)
        {
            PluginLogger.Error($"保存玩家探索数据失败，accountName={accountName}，原因：{ex.Message}");
        }
    }

    private string ResolveFilePath(string accountName)
    {
        var worldId = _worldIdProvider();
        var safeName = SanitizeFileName(accountName);
        return Path.Combine(_rootDirectory, worldId.ToString(), safeName + ".bin");
    }

    private static string SanitizeFileName(string raw)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = raw.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (Array.IndexOf(invalid, chars[i]) >= 0)
            {
                chars[i] = '_';
            }
        }
        return new string(chars);
    }
}
