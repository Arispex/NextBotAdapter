namespace NextBotAdapter.Services;

public sealed class ConfigurationReloadService(
    PluginConfigService configService,
    WhitelistService whitelistService,
    OnlineTimeService onlineTimeService) : IConfigurationReloadService
{
    public void ReloadAll()
    {
        PluginLogger.Info("开始重新加载插件配置。");
        _ = configService.Reload();
        whitelistService.Reload();
        onlineTimeService.Reload();
        PluginLogger.Info("插件配置重新加载完成。");
    }
}
