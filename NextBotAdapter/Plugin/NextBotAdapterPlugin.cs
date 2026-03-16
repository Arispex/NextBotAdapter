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
        PluginLogger.Info("开始初始化插件。");

        _whitelistService = new PersistedWhitelistService(new WhitelistConfigService());
        WhitelistEndpoints.Service = _whitelistService;
        ConfigEndpoints.Service = new ConfigurationReloadService(_whitelistService);
        PluginLogger.Info("初始化白名单服务完成。");

        EndpointRegistrar.Register(TShock.RestApi);
        PluginLogger.Info($"注册 REST 端点完成，共 {EndpointRegistrar.CreateCommands().Count} 个。");

        GetDataHandlers.PlayerInfo.Register(OnPlayerInfo, HandlerPriority.Highest);
        PluginLogger.Info("注册玩家信息校验钩子完成。");
        PluginLogger.Info(
            $"应用当前白名单配置完成。启用状态为 {_whitelistService.Settings.Enabled}，区分大小写为 {_whitelistService.Settings.CaseSensitive}，当前共有 {_whitelistService.GetAll().Count} 个条目。");

        if (!_whitelistService.Settings.Enabled)
        {
            PluginLogger.Warn("检测到白名单功能未启用，玩家进入服务器时将不会进行白名单校验。");
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            PluginLogger.Info("开始释放插件资源。");
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

        PluginLogger.Warn($"拒绝玩家 {args.Name} 进入服务器，原因：{denialReason ?? "You are not on the whitelist."}");
        args.Player?.Disconnect(denialReason ?? "You are not on the whitelist.");
        args.Handled = true;
    }
}
