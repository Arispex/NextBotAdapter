namespace NextBotAdapter.Models;

public sealed record NextBotAdapterConfig(WhitelistSettings Whitelist)
{
    public static NextBotAdapterConfig Default { get; } = new(WhitelistSettings.Default);
}
