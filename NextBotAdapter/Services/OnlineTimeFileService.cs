using System.IO;
using Newtonsoft.Json;
using NextBotAdapter.Models;

namespace NextBotAdapter.Services;

public sealed class OnlineTimeFileService : IOnlineTimeFileService
{
    private static readonly JsonSerializerSettings JsonSettings = new() { Formatting = Formatting.Indented };
    private readonly string _filePath;

    public OnlineTimeFileService()
        : this(Path.Combine(TShockAPI.TShock.SavePath, "NextBotAdapter", "OnlineTime.json"))
    {
    }

    public OnlineTimeFileService(string filePath)
    {
        _filePath = filePath;
    }

    public string FilePath => _filePath;

    public OnlineTimeStore Load()
    {
        EnsureDirectory();

        if (!File.Exists(_filePath))
        {
            Save(OnlineTimeStore.Empty);
            PluginLogger.Info("在线时长数据文件已创建。");
            return OnlineTimeStore.Empty;
        }

        try
        {
            var store = JsonConvert.DeserializeObject<OnlineTimeStore>(File.ReadAllText(_filePath), JsonSettings);
            var result = store ?? OnlineTimeStore.Empty;
            PluginLogger.Info($"在线时长数据加载完成，共 {result.Records.Count} 条记录。");
            return result;
        }
        catch (Exception ex)
        {
            PluginLogger.Warn($"在线时长数据加载失败，已回退为空记录，原因：{ex.Message}");
            return OnlineTimeStore.Empty;
        }
    }

    public void Save(OnlineTimeStore store)
    {
        EnsureDirectory();
        File.WriteAllText(_filePath, JsonConvert.SerializeObject(store, JsonSettings));
    }

    private void EnsureDirectory()
    {
        var dir = Path.GetDirectoryName(_filePath);
        if (dir is not null)
        {
            Directory.CreateDirectory(dir);
        }
    }
}
