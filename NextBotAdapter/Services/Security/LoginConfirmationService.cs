namespace NextBotAdapter.Services;

public sealed class LoginConfirmationService : ILoginConfirmationService
{
    private static readonly TimeSpan PendingDuration = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan ApprovalDuration = TimeSpan.FromMinutes(5);

    private sealed record PendingLogin(string? DetectedUuid, string? DetectedIp, DateTime ExpiresAt);
    private sealed record ApprovalEntry(string? ExpectedUuid, string? ExpectedIp, DateTime ExpiresAt);

    private readonly Dictionary<string, PendingLogin> _pendingLogins = new();
    private readonly Dictionary<string, ApprovalEntry> _approvals = new();
    private readonly object _lock = new();

    public void RecordBlockedLogin(string username, string? detectedUuid, string? detectedIp)
    {
        lock (_lock)
        {
            _pendingLogins[username] = new PendingLogin(detectedUuid, detectedIp, DateTime.UtcNow + PendingDuration);
        }
    }

    public bool TryApproveNextLogin(string username, out string? error)
    {
        lock (_lock)
        {
            if (_approvals.TryGetValue(username, out var existing) && DateTime.UtcNow <= existing.ExpiresAt)
            {
                error = $"An active approval already exists for user '{username}'.";
                return false;
            }

            if (!_pendingLogins.TryGetValue(username, out var pending) || DateTime.UtcNow > pending.ExpiresAt)
            {
                error = $"No pending login request found for user '{username}'.";
                return false;
            }

            _pendingLogins.Remove(username);
            _approvals[username] = new ApprovalEntry(pending.DetectedUuid, pending.DetectedIp, DateTime.UtcNow + ApprovalDuration);
        }

        PluginLogger.Info($"玩家 {username} 的登录二次确认已创建，有效期 5 分钟。");
        error = null;
        return true;
    }

    public bool HasActiveApproval(string username)
    {
        lock (_lock)
        {
            return _approvals.TryGetValue(username, out var approval) && DateTime.UtcNow <= approval.ExpiresAt;
        }
    }

    public bool HasActivePending(string username)
    {
        lock (_lock)
        {
            return _pendingLogins.TryGetValue(username, out var pending) && DateTime.UtcNow <= pending.ExpiresAt;
        }
    }

    public bool ConsumeApproval(string username, string? currentUuid, string? currentIp)
    {
        lock (_lock)
        {
            if (!_approvals.TryGetValue(username, out var approval))
            {
                return false;
            }

            if (DateTime.UtcNow > approval.ExpiresAt)
            {
                _approvals.Remove(username);
                return false;
            }

            if (approval.ExpectedUuid != null && approval.ExpectedUuid != currentUuid)
            {
                return false;
            }

            if (approval.ExpectedIp != null && approval.ExpectedIp != currentIp)
            {
                return false;
            }

            _approvals.Remove(username);
            return true;
        }
    }
}
