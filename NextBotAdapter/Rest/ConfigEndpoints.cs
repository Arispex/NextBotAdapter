using Newtonsoft.Json.Linq;
using NextBotAdapter.Infrastructure;
using NextBotAdapter.Services;
using Rests;

namespace NextBotAdapter.Rest;

public static class ConfigEndpoints
{
    public static IConfigurationReloadService ReloadService { get; set; } = null!;
    public static WhitelistConfigService ConfigService { get; set; } = null!;

    public static object Reload(RestRequestArgs _)
        => Reload(ReloadService);

    public static object Reload(IConfigurationReloadService service)
    {
        try
        {
            service.ReloadAll();
            return new RestObject("200") { { "response", "Configuration reloaded successfully." } };
        }
        catch (Exception ex)
        {
            PluginLogger.Error($"插件配置重新加载失败，原因：{ex.Message}");
            return EndpointResponseFactory.Error(ex.Message, "500");
        }
    }

    public static object Read(RestRequestArgs _)
        => Read(ConfigService);

    public static object Read(WhitelistConfigService service)
    {
        try
        {
            var raw = service.ReadConfigRaw();
            var obj = JObject.Parse(raw);
            var result = new RestObject("200");
            foreach (var (key, value) in obj)
            {
                result[key] = value;
            }
            return result;
        }
        catch (Exception ex)
        {
            return EndpointResponseFactory.Error(ex.Message, "500");
        }
    }

    public static object Update(RestRequestArgs args)
        => Update(args.Parameters, ConfigService, ReloadService);

    public static object Update(
        EscapedParameterCollection? parameters,
        WhitelistConfigService configService,
        IConfigurationReloadService reloadService)
    {
        var fields = new List<KeyValuePair<string, string>>();
        if (parameters is not null)
        {
            foreach (var param in parameters)
            {
                if (param.Name is "token" or "tokenHash") continue;
                fields.Add(new KeyValuePair<string, string>(param.Name, param.Value));
            }
        }

        if (fields.Count == 0)
        {
            return EndpointResponseFactory.Error("No fields specified for update.");
        }

        if (!configService.TryUpdateConfig(fields, out var error))
        {
            return EndpointResponseFactory.Error(error!);
        }

        try
        {
            reloadService.ReloadAll();
        }
        catch (Exception ex)
        {
            PluginLogger.Warn($"配置更新后热重载失败，原因：{ex.Message}");
        }

        return new RestObject("200") { { "response", $"Updated {fields.Count} field(s) successfully." } };
    }
}
