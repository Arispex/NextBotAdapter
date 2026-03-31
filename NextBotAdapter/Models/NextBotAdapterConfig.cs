using System.Text.Json.Serialization;

namespace NextBotAdapter.Models;

public sealed record NextBotAdapterConfig(
    WhitelistSettings Whitelist,
    [property: JsonPropertyName("loginConfirmation")] LoginConfirmationSettings? LoginConfirmation = null)
{
    public static NextBotAdapterConfig Default { get; } = new(WhitelistSettings.Default, LoginConfirmationSettings.Default);

    public NextBotAdapterConfig WithDefaults() => new(
        Whitelist ?? WhitelistSettings.Default,
        LoginConfirmation ?? LoginConfirmationSettings.Default);
}
