using System.IO;
using Newtonsoft.Json;
using NextBotAdapter.Models;

namespace NextBotAdapter.Services;

public sealed class WhitelistFileService
{
    private static readonly JsonSerializerSettings JsonSettings = new()
    {
        Formatting = Formatting.Indented
    };
    private readonly string _configDirectoryPath;

    public WhitelistFileService(string configDirectoryPath)
    {
        _configDirectoryPath = configDirectoryPath;
    }

    public string WhitelistFilePath => Path.Combine(_configDirectoryPath, "Whitelist.json");

    public WhitelistStore LoadWhitelist()
    {
        Directory.CreateDirectory(_configDirectoryPath);
        if (!File.Exists(WhitelistFilePath))
        {
            WriteWhitelistFile(WhitelistStore.Empty);
            PluginLogger.Info("默认白名单数据文件已创建。");
            return WhitelistStore.Empty;
        }

        try
        {
            var store = JsonConvert.DeserializeObject<WhitelistStore>(File.ReadAllText(WhitelistFilePath), JsonSettings);
            var whitelist = store ?? WhitelistStore.Empty;
            PluginLogger.Info($"白名单数据加载完成，共 {whitelist.Users.Count} 个条目。");
            return whitelist;
        }
        catch (Exception ex)
        {
            PluginLogger.Warn($"白名单数据加载失败，已回退为空白名单，原因：{ex.Message}");
            return WhitelistStore.Empty;
        }
    }

    public void SaveWhitelist(WhitelistStore store)
    {
        Directory.CreateDirectory(_configDirectoryPath);
        WriteWhitelistFile(store);
        PluginLogger.Info($"白名单数据保存完成，共 {store.Users.Count} 个条目。");
    }

    private void WriteWhitelistFile(WhitelistStore store)
    {
        File.WriteAllText(WhitelistFilePath, JsonConvert.SerializeObject(store, JsonSettings));
    }
}
