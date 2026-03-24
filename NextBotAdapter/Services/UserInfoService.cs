using NextBotAdapter.Models.Responses;
using TShockAPI;

namespace NextBotAdapter.Services;

public static class UserInfoService
{
    public static bool TryGetUserInfo(string user, out UserInfoResponse response, out string? error)
        => TryGetUserInfo(user, UserDataService.Default, out response, out error);

    public static bool TryGetUserInfo(string user, IPlayerDataAccessor accessor, out UserInfoResponse response, out string? error)
    {
        response = new UserInfoResponse(0, 0, 0, 0, 0, 0, 0);
        error = null;

        if (!accessor.TryGetPlayerData(user, out var data, out error))
        {
            return false;
        }

        response = UserInfoMapper.CreateResponse(data);
        return true;
    }
}
