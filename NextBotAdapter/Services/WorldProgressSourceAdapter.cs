using System.Diagnostics.CodeAnalysis;
using NextBotAdapter.Models.Responses;

namespace NextBotAdapter.Services;

[ExcludeFromCodeCoverage]
public sealed class WorldProgressSourceAdapter : IWorldProgressSource
{
    public WorldProgressSnapshot GetSnapshot()
    {
        return new WorldProgressSnapshot(
            Terraria.NPC.downedSlimeKing,
            Terraria.NPC.downedBoss1,
            Terraria.NPC.downedBoss2,
            Terraria.NPC.downedQueenBee,
            Terraria.NPC.downedBoss3,
            Terraria.NPC.downedDeerclops,
            Terraria.Main.hardMode,
            Terraria.NPC.downedQueenSlime,
            Terraria.NPC.downedMechBoss2,
            Terraria.NPC.downedMechBoss1,
            Terraria.NPC.downedMechBoss3,
            Terraria.NPC.downedPlantBoss,
            Terraria.NPC.downedGolemBoss,
            Terraria.NPC.downedFishron,
            Terraria.NPC.downedEmpressOfLight,
            Terraria.NPC.downedAncientCultist,
            Terraria.NPC.downedTowerSolar,
            Terraria.NPC.downedTowerNebula,
            Terraria.NPC.downedTowerVortex,
            Terraria.NPC.downedTowerStardust,
            Terraria.NPC.downedMoonlord);
    }
}
