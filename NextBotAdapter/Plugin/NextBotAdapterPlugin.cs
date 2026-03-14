using System;
using System.Diagnostics.CodeAnalysis;
using NextBotAdapter.Rest;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;

namespace NextBotAdapter.Plugin;

[ExcludeFromCodeCoverage]
[ApiVersion(2, 1)]
public sealed class NextBotAdapterPlugin(Main game) : TerrariaPlugin(game)
{
    public override string Author => "Arispex";

    public override string Description => "Provides NextBot with TShock server information.";

    public override string Name => "NextBotAdapter";

    public override Version Version => new(1, 0, 0);

    public override void Initialize()
    {
        EndpointRegistrar.Register(TShock.RestApi);
    }
}
