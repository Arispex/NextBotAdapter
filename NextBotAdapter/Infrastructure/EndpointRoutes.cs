namespace NextBotAdapter.Infrastructure;

public static class EndpointRoutes
{
    public const string UserInventory = "/nextbot/users/{user}/inventory";
    public const string UserStats = "/nextbot/users/{user}/stats";
    public const string WorldProgress = "/nextbot/world/progress";
    public const string Whitelist = "/nextbot/whitelist";
    public const string WhitelistAddUser = "/nextbot/whitelist/add/{user}";
    public const string WhitelistRemoveUser = "/nextbot/whitelist/remove/{user}";
}
