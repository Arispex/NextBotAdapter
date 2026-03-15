using System;
using System.Diagnostics.CodeAnalysis;
using NextBotAdapter.Rest;
using NextBotAdapter.Services;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;

namespace NextBotAdapter.Plugin;

[ExcludeFromCodeCoverage]
[ApiVersion(2, 1)]
public sealed class NextBotAdapterPlugin(Main game) : TerrariaPlugin(game)
{
    private PersistedWhitelistService? _whitelistService;

    public override string Author => "Arispex";

    public override string Description => "Provides NextBot with TShock server information.";

    public override string Name => "NextBotAdapter";

    public override Version Version => new(1, 0, 0);

    public override void Initialize()
    {
        PluginLogger.Info("Lifecycle", "Initializing plugin.");

        _whitelistService = new PersistedWhitelistService(new WhitelistConfigService());
        WhitelistEndpoints.Service = _whitelistService;
        ConfigEndpoints.Service = new ConfigurationReloadService(_whitelistService);
        PluginLogger.Info("Lifecycle", "Whitelist service initialized.");

        EndpointRegistrar.Register(TShock.RestApi);
        PluginLogger.Info("REST", $"Registered {EndpointRegistrar.CreateCommands().Count} REST endpoints.");

        GetDataHandlers.PlayerInfo.Register(OnPlayerInfo, HandlerPriority.Highest);
        PluginLogger.Info("Lifecycle", "Registered PlayerInfo whitelist check hook.");
        PluginLogger.Info(
            "Config",
            $"Active whitelist settings: enabled={_whitelistService.Settings.Enabled}, caseSensitive={_whitelistService.Settings.CaseSensitive}, entries={_whitelistService.GetAll().Count}");

        if (!_whitelistService.Settings.Enabled)
        {
            PluginLogger.Warn("Whitelist", "Whitelist is disabled.");
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            PluginLogger.Info("Lifecycle", "Disposing plugin.");
            GetDataHandlers.PlayerInfo.UnRegister(OnPlayerInfo);
        }

        base.Dispose(disposing);
    }

    private void OnPlayerInfo(object? _, GetDataHandlers.PlayerInfoEventArgs args)
    {
        if (_whitelistService is null)
        {
            return;
        }

        if (_whitelistService.TryValidateJoin(args.Name, out var denialReason))
        {
            return;
        }

        PluginLogger.Warn("Whitelist", $"Rejected player: {args.Name}");
        args.Player?.Disconnect(denialReason ?? "You are not on the whitelist.");
        args.Handled = true;
    }
}
