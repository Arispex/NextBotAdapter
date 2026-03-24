using NextBotAdapter.Infrastructure;
using NextBotAdapter.Services;
using Rests;

namespace NextBotAdapter.Rest;

public static class WorldEndpoints
{
    public static IWorldFileService WorldFileService { get; set; } = null!;
    public static IMapFileService MapFileService { get; set; } = null!;

    public static object Progress(RestRequestArgs _)
        => Progress(WorldProgressService.DefaultSource);

    public static RestObject Progress(IWorldProgressSource source)
    {
        var r = WorldProgressService.GetProgress(source);
        return new RestObject("200")
        {
            { "kingSlime", r.KingSlime },
            { "eyeOfCthulhu", r.EyeOfCthulhu },
            { "eaterOfWorldsOrBrainOfCthulhu", r.EaterOfWorldsOrBrainOfCthulhu },
            { "queenBee", r.QueenBee },
            { "skeletron", r.Skeletron },
            { "deerclops", r.Deerclops },
            { "wallOfFlesh", r.WallOfFlesh },
            { "queenSlime", r.QueenSlime },
            { "theTwins", r.TheTwins },
            { "theDestroyer", r.TheDestroyer },
            { "skeletronPrime", r.SkeletronPrime },
            { "plantera", r.Plantera },
            { "golem", r.Golem },
            { "dukeFishron", r.DukeFishron },
            { "empressOfLight", r.EmpressOfLight },
            { "lunaticCultist", r.LunaticCultist },
            { "solarPillar", r.SolarPillar },
            { "nebulaPillar", r.NebulaPillar },
            { "vortexPillar", r.VortexPillar },
            { "stardustPillar", r.StardustPillar },
            { "moonLord", r.MoonLord }
        };
    }

    public static object WorldFile(RestRequestArgs _)
        => WorldFile(WorldFileService);

    public static RestObject WorldFile(IWorldFileService service)
    {
        try
        {
            PluginLogger.Info("开始读取世界文件。");
            var result = service.GetWorldFile();
            PluginLogger.Info($"世界文件读取完成，文件名：{result.FileName}。");
            return new RestObject("200")
            {
                { "fileName", result.FileName },
                { "base64", Convert.ToBase64String(result.Content) }
            };
        }
        catch (Exception ex)
        {
            PluginLogger.Error($"世界文件读取失败，原因：{ex.Message}");
            return EndpointResponseFactory.Error(ex.Message, "500");
        }
    }

    public static object MapFile(RestRequestArgs _)
        => MapFile(MapFileService);

    public static RestObject MapFile(IMapFileService service)
    {
        try
        {
            PluginLogger.Info("开始生成地图文件。");
            var result = service.GetMapFile();
            PluginLogger.Info($"地图文件生成完成，文件名：{result.FileName}。");
            return new RestObject("200")
            {
                { "fileName", result.FileName },
                { "base64", Convert.ToBase64String(result.Content) }
            };
        }
        catch (Exception ex)
        {
            PluginLogger.Error($"地图文件生成失败，原因：{ex.Message}");
            return EndpointResponseFactory.Error(ex.Message, "500");
        }
    }
}
