namespace NextBotAdapter.Services;

public sealed class ConfigurationReloadService(PersistedWhitelistService whitelistService) : IConfigurationReloadService
{
    public void ReloadAll()
    {
        PluginLogger.Info("Config", "Reload requested via REST API.");
        whitelistService.Reload();
        PluginLogger.Info("Config", "Reload completed successfully.");
    }
}
