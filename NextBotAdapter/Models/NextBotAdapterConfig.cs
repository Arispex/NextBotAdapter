using Newtonsoft.Json;

namespace NextBotAdapter.Models;

public sealed record NextBotAdapterConfig(
    [property: JsonProperty("nextbot")] NextBotSettings NextBot,
    [property: JsonProperty("whitelist")] WhitelistSettings Whitelist,
    [property: JsonProperty("blacklist")] BlacklistSettings? Blacklist = null,
    [property: JsonProperty("sync")] SyncSettings? Sync = null,
    [property: JsonProperty("loginConfirmation")] LoginConfirmationSettings? LoginConfirmation = null)
{
    public static NextBotAdapterConfig Default { get; } = new(NextBotSettings.Default, WhitelistSettings.Default, BlacklistSettings.Default, SyncSettings.Default, LoginConfirmationSettings.Default);

    public NextBotAdapterConfig WithDefaults() => new(
        NextBot ?? NextBotSettings.Default,
        Whitelist ?? WhitelistSettings.Default,
        Blacklist ?? BlacklistSettings.Default,
        Sync ?? SyncSettings.Default,
        LoginConfirmation ?? LoginConfirmationSettings.Default);
}
