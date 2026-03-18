using NextBotAdapter.Infrastructure;
using NextBotAdapter.Models.Responses;
using NextBotAdapter.Services;
using Rests;

namespace NextBotAdapter.Rest;

public static class MapEndpoints
{
    public static IMapImageService Service { get; set; } = null!;

    public static object Image(RestRequestArgs _)
        => Image(Service);

    public static RestObject Image(IMapImageService service)
    {
        try
        {
            PluginLogger.Info("世界地图图片正在生成......");
            var result = service.GenerateAndCache();
            PluginLogger.Info($"世界地图图片生成完成，文件名：{result.FileName}，缓存路径：{result.FilePath}。");
            return EndpointResponseFactory.Success(new MapImageResponse(
                result.FileName,
                Convert.ToBase64String(result.Content)));
        }
        catch (Exception ex)
        {
            PluginLogger.Error($"世界地图图片生成失败，原因：{ex.Message}");
            return EndpointResponseFactory.Error("500", ErrorCodes.MapImageGenerationFailed, ex.Message);
        }
    }
}
