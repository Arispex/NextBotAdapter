using NextBotAdapter.Infrastructure;
using NextBotAdapter.Services;
using Rests;

namespace NextBotAdapter.Rest;

public static class ConfigEndpoints
{
    public static IConfigurationReloadService Service { get; set; } = null!;

    public static object Reload(RestRequestArgs _)
        => Reload(Service);

    public static object Reload(IConfigurationReloadService service)
    {
        try
        {
            service.ReloadAll();
            return EndpointResponseFactory.Success(new Dictionary<string, object?>
            {
                { "reloaded", true }
            });
        }
        catch (Exception ex)
        {
            PluginLogger.Error($"处理配置热重载请求失败，原因：{ex.Message}");
            return EndpointResponseFactory.Error("500", ErrorCodes.ConfigReloadFailed, ex.Message);
        }
    }
}
