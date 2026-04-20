using NextBotAdapter.Services;

namespace NextBotAdapter.Tests;

internal sealed class FakeTShockUserBanService : ITShockUserBanService
{
    public List<(string User, string Reason)> BanCalls { get; } = [];
    public List<string> UnbanCalls { get; } = [];

    public void BanAccountIfRegistered(string username, string reason)
        => BanCalls.Add((username, reason));

    public void UnbanAccountIfBanned(string username)
        => UnbanCalls.Add(username);
}
