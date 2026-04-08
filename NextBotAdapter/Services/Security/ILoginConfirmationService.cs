namespace NextBotAdapter.Services;

public interface ILoginConfirmationService
{
    void RecordBlockedLogin(string username, string? detectedUuid, string? detectedIp);

    bool TryApproveNextLogin(string username, out string? error);

    bool TryRejectPendingLogin(string username, out string? error);

    bool ConsumeApproval(string username, string? currentUuid, string? currentIp);

    bool HasActiveApproval(string username);

    bool HasActivePending(string username);
}
