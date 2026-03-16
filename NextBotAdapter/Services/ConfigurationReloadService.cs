namespace NextBotAdapter.Services;

public sealed class ConfigurationReloadService(PersistedWhitelistService whitelistService) : IConfigurationReloadService
{
    public void ReloadAll()
    {
        PluginLogger.Info("开始处理配置热重载请求。");
        whitelistService.Reload();
        PluginLogger.Info("处理配置热重载请求成功。");
    }
}
