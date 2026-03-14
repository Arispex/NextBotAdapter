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
            new SecureRestCommand(Infrastructure.EndpointRoutes.WorldProgress, WorldEndpoints.Progress, Infrastructure.Permissions.WorldProgress)
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
