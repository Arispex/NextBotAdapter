using System;
using System.Linq;
using TShockAPI;

namespace NextBotAdapter.Services;

public interface ITShockUserBanService
{
    void BanAccountIfRegistered(string username, string reason);
    void UnbanAccountIfBanned(string username);
}

public sealed class TShockUserBanService : ITShockUserBanService
{
    private const string BanningUser = "NextBotAdapter";
    private const string AccountIdentifierPrefix = "acc:";

    public void BanAccountIfRegistered(string username, string reason)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return;
        }

        try
        {
            var account = TShock.UserAccounts.GetUserAccountByName(username);
            if (account is null)
            {
                PluginLogger.Info($"跳过游戏内封禁：用户名 {username} 未注册 TShock 账号");
                return;
            }

            var identifier = AccountIdentifierPrefix + account.Name;
            var result = TShock.Bans.InsertBan(
                identifier,
                reason,
                BanningUser,
                DateTime.UtcNow,
                DateTime.MaxValue);

            if (result?.Ban is not null)
            {
                PluginLogger.Info($"TShock 账号 {account.Name} 已被游戏内封禁，ticket={result.Ban.TicketNumber}，原因：{reason}");
            }
            else
            {
                PluginLogger.Warn($"TShock 账号 {account.Name} 游戏内封禁失败，原因：{result?.Message ?? "未知错误"}");
            }
        }
        catch (Exception ex)
        {
            PluginLogger.Warn($"TShock 账号 {username} 游戏内封禁异常，原因：{ex.Message}");
        }
    }

    public void UnbanAccountIfBanned(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return;
        }

        try
        {
            var account = TShock.UserAccounts.GetUserAccountByName(username);
            if (account is null)
            {
                return;
            }

            var identifier = AccountIdentifierPrefix + account.Name;
            var bans = TShock.Bans.RetrieveBansByIdentifier(identifier).ToList();
            if (bans.Count == 0)
            {
                return;
            }

            var removed = 0;
            foreach (var ban in bans)
            {
                if (TShock.Bans.RemoveBan(ban.TicketNumber))
                {
                    removed++;
                }
            }

            PluginLogger.Info($"TShock 账号 {account.Name} 游戏内封禁已解除，共处理 {removed}/{bans.Count} 条 ban 记录");
        }
        catch (Exception ex)
        {
            PluginLogger.Warn($"TShock 账号 {username} 解除游戏内封禁异常，原因：{ex.Message}");
        }
    }
}
