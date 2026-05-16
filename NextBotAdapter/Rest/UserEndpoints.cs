using NextBotAdapter.Infrastructure;
using NextBotAdapter.Services;
using Rests;

namespace NextBotAdapter.Rest;

public static class UserEndpoints
{
    public static IOnlineTimeService? OnlineTimeService { get; set; }
    public static IPlayerMapImageService? PlayerMapImageService { get; set; }
    public static IPlayerExplorationTracker? ExplorationTracker { get; set; }
    public static IUserAccountLookup? AccountLookup { get; set; }

    public static object Inventory(RestRequestArgs args)
        => Inventory(ReadRouteUser(args), UserDataService.Default);

    public static object Inventory(string? user, IPlayerDataAccessor accessor)
    {
        if (string.IsNullOrWhiteSpace(user))
        {
            return EndpointResponseFactory.MissingUser();
        }

        if (!UserInventoryService.TryGetInventory(user, accessor, out var inventory, out var error))
        {
            return EndpointResponseFactory.Error(error ?? "User was not found.");
        }

        return new RestObject("200") { { "items", inventory.Items } };
    }

    public static object Stats(RestRequestArgs args)
        => Stats(ReadRouteUser(args), UserDataService.Default, OnlineTimeService, ExplorationTracker);

    public static object Stats(
        string? user,
        IPlayerDataAccessor accessor,
        IOnlineTimeService? onlineTimeService = null,
        IPlayerExplorationTracker? explorationTracker = null)
    {
        if (string.IsNullOrWhiteSpace(user))
        {
            return EndpointResponseFactory.MissingUser();
        }

        if (!UserInfoService.TryGetUserInfo(user, accessor, onlineTimeService, explorationTracker, out var response, out var error))
        {
            return EndpointResponseFactory.Error(error ?? "User was not found.");
        }

        return new RestObject("200")
        {
            { "health", response.Health },
            { "maxHealth", response.MaxHealth },
            { "mana", response.Mana },
            { "maxMana", response.MaxMana },
            { "questsCompleted", response.QuestsCompleted },
            { "deathsPve", response.DeathsPve },
            { "deathsPvp", response.DeathsPvp },
            { "onlineSeconds", response.OnlineSeconds },
            { "mapExplorationPercent", response.MapExplorationPercent }
        };
    }

    public static object MapImage(RestRequestArgs args)
        => MapImage(ReadRouteUser(args), PlayerMapImageService, ExplorationTracker, AccountLookup);

    public static RestObject MapImage(
        string? user,
        IPlayerMapImageService? playerService,
        IPlayerExplorationTracker? tracker,
        IUserAccountLookup? lookup)
    {
        if (string.IsNullOrWhiteSpace(user))
        {
            return EndpointResponseFactory.MissingUser();
        }

        var trimmedUser = user.Trim();

        if (playerService is null || tracker is null || lookup is null)
        {
            return EndpointResponseFactory.Error("Player exploration service is not configured.", "500");
        }

        if (!lookup.TryGetAccountName(trimmedUser, out var accountName))
        {
            return EndpointResponseFactory.Error("User was not found.");
        }

        try
        {
            var bitmap = string.IsNullOrEmpty(accountName) ? null : tracker.GetBitmap(accountName);
            if (bitmap is null)
            {
                PluginLogger.Info($"开始生成玩家视角地图，user={trimmedUser}（无探索数据，返回全黑图）。");
                var blank = playerService.GenerateBlank(trimmedUser);
                PluginLogger.Info($"玩家视角地图生成完成，文件名：{blank.FileName}。");
                return new RestObject("200")
                {
                    { "fileName", blank.FileName },
                    { "base64", Convert.ToBase64String(blank.Content) }
                };
            }

            PluginLogger.Info($"开始生成玩家视角地图，user={trimmedUser}。");
            var result = playerService.Generate(trimmedUser, bitmap);
            PluginLogger.Info($"玩家视角地图生成完成，文件名：{result.FileName}。");
            return new RestObject("200")
            {
                { "fileName", result.FileName },
                { "base64", Convert.ToBase64String(result.Content) }
            };
        }
        catch (Exception ex)
        {
            PluginLogger.Error($"玩家视角地图生成失败，user={trimmedUser}，原因：{ex.Message}");
            return EndpointResponseFactory.Error(ex.Message, "500");
        }
    }

    private static string? ReadRouteUser(RestRequestArgs args)
        => RouteParameters.ReadDecodedRouteParam(args, RequestParameters.User);
}
