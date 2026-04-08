using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Threading.Tasks;
using NextBotAdapter.Models;
using NextBotAdapter.Rest;
using NextBotAdapter.Services;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.DB;
using TShockAPI.Hooks;

namespace NextBotAdapter.Plugin;

[ExcludeFromCodeCoverage]
[ApiVersion(2, 1)]
public sealed class NextBotAdapterPlugin(Main game) : TerrariaPlugin(game)
{
    private PluginConfigService? _configService;
    private WhitelistService? _whitelistService;
    private OnlineTimeService? _onlineTimeService;
    private LoginConfirmationService? _loginConfirmationService;
    private NextBotSessionProbeService? _nextBotProbeService;

    public override string Author => "Arispex";

    public override string Description => "Provides NextBot with TShock server information.";

    public override string Name => "NextBotAdapter";

    public override Version Version => new(1, 0, 0);

    public override void Initialize()
    {
        PluginLogger.Info("插件开始初始化。");
        AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;

        _configService = new PluginConfigService();
        _configService.EnsureConfigComplete();
        _whitelistService = new WhitelistService(_configService);
        _onlineTimeService = new OnlineTimeService();
        WhitelistEndpoints.Service = _whitelistService;
        ConfigEndpoints.ReloadService = new ConfigurationReloadService(_configService, _whitelistService, _onlineTimeService);
        ConfigEndpoints.ConfigService = _configService;
        MapEndpoints.Service = new MapImageService();
        WorldEndpoints.WorldFileService = new WorldFileService();
        WorldEndpoints.MapFileService = new MapFileService();

        UserEndpoints.OnlineTimeService = _onlineTimeService;
        LeaderboardEndpoints.OnlineTimeService = _onlineTimeService;

        _loginConfirmationService = new LoginConfirmationService();
        SecurityEndpoints.Service = _loginConfirmationService;

        _nextBotProbeService = new NextBotSessionProbeService();
        ConfigEndpoints.NextBotProbeService = _nextBotProbeService;

        EndpointRegistrar.Register(TShock.RestApi);

        _ = Task.Run(VerifyNextBotConnectionAsync);

        GetDataHandlers.PlayerInfo.Register(OnPlayerInfo, HandlerPriority.Highest);
        PlayerHooks.PlayerPreLogin += OnPlayerPreLogin;
        PlayerHooks.PlayerPostLogin += OnPlayerPostLogin;
        ServerApi.Hooks.ServerLeave.Register(this, OnServerLeave);
        ServerApi.Hooks.NetGreetPlayer.Register(this, OnNetGreetPlayer);

        if (!_whitelistService.Settings.Enabled)
        {
            PluginLogger.Warn("白名单功能未启用，玩家入服时将跳过白名单校验。");
        }

        var initialLoginSettings = _configService.LoadLoginConfirmationSettings();
        if (initialLoginSettings.AutoLogin)
        {
            if (!IsAutoLoginConfigurationSafe(initialLoginSettings))
            {
                PluginLogger.Warn("autoLogin 已启用但 loginConfirmation.enabled 为 false 或 detectUuid/detectIp 全为 false，自动登入将被跳过以防止任意账号冒充。");
            }
            else
            {
                PluginLogger.Warn("autoLogin 已启用：设备指纹 (UUID + 上次登录 IP) 将替代密码作为唯一鉴权因素。");
            }
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
            ServerApi.Hooks.NetGreetPlayer.Deregister(this, OnNetGreetPlayer);
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
            PluginLogger.Warn($"玩家 {args.Name} 入服被拒绝，原因：{denialReason ?? "你不在白名单中"}");
            args.Player?.Disconnect(denialReason ?? "你不在白名单中");
            args.Handled = true;
        }
    }

    private void OnPlayerPreLogin(PlayerPreLoginEventArgs args)
    {
        var settings = _configService?.LoadLoginConfirmationSettings() ?? LoginConfirmationSettings.Default;
        if (!settings.Enabled || _loginConfirmationService is null) return;

        var player = args.Player;
        var loginName = args.LoginName;
        var account = TShock.UserAccounts.GetUserAccountByName(loginName);

        if (EvaluateLoginConfirmation(player, loginName, account, settings, out var denialReason))
        {
            return;
        }

        args.Handled = true;
        player.Disconnect(denialReason!);
    }

    private void OnNetGreetPlayer(GreetPlayerEventArgs args)
    {
        var player = TShock.Players[args.Who];
        if (player is null || player.IsLoggedIn)
        {
            return;
        }

        var settings = _configService?.LoadLoginConfirmationSettings() ?? LoginConfirmationSettings.Default;
        if (!settings.AutoLogin)
        {
            return;
        }

        if (!IsAutoLoginConfigurationSafe(settings))
        {
            return;
        }

        var loginName = player.Name;
        if (string.IsNullOrEmpty(loginName))
        {
            return;
        }

        var account = TShock.UserAccounts.GetUserAccountByName(loginName);
        if (account is null)
        {
            return;
        }

        if (settings.Enabled && _loginConfirmationService is not null)
        {
            if (!EvaluateLoginConfirmation(player, loginName, account, settings, out var denialReason))
            {
                player.Disconnect(denialReason!);
                return;
            }
        }

        PerformAutoLogin(player, account);
    }

    // Returns true if login should be allowed; false if rejected (denialReason set).
    // Safe to call with account == null (returns true — nothing to check).
    private bool EvaluateLoginConfirmation(
        TSPlayer player,
        string loginName,
        UserAccount? account,
        LoginConfirmationSettings settings,
        out string? denialReason)
    {
        denialReason = null;
        var uuid = player.UUID ?? string.Empty;

        if (settings.DetectUuid && string.IsNullOrEmpty(uuid))
        {
            denialReason = settings.EmptyUuidMessage;
            PluginLogger.Warn($"玩家 {loginName} 登入被拒绝：UUID 为空。");
            return false;
        }

        if (account is null)
        {
            return true;
        }

        string? detectedUuid = null;
        string? detectedIp = null;

        if (settings.DetectUuid && account.UUID != uuid)
        {
            detectedUuid = uuid;
        }

        if (settings.DetectIp && HasIpChanged(account.KnownIps, player.IP))
        {
            detectedIp = player.IP;
        }

        if (detectedUuid is null && detectedIp is null)
        {
            return true;
        }

        if (_loginConfirmationService!.ConsumeApproval(loginName, uuid, player.IP))
        {
            PluginLogger.Info($"玩家 {loginName} 通过二次确认登入，UUID 或 IP 已变更。");
            return true;
        }

        if (_loginConfirmationService.HasActiveApproval(loginName))
        {
            denialReason = settings.DeviceMismatchMessage;
            PluginLogger.Warn($"玩家 {loginName} 登入被拒绝：存在有效审批但设备不匹配。");
            return false;
        }

        if (_loginConfirmationService.HasActivePending(loginName))
        {
            denialReason = settings.PendingExistsMessage;
            PluginLogger.Warn($"玩家 {loginName} 登入被拒绝：已存在待确认的登入请求。");
            return false;
        }

        _loginConfirmationService.RecordBlockedLogin(loginName, uuid, player.IP);
        var changed = detectedUuid != null && detectedIp != null ? "UUID 和 IP"
            : detectedUuid != null ? "UUID" : "IP";
        denialReason = settings.ChangeDetectedMessage.Replace("{changed}", changed);
        PluginLogger.Warn($"玩家 {loginName} 登入被拒绝：{changed} 发生变化。");
        return false;
    }

    private static bool IsAutoLoginConfigurationSafe(LoginConfirmationSettings settings)
        => settings.Enabled && (settings.DetectUuid || settings.DetectIp);

    private void PerformAutoLogin(TSPlayer player, UserAccount account)
    {
        try
        {
            player.Group = TShock.Groups.GetGroupByName(account.Group);
            player.tempGroup = null;
            player.Account = account;
            player.IsLoggedIn = true;
            player.IsDisabledForSSC = false;

            if (Main.ServerSideCharacter)
            {
                player.PlayerData = TShock.CharacterDB.GetPlayerData(player, account.ID);
                player.PlayerData?.RestoreCharacter(player);
            }

            TShock.UserAccounts.SetUserAccountUUID(account, player.UUID ?? string.Empty);
            TShock.UserAccounts.UpdateLogin(account);

            _onlineTimeService?.StartSession(account.Name);
            player.SendSuccessMessage($"已自动登入账号 {account.Name}。");
            PluginLogger.Info($"玩家 {account.Name} 已自动登入。");
        }
        catch (Exception ex)
        {
            PluginLogger.Warn($"玩家 {account.Name} 自动登入失败，原因：{ex.Message}");
        }
    }

    private async Task VerifyNextBotConnectionAsync()
    {
        try
        {
            if (_configService is null || _nextBotProbeService is null)
            {
                return;
            }

            var settings = _configService.Load().NextBot;
            var result = await _nextBotProbeService.ProbeAsync(settings).ConfigureAwait(false);

            switch (result.Status)
            {
                case NextBotProbeStatus.Ok:
                    PluginLogger.Info("连接 NextBot 成功");
                    break;
                case NextBotProbeStatus.Skipped:
                    PluginLogger.Info($"连接 NextBot 跳过：{result.Message}");
                    break;
                default:
                    PluginLogger.Warn($"连接 NextBot 失败：{result.Message}");
                    break;
            }
        }
        catch (Exception ex)
        {
            PluginLogger.Warn($"连接 NextBot 失败：{ex.Message}");
        }
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
