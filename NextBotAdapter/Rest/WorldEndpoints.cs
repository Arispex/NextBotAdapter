using NextBotAdapter.Infrastructure;
using NextBotAdapter.Models.Responses;
using NextBotAdapter.Services;
using Rests;

namespace NextBotAdapter.Rest;

public static class WorldEndpoints
{
    public static IWorldFileService WorldFileService { get; set; } = null!;

    public static object Progress(RestRequestArgs _)
        => Progress(WorldProgressService.DefaultSource);

    public static RestObject Progress(IWorldProgressSource source)
    {
        var response = WorldProgressService.GetProgress(source);
        return EndpointResponseFactory.Success(response);
    }

    public static object WorldFile(RestRequestArgs _)
        => WorldFile(WorldFileService);

    public static RestObject WorldFile(IWorldFileService service)
    {
        try
        {
            PluginLogger.Info("世界文件正在读取......");
            var result = service.GetWorldFile();
            PluginLogger.Info($"世界文件读取完成，文件名：{result.FileName}。");
            return EndpointResponseFactory.Success(new WorldFileResponse(
                result.FileName,
                Convert.ToBase64String(result.Content)));
        }
        catch (Exception ex)
        {
            PluginLogger.Error($"世界文件读取失败，原因：{ex.Message}");
            return EndpointResponseFactory.Error("500", ErrorCodes.WorldFileReadFailed, ex.Message);
        }
    }
}
