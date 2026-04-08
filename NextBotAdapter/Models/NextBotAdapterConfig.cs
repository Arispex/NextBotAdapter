using Newtonsoft.Json;

namespace NextBotAdapter.Models;

public sealed record NextBotAdapterConfig(
    [property: JsonProperty("nextbot")] NextBotSettings NextBot,
    [property: JsonProperty("whitelist")] WhitelistSettings Whitelist,
    [property: JsonProperty("loginConfirmation")] LoginConfirmationSettings? LoginConfirmation = null)
{
    public static NextBotAdapterConfig Default { get; } = new(NextBotSettings.Default, WhitelistSettings.Default, LoginConfirmationSettings.Default);

    public NextBotAdapterConfig WithDefaults() => new(
        NextBot ?? NextBotSettings.Default,
        Whitelist ?? WhitelistSettings.Default,
        LoginConfirmation ?? LoginConfirmationSettings.Default);
}
