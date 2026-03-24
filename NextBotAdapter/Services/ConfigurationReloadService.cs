namespace NextBotAdapter.Services;

public sealed class ConfigurationReloadService(PersistedWhitelistService whitelistService) : IConfigurationReloadService
{
    public void ReloadAll()
    {
        PluginLogger.Info("开始重新加载插件配置。");
        whitelistService.Reload();
        PluginLogger.Info("插件配置重新加载完成。");
    }
}
