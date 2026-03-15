using TShockAPI;

namespace NextBotAdapter.Services;

public sealed class ConfigurationReloadService(PersistedWhitelistService whitelistService) : IConfigurationReloadService
{
    public void ReloadAll()
    {
        whitelistService.Reload();
        TShock.Log?.ConsoleInfo("NextBotAdapter configuration reloaded.");
    }
}
