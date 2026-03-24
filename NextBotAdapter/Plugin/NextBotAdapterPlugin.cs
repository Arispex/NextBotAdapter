using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
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
        PluginLogger.Info("插件开始初始化。");
        AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;

        var configService = new WhitelistConfigService();
        _whitelistService = new PersistedWhitelistService(configService);
        WhitelistEndpoints.Service = _whitelistService;
        ConfigEndpoints.Service = new ConfigurationReloadService(_whitelistService);
        MapEndpoints.Service = new MapImageService();
        WorldEndpoints.WorldFileService = new WorldFileService();
        WorldEndpoints.MapFileService = new MapFileService();

        EndpointRegistrar.Register(TShock.RestApi);

        GetDataHandlers.PlayerInfo.Register(OnPlayerInfo, HandlerPriority.Highest);

        if (!_whitelistService.Settings.Enabled)
        {
            PluginLogger.Warn("白名单功能未启用，玩家入服时将跳过白名单校验。");
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            GetDataHandlers.PlayerInfo.UnRegister(OnPlayerInfo);
            AppDomain.CurrentDomain.AssemblyResolve -= CurrentDomain_AssemblyResolve;
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

        PluginLogger.Warn($"玩家 {args.Name} 入服被拒绝，原因：{denialReason ?? "You are not on the whitelist."}");
        args.Player?.Disconnect(denialReason ?? "You are not on the whitelist.");
        args.Handled = true;
    }

    private Assembly? CurrentDomain_AssemblyResolve(object? sender, ResolveEventArgs args)
    {
        var resourceName = $"embedded.{new AssemblyName(args.Name).Name}.dll";
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            return null;
        }

        var assemblyData = new byte[stream.Length];
        _ = stream.Read(assemblyData, 0, assemblyData.Length);
        return Assembly.Load(assemblyData);
    }
}
