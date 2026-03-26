namespace NextBotAdapter.Infrastructure;

public static class EndpointRoutes
{
    public const string UserInventory = "/nextbot/users/{user}/inventory";
    public const string UserStats = "/nextbot/users/{user}/stats";
    public const string WorldProgress = "/nextbot/world/progress";
    public const string WorldMapImage = "/nextbot/world/map-image";
    public const string WorldFile = "/nextbot/world/world-file";
    public const string WorldMapFile = "/nextbot/world/map-file";
    public const string Whitelist = "/nextbot/whitelist";
    public const string WhitelistAddUser = "/nextbot/whitelist/add/{user}";
    public const string WhitelistRemoveUser = "/nextbot/whitelist/remove/{user}";
    public const string ConfigReload = "/nextbot/config/reload";
    public const string LeaderboardDeaths = "/nextbot/leaderboards/deaths";
    public const string LeaderboardFishingQuests = "/nextbot/leaderboards/fishing-quests";
    public const string LeaderboardOnlineTime = "/nextbot/leaderboards/online-time";
}
