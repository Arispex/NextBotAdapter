using Rests;

namespace NextBotAdapter.Rest;

public static class EndpointRegistrar
{
    public static IReadOnlyList<RestCommand> CreateCommands()
    {
        return
        [
            new SecureRestCommand(Infrastructure.EndpointRoutes.UserInventory, UserEndpoints.Inventory, Infrastructure.Permissions.UserInventory),
            new SecureRestCommand(Infrastructure.EndpointRoutes.UserStats, UserEndpoints.Stats, Infrastructure.Permissions.UserStats),
            new SecureRestCommand(Infrastructure.EndpointRoutes.WorldProgress, WorldEndpoints.Progress, Infrastructure.Permissions.WorldProgress),
            new SecureRestCommand(Infrastructure.EndpointRoutes.WorldMapImage, MapEndpoints.Image, Infrastructure.Permissions.WorldMapImage),
            new SecureRestCommand(Infrastructure.EndpointRoutes.WorldFile, WorldEndpoints.WorldFile, Infrastructure.Permissions.WorldFile),
            new SecureRestCommand(Infrastructure.EndpointRoutes.WorldMapFile, WorldEndpoints.MapFile, Infrastructure.Permissions.WorldMapFile),
            new SecureRestCommand(Infrastructure.EndpointRoutes.Whitelist, WhitelistEndpoints.List, Infrastructure.Permissions.WhitelistView),
            new SecureRestCommand(Infrastructure.EndpointRoutes.WhitelistAddUser, WhitelistEndpoints.Add, Infrastructure.Permissions.WhitelistAdd),
            new SecureRestCommand(Infrastructure.EndpointRoutes.WhitelistRemoveUser, WhitelistEndpoints.Remove, Infrastructure.Permissions.WhitelistRemove),
            new SecureRestCommand(Infrastructure.EndpointRoutes.ConfigReload, ConfigEndpoints.Reload, Infrastructure.Permissions.ConfigReload),
            new SecureRestCommand(Infrastructure.EndpointRoutes.ConfigRead, ConfigEndpoints.Read, Infrastructure.Permissions.ConfigRead),
            new SecureRestCommand(Infrastructure.EndpointRoutes.ConfigUpdate, ConfigEndpoints.Update, Infrastructure.Permissions.ConfigUpdate),
            new SecureRestCommand(Infrastructure.EndpointRoutes.LeaderboardDeaths, LeaderboardEndpoints.Deaths, Infrastructure.Permissions.LeaderboardDeaths),
            new SecureRestCommand(Infrastructure.EndpointRoutes.LeaderboardFishingQuests, LeaderboardEndpoints.FishingQuests, Infrastructure.Permissions.LeaderboardFishingQuests),
            new SecureRestCommand(Infrastructure.EndpointRoutes.LeaderboardOnlineTime, LeaderboardEndpoints.OnlineTime, Infrastructure.Permissions.LeaderboardOnlineTime),
            new SecureRestCommand(Infrastructure.EndpointRoutes.SecurityConfirmLogin, SecurityEndpoints.ConfirmLogin, Infrastructure.Permissions.SecurityConfirmLogin)
        ];
    }

    public static void Register(Rests.Rest restApi)
    {
        foreach (var command in CreateCommands())
        {
            restApi.Register(command);
        }
    }
}
