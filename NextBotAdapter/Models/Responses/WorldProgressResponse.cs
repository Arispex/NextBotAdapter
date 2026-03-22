using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace NextBotAdapter.Models.Responses;

public sealed record WorldProgressResponse(
    [property: JsonProperty("kingSlime"), JsonPropertyName("kingSlime")] bool KingSlime,
    [property: JsonProperty("eyeOfCthulhu"), JsonPropertyName("eyeOfCthulhu")] bool EyeOfCthulhu,
    [property: JsonProperty("eaterOfWorldsOrBrainOfCthulhu"), JsonPropertyName("eaterOfWorldsOrBrainOfCthulhu")] bool EaterOfWorldsOrBrainOfCthulhu,
    [property: JsonProperty("queenBee"), JsonPropertyName("queenBee")] bool QueenBee,
    [property: JsonProperty("skeletron"), JsonPropertyName("skeletron")] bool Skeletron,
    [property: JsonProperty("deerclops"), JsonPropertyName("deerclops")] bool Deerclops,
    [property: JsonProperty("wallOfFlesh"), JsonPropertyName("wallOfFlesh")] bool WallOfFlesh,
    [property: JsonProperty("queenSlime"), JsonPropertyName("queenSlime")] bool QueenSlime,
    [property: JsonProperty("theTwins"), JsonPropertyName("theTwins")] bool TheTwins,
    [property: JsonProperty("theDestroyer"), JsonPropertyName("theDestroyer")] bool TheDestroyer,
    [property: JsonProperty("skeletronPrime"), JsonPropertyName("skeletronPrime")] bool SkeletronPrime,
    [property: JsonProperty("plantera"), JsonPropertyName("plantera")] bool Plantera,
    [property: JsonProperty("golem"), JsonPropertyName("golem")] bool Golem,
    [property: JsonProperty("dukeFishron"), JsonPropertyName("dukeFishron")] bool DukeFishron,
    [property: JsonProperty("empressOfLight"), JsonPropertyName("empressOfLight")] bool EmpressOfLight,
    [property: JsonProperty("lunaticCultist"), JsonPropertyName("lunaticCultist")] bool LunaticCultist,
    [property: JsonProperty("solarPillar"), JsonPropertyName("solarPillar")] bool SolarPillar,
    [property: JsonProperty("nebulaPillar"), JsonPropertyName("nebulaPillar")] bool NebulaPillar,
    [property: JsonProperty("vortexPillar"), JsonPropertyName("vortexPillar")] bool VortexPillar,
    [property: JsonProperty("stardustPillar"), JsonPropertyName("stardustPillar")] bool StardustPillar,
    [property: JsonProperty("moonLord"), JsonPropertyName("moonLord")] bool MoonLord);
