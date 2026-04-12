using NextBotAdapter.Models.Responses;

namespace NextBotAdapter.Services;

public sealed record SyncResult(int Added, int Removed, int Skipped);

public sealed class NextBotSyncService(IWhitelistService whitelistService, IBlacklistService blacklistService)
{
    public SyncResult SyncWhitelist(IReadOnlyList<NextBotUserEntry> users)
    {
        var expected = new HashSet<string>(
            users.Where(u => !u.IsBanned).Select(u => u.Name),
            StringComparer.OrdinalIgnoreCase);

        var current = whitelistService.GetAll();
        int added = 0, removed = 0, skipped = 0;

        foreach (var user in current)
        {
            if (!expected.Contains(user))
            {
                if (whitelistService.TryRemove(user, out _)) removed++;
                else skipped++;
            }
        }

        var currentSet = new HashSet<string>(current, StringComparer.OrdinalIgnoreCase);
        foreach (var name in expected)
        {
            if (!currentSet.Contains(name))
            {
                if (whitelistService.TryAdd(name, out _)) added++;
                else skipped++;
            }
        }

        return new SyncResult(added, removed, skipped);
    }

    public SyncResult SyncBlacklist(IReadOnlyList<NextBotUserEntry> users)
    {
        var expected = users.Where(u => u.IsBanned)
            .GroupBy(u => u.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Last().BanReason, StringComparer.OrdinalIgnoreCase);

        var current = blacklistService.GetAll();
        int added = 0, removed = 0, skipped = 0;

        foreach (var entry in current)
        {
            if (!expected.ContainsKey(entry.Username))
            {
                if (blacklistService.TryRemove(entry.Username, out _)) removed++;
                else skipped++;
            }
        }

        var currentSet = new HashSet<string>(
            current.Select(e => e.Username), StringComparer.OrdinalIgnoreCase);
        foreach (var (name, reason) in expected)
        {
            if (!currentSet.Contains(name))
            {
                if (blacklistService.TryAdd(name, reason, out _)) added++;
                else skipped++;
            }
        }

        return new SyncResult(added, removed, skipped);
    }
}
