using NextBotAdapter.Models.Responses;

namespace NextBotAdapter.Services;

public static class UserInfoMapper
{
    public static UserInfoResponse CreateResponse(object playerData)
    {
        return new UserInfoResponse(
            ReadInt(playerData, "health"),
            ReadInt(playerData, "maxHealth"),
            ReadInt(playerData, "mana"),
            ReadInt(playerData, "maxMana"),
            ReadInt(playerData, "questsCompleted"),
            PlayerStatisticsReader.ReadDeaths(playerData, "deathsPVE"),
            PlayerStatisticsReader.ReadDeaths(playerData, "deathsPVP"));
    }

    private static int ReadInt(object source, string fieldName)
    {
        return PlayerStatisticsReader.ReadDeaths(source, fieldName);
    }
}
