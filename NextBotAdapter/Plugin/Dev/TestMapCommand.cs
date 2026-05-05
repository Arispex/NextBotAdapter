// TEMP: dev-only command, remove before final release.
// All test-output dump logic lives in this file so it can be deleted in one PR.
// To remove: delete this file and the two TestMapCommand.Register/Unregister calls in NextBotAdapterPlugin
// plus the Permissions.TestMap constant.
//
// /nbtestmap           - dump current world map to disk
// /nbtestmap <player>  - dump player-perspective map (using exploration bitmap) to disk
//
// Output: tshock/NextBotAdapter/test-output/map-{world|player}-{yyyyMMddHHmmss}.png
using System.Diagnostics.CodeAnalysis;
using NextBotAdapter.Rest;
using NextBotAdapter.Services;
using TShockAPI;

namespace NextBotAdapter.Plugin.Dev;

[ExcludeFromCodeCoverage]
internal static class TestMapCommand
{
    private const string CommandName = "nbtestmap";

    public static void Register()
    {
        Commands.ChatCommands.Add(new Command(Infrastructure.Permissions.TestMap, Execute, CommandName)
        {
            HelpText = "TEMP: dump current map (or player-view map) to tshock/NextBotAdapter/test-output/. Usage: /nbtestmap [player]"
        });
    }

    public static void Unregister()
    {
        Commands.ChatCommands.RemoveAll(c => c.CommandDelegate == Execute);
    }

    private static void Execute(CommandArgs args)
    {
        try
        {
            var outputDir = Path.Combine(TShock.SavePath, "NextBotAdapter", "test-output");
            if (!Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            string fileName;
            byte[] content;
            string label;

            if (args.Parameters.Count == 0)
            {
                if (MapEndpoints.Service is null)
                {
                    args.Player.SendErrorMessage("MapImageService is not configured.");
                    return;
                }

                var result = MapEndpoints.Service.Generate();
                fileName = $"map-world-{DateTimeOffset.Now:yyyyMMdd-HHmmss}.png";
                content = result.Content;
                label = "world";
            }
            else
            {
                var playerName = args.Parameters[0].Trim();
                if (string.IsNullOrEmpty(playerName))
                {
                    args.Player.SendErrorMessage("Player name cannot be empty.");
                    return;
                }

                if (MapEndpoints.PlayerService is null
                    || MapEndpoints.ExplorationTracker is null
                    || MapEndpoints.AccountLookup is null)
                {
                    args.Player.SendErrorMessage("Player exploration service is not configured.");
                    return;
                }

                if (!MapEndpoints.AccountLookup.TryGetAccountUuid(playerName, out var accountUuid))
                {
                    args.Player.SendErrorMessage("User was not found.");
                    return;
                }

                var bitmap = string.IsNullOrEmpty(accountUuid)
                    ? null
                    : MapEndpoints.ExplorationTracker.GetBitmap(accountUuid);

                var result = bitmap is null
                    ? MapEndpoints.PlayerService.GenerateBlank(playerName)
                    : MapEndpoints.PlayerService.Generate(playerName, bitmap);

                fileName = $"map-{playerName}-{DateTimeOffset.Now:yyyyMMdd-HHmmss}.png";
                content = result.Content;
                label = playerName;
            }

            var fullPath = Path.Combine(outputDir, fileName);
            File.WriteAllBytes(fullPath, content);
            args.Player.SendSuccessMessage($"Map dumped: {fullPath}");
            PluginLogger.Info($"测试地图已写入磁盘，target={label}，path={fullPath}");
        }
        catch (Exception ex)
        {
            args.Player.SendErrorMessage($"Failed to dump map: {ex.Message}");
            PluginLogger.Warn($"测试地图写入失败：{ex.Message}");
        }
    }
}
