using NextBotAdapter.Infrastructure;
using NextBotAdapter.Services;
using Rests;

namespace NextBotAdapter.Rest;

public static class MapEndpoints
{
    public static IMapImageService Service { get; set; } = null!;

    public static IWorldExploredMapImageService? ExploredService { get; set; }

    public static object Image(RestRequestArgs args)
        => Image(Service);

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

    public static object ExploredImage(RestRequestArgs args)
        => ExploredImage(ExploredService);

    public static RestObject ExploredImage(IWorldExploredMapImageService? service)
    {
        if (service is null)
        {
            return EndpointResponseFactory.Error("World explored map service is not configured.", "500");
        }

        try
        {
            PluginLogger.Info("开始生成全玩家探索并集地图图片。");
            var result = service.Generate();
            PluginLogger.Info($"全玩家探索并集地图图片生成完成，文件名：{result.FileName}。");
            return new RestObject("200")
            {
                { "fileName", result.FileName },
                { "base64", Convert.ToBase64String(result.Content) }
            };
        }
        catch (Exception ex)
        {
            PluginLogger.Error($"全玩家探索并集地图图片生成失败，原因：{ex.Message}");
            return EndpointResponseFactory.Error(ex.Message, "500");
        }
    }
}
