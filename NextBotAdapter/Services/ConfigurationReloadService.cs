namespace NextBotAdapter.Services;

public sealed class ConfigurationReloadService(PersistedWhitelistService whitelistService) : IConfigurationReloadService
{
    public void ReloadAll()
    {
        PluginLogger.Info("插件配置正在重新加载......");
        whitelistService.Reload();
        PluginLogger.Info("插件配置重新加载完成。");
    }
}
