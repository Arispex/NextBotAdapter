using Newtonsoft.Json;

namespace NextBotAdapter.Models;

public sealed record NextBotAdapterConfig(
    [property: JsonProperty("whitelist")] WhitelistSettings Whitelist,
    [property: JsonProperty("loginConfirmation")] LoginConfirmationSettings? LoginConfirmation = null)
{
    public static NextBotAdapterConfig Default { get; } = new(WhitelistSettings.Default, LoginConfirmationSettings.Default);

    public NextBotAdapterConfig WithDefaults() => new(
        Whitelist ?? WhitelistSettings.Default,
        LoginConfirmation ?? LoginConfirmationSettings.Default);
}
