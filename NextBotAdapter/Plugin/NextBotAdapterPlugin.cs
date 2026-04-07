using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using NextBotAdapter.Models;
using NextBotAdapter.Rest;
using NextBotAdapter.Services;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.Hooks;

namespace NextBotAdapter.Plugin;

[ExcludeFromCodeCoverage]
[ApiVersion(2, 1)]
public sealed class NextBotAdapterPlugin(Main game) : TerrariaPlugin(game)
{
    private PersistedWhitelistService? _whitelistService;
    private OnlineTimeService? _onlineTimeService;
    private LoginConfirmationService? _loginConfirmationService;
    private LoginConfirmationSettings _loginConfirmationSettings = LoginConfirmationSettings.Default;

    public override string Author => "Arispex";

    public override string Description => "Provides NextBot with TShock server information.";

    public override string Name => "NextBotAdapter";

    public override Version Version => new(1, 0, 0);

    public override void Initialize()
    {
        PluginLogger.Info("插件开始初始化。");
        AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;

        var configService = new PluginConfigService();
        configService.EnsureConfigComplete();
        var whitelistFileService = new WhitelistFileService(configService.ConfigDirectoryPath);
        _whitelistService = new PersistedWhitelistService(configService, whitelistFileService);
        WhitelistEndpoints.Service = _whitelistService;
        ConfigEndpoints.ReloadService = new ConfigurationReloadService(_whitelistService);
        ConfigEndpoints.ConfigService = configService;
        MapEndpoints.Service = new MapImageService();
        WorldEndpoints.WorldFileService = new WorldFileService();
        WorldEndpoints.MapFileService = new MapFileService();

        _onlineTimeService = new OnlineTimeService(new OnlineTimeFileService());
        UserEndpoints.OnlineTimeService = _onlineTimeService;
        LeaderboardEndpoints.OnlineTimeService = _onlineTimeService;

        _loginConfirmationSettings = configService.LoadLoginConfirmationSettings();
        _loginConfirmationService = new LoginConfirmationService();
        SecurityEndpoints.Service = _loginConfirmationService;

        EndpointRegistrar.Register(TShock.RestApi);

        GetDataHandlers.PlayerInfo.Register(OnPlayerInfo, HandlerPriority.Highest);
        PlayerHooks.PlayerPreLogin += OnPlayerPreLogin;
        PlayerHooks.PlayerPostLogin += OnPlayerPostLogin;
        ServerApi.Hooks.ServerLeave.Register(this, OnServerLeave);

        if (!_whitelistService.Settings.Enabled)
        {
            PluginLogger.Warn("白名单功能未启用，玩家入服时将跳过白名单校验。");
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _onlineTimeService?.PersistAllSessions();
            GetDataHandlers.PlayerInfo.UnRegister(OnPlayerInfo);
            PlayerHooks.PlayerPreLogin -= OnPlayerPreLogin;
            PlayerHooks.PlayerPostLogin -= OnPlayerPostLogin;
            ServerApi.Hooks.ServerLeave.Deregister(this, OnServerLeave);
            AppDomain.CurrentDomain.AssemblyResolve -= CurrentDomain_AssemblyResolve;
        }

        base.Dispose(disposing);
    }

    private static bool HasIpChanged(string? knownIps, string currentIp)
    {
        if (string.IsNullOrEmpty(knownIps))
        {
            return true;
        }

        try
        {
            var ips = Newtonsoft.Json.JsonConvert.DeserializeObject<string[]>(knownIps);
            return ips is not { Length: > 0 } || ips[^1] != currentIp;
        }
        catch
        {
            return true;
        }
    }

    private void OnPlayerPostLogin(PlayerPostLoginEventArgs args)
    {
        _onlineTimeService?.StartSession(args.Player.Account.Name);
    }

    private void OnServerLeave(LeaveEventArgs args)
    {
        var player = TShock.Players[args.Who];
        if (player?.Account?.Name is { } username)
        {
            _onlineTimeService?.EndSession(username);
        }
    }

    private void OnPlayerInfo(object? _, GetDataHandlers.PlayerInfoEventArgs args)
    {
        if (_whitelistService is not null && !_whitelistService.TryValidateJoin(args.Name, out var denialReason))
        {
            PluginLogger.Warn($"玩家 {args.Name} 入服被拒绝，原因：{denialReason ?? "You are not on the whitelist."}");
            args.Player?.Disconnect(denialReason ?? "You are not on the whitelist.");
            args.Handled = true;
        }
    }

    private void OnPlayerPreLogin(PlayerPreLoginEventArgs args)
    {
        if (!_loginConfirmationSettings.Enabled || _loginConfirmationService is null) return;

        var player = args.Player;
        var loginName = args.LoginName;
        var uuid = player.UUID;

        if (_loginConfirmationSettings.DetectUuid && string.IsNullOrEmpty(uuid))
        {
            args.Handled = true;
            player.Disconnect(_loginConfirmationSettings.EmptyUuidMessage);
            PluginLogger.Warn($"玩家 {loginName} 登入被拒绝：UUID 为空。");
            return;
        }

        var account = TShock.UserAccounts.GetUserAccountByName(loginName);
        if (account is null) return;

        string? detectedUuid = null;
        string? detectedIp = null;

        if (_loginConfirmationSettings.DetectUuid && account.UUID != uuid)
        {
            detectedUuid = uuid;
        }

        if (_loginConfirmationSettings.DetectIp && HasIpChanged(account.KnownIps, player.IP))
        {
            detectedIp = player.IP;
        }

        if (detectedUuid is null && detectedIp is null) return;

        if (_loginConfirmationService.ConsumeApproval(loginName, uuid, player.IP))
        {
            PluginLogger.Info($"玩家 {loginName} 通过二次确认登入，UUID 或 IP 已变更。");
            return;
        }

        if (_loginConfirmationService.HasActiveApproval(loginName))
        {
            args.Handled = true;
            player.Disconnect(_loginConfirmationSettings.DeviceMismatchMessage);
            PluginLogger.Warn($"玩家 {loginName} 登入被拒绝：存在有效审批但设备不匹配。");
            return;
        }

        if (_loginConfirmationService.HasActivePending(loginName))
        {
            args.Handled = true;
            player.Disconnect(_loginConfirmationSettings.PendingExistsMessage);
            PluginLogger.Warn($"玩家 {loginName} 登入被拒绝：已存在待确认的登入请求。");
            return;
        }

        _loginConfirmationService.RecordBlockedLogin(loginName, uuid, player.IP);
        var changed = detectedUuid != null && detectedIp != null ? "UUID 和 IP"
            : detectedUuid != null ? "UUID" : "IP";
        args.Handled = true;
        player.Disconnect(_loginConfirmationSettings.ChangeDetectedMessage.Replace("{changed}", changed));
        PluginLogger.Warn($"玩家 {loginName} 登入被拒绝：{changed} 发生变化。");
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
