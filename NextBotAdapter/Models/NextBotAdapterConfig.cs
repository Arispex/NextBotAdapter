using Newtonsoft.Json;

namespace NextBotAdapter.Models;

public sealed record NextBotAdapterConfig(
    [property: JsonProperty("nextbot", Order = 2)] NextBotSettings NextBot,
    [property: JsonProperty("whitelist", Order = 3)] WhitelistSettings Whitelist,
    [property: JsonProperty("blacklist", Order = 4)] BlacklistSettings? Blacklist = null,
    [property: JsonProperty("sync", Order = 5)] SyncSettings? Sync = null,
    [property: JsonProperty("loginConfirmation", Order = 6)] LoginConfirmationSettings? LoginConfirmation = null,
    [property: JsonProperty("playerEvents", Order = 7)] PlayerEventsSettings? PlayerEvents = null,
    [property: JsonProperty("serverName", Order = 1)] string ServerName = "我的服务器")
{
    public static NextBotAdapterConfig Default { get; } = new(NextBotSettings.Default, WhitelistSettings.Default, BlacklistSettings.Default, SyncSettings.Default, LoginConfirmationSettings.Default, PlayerEventsSettings.Default, "我的服务器");

    public NextBotAdapterConfig WithDefaults() => new(
        NextBot ?? NextBotSettings.Default,
        Whitelist ?? WhitelistSettings.Default,
        Blacklist ?? BlacklistSettings.Default,
        Sync ?? SyncSettings.Default,
        LoginConfirmation ?? LoginConfirmationSettings.Default,
        PlayerEvents ?? PlayerEventsSettings.Default,
        ServerName ?? "我的服务器");
}
