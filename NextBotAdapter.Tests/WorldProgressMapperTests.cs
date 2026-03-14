using NextBotAdapter.Models.Responses;
using NextBotAdapter.Services;

namespace NextBotAdapter.Tests;

public sealed class WorldProgressMapperTests
{
    [Fact]
    public void CreateResponse_ShouldMapSnapshotToStableEnglishKeys()
    {
        var snapshot = new WorldProgressSnapshot(
            true,
            false,
            true,
            false,
            true,
            false,
            true,
            false,
            true,
            false,
            true,
            false,
            true,
            false,
            true,
            false,
            true,
            false,
            true,
            false,
            true);

        var response = WorldProgressMapper.CreateResponse(snapshot);

        Assert.True(response.KingSlime);
        Assert.False(response.EyeOfCthulhu);
        Assert.True(response.EaterOfWorldsOrBrainOfCthulhu);
        Assert.False(response.QueenBee);
        Assert.True(response.Skeletron);
        Assert.False(response.Deerclops);
        Assert.True(response.WallOfFlesh);
        Assert.False(response.QueenSlime);
        Assert.True(response.TheTwins);
        Assert.False(response.TheDestroyer);
        Assert.True(response.SkeletronPrime);
        Assert.False(response.Plantera);
        Assert.True(response.Golem);
        Assert.False(response.DukeFishron);
        Assert.True(response.EmpressOfLight);
        Assert.False(response.LunaticCultist);
        Assert.True(response.SolarPillar);
        Assert.False(response.NebulaPillar);
        Assert.True(response.VortexPillar);
        Assert.False(response.StardustPillar);
        Assert.True(response.MoonLord);
    }
}
