using NextBotAdapter.Infrastructure;
using NextBotAdapter.Services;
using Rests;

namespace NextBotAdapter.Rest;

public static class MapEndpoints
{
    public static IMapImageService Service { get; set; } = null!;
    public static IPlayerMapImageService? PlayerService { get; set; }
    public static IPlayerExplorationTracker? ExplorationTracker { get; set; }
    public static IUserAccountLookup? AccountLookup { get; set; }

    public static object Image(RestRequestArgs args)
    {
        var player = ReadPlayerQuery(args);
        if (string.IsNullOrWhiteSpace(player))
        {
            return Image(Service);
        }

        return ImageForPlayer(player.Trim(), PlayerService, ExplorationTracker, AccountLookup);
    }

    public static RestObject Image(IMapImageService service)
    {
        try
        {
            PluginLogger.Info("开始生成世界地图图片。");
            var result = service.Generate();
            PluginLogger.Info($"世界地图图片生成完成，文件名：{result.FileName}。");
            return new RestObject("200")
            {
                { "fileName", result.FileName },
                { "base64", Convert.ToBase64String(result.Content) }
            };
        }
        catch (Exception ex)
        {
            PluginLogger.Error($"世界地图图片生成失败，原因：{ex.Message}");
            return EndpointResponseFactory.Error(ex.Message, "500");
        }
    }

    public static RestObject ImageForPlayer(
        string? player,
        IPlayerMapImageService? playerService,
        IPlayerExplorationTracker? tracker,
        IUserAccountLookup? lookup)
    {
        if (string.IsNullOrWhiteSpace(player))
        {
            return EndpointResponseFactory.Error("Missing required parameter 'player'.");
        }

        if (playerService is null || tracker is null || lookup is null)
        {
            return EndpointResponseFactory.Error("Player exploration service is not configured.", "500");
        }

        if (!lookup.TryGetAccountUuid(player, out var accountUuid))
        {
            return EndpointResponseFactory.Error("User was not found.");
        }

        try
        {
            var bitmap = string.IsNullOrEmpty(accountUuid) ? null : tracker.GetBitmap(accountUuid);
            if (bitmap is null)
            {
                PluginLogger.Info($"开始生成玩家视角地图，player={player}（无探索数据，返回全黑图）。");
                var blank = playerService.GenerateBlank(player);
                PluginLogger.Info($"玩家视角地图生成完成，文件名：{blank.FileName}。");
                return new RestObject("200")
                {
                    { "fileName", blank.FileName },
                    { "base64", Convert.ToBase64String(blank.Content) }
                };
            }

            PluginLogger.Info($"开始生成玩家视角地图，player={player}。");
            var result = playerService.Generate(player, bitmap);
            PluginLogger.Info($"玩家视角地图生成完成，文件名：{result.FileName}。");
            return new RestObject("200")
            {
                { "fileName", result.FileName },
                { "base64", Convert.ToBase64String(result.Content) }
            };
        }
        catch (Exception ex)
        {
            PluginLogger.Error($"玩家视角地图生成失败，player={player}，原因：{ex.Message}");
            return EndpointResponseFactory.Error(ex.Message, "500");
        }
    }

    private static string? ReadPlayerQuery(RestRequestArgs args)
        => args.Parameters?[RequestParameters.Player]
           ?? args.Verbs?[RequestParameters.Player]
           ?? args.Request?.Parameters?[RequestParameters.Player];
}
