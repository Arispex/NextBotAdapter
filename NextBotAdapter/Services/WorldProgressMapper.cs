using NextBotAdapter.Models.Responses;

namespace NextBotAdapter.Services;

public static class WorldProgressMapper
{
    public static WorldProgressResponse CreateResponse(WorldProgressSnapshot snapshot)
        => new(
            snapshot.KingSlime,
            snapshot.EyeOfCthulhu,
            snapshot.EaterOfWorldsOrBrainOfCthulhu,
            snapshot.QueenBee,
            snapshot.Skeletron,
            snapshot.Deerclops,
            snapshot.WallOfFlesh,
            snapshot.QueenSlime,
            snapshot.TheTwins,
            snapshot.TheDestroyer,
            snapshot.SkeletronPrime,
            snapshot.Plantera,
            snapshot.Golem,
            snapshot.DukeFishron,
            snapshot.EmpressOfLight,
            snapshot.LunaticCultist,
            snapshot.SolarPillar,
            snapshot.NebulaPillar,
            snapshot.VortexPillar,
            snapshot.StardustPillar,
            snapshot.MoonLord);
}
