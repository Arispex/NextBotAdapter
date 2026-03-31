using NextBotAdapter.Services;

namespace NextBotAdapter.Tests;

public sealed class LoginConfirmationServiceTests
{
    [Fact]
    public void ConsumeApproval_ShouldReturnFalseWhenNoApprovalExists()
    {
        var service = new LoginConfirmationService();

        Assert.False(service.ConsumeApproval("alice", null, null));
    }

    [Fact]
    public void TryApproveNextLogin_ShouldFailWhenNoPendingLoginExists()
    {
        var service = new LoginConfirmationService();

        var result = service.TryApproveNextLogin("alice", out var error);

        Assert.False(result);
        Assert.NotNull(error);
        Assert.Contains("alice", error);
    }

    [Fact]
    public void TryApproveNextLogin_ShouldSucceedAfterRecordBlockedLogin()
    {
        var service = new LoginConfirmationService();
        service.RecordBlockedLogin("alice", "new-uuid", "1.2.3.4");

        var result = service.TryApproveNextLogin("alice", out var error);

        Assert.True(result);
        Assert.Null(error);
    }

    [Fact]
    public void ConsumeApproval_ShouldReturnTrueWhenUuidAndIpMatch()
    {
        var service = new LoginConfirmationService();
        service.RecordBlockedLogin("alice", "new-uuid", "1.2.3.4");
        service.TryApproveNextLogin("alice", out _);

        Assert.True(service.ConsumeApproval("alice", "new-uuid", "1.2.3.4"));
    }

    [Fact]
    public void ConsumeApproval_ShouldReturnFalseWhenUuidDoesNotMatch()
    {
        var service = new LoginConfirmationService();
        service.RecordBlockedLogin("alice", "new-uuid", null);
        service.TryApproveNextLogin("alice", out _);

        Assert.False(service.ConsumeApproval("alice", "other-uuid", null));
    }

    [Fact]
    public void ConsumeApproval_ShouldReturnFalseWhenIpDoesNotMatch()
    {
        var service = new LoginConfirmationService();
        service.RecordBlockedLogin("alice", null, "1.2.3.4");
        service.TryApproveNextLogin("alice", out _);

        Assert.False(service.ConsumeApproval("alice", null, "9.9.9.9"));
    }

    [Fact]
    public void ConsumeApproval_ShouldBeConsumedAfterFirstUse()
    {
        var service = new LoginConfirmationService();
        service.RecordBlockedLogin("alice", "uuid", "1.2.3.4");
        service.TryApproveNextLogin("alice", out _);
        service.ConsumeApproval("alice", "uuid", "1.2.3.4");

        Assert.False(service.ConsumeApproval("alice", "uuid", "1.2.3.4"));
    }

    [Fact]
    public void ConsumeApproval_ShouldNotBeConsumedOnMismatch()
    {
        var service = new LoginConfirmationService();
        service.RecordBlockedLogin("alice", "uuid", null);
        service.TryApproveNextLogin("alice", out _);
        service.ConsumeApproval("alice", "wrong-uuid", null);

        // Approval should still be valid after mismatch — device A can still use it
        Assert.True(service.ConsumeApproval("alice", "uuid", null));
    }

    [Fact]
    public void TryApproveNextLogin_ShouldFailWhenActiveApprovalAlreadyExists()
    {
        var service = new LoginConfirmationService();
        service.RecordBlockedLogin("alice", "uuid", null);
        service.TryApproveNextLogin("alice", out _);

        // Device B creates a new pending entry
        service.RecordBlockedLogin("alice", "other-uuid", null);

        // Calling confirm again should fail — active approval already exists
        var result = service.TryApproveNextLogin("alice", out var error);

        Assert.False(result);
        Assert.NotNull(error);
    }

    [Fact]
    public void TryApproveNextLogin_ShouldConsumeThePendingEntry()
    {
        var service = new LoginConfirmationService();
        service.RecordBlockedLogin("alice", "uuid", null);
        service.TryApproveNextLogin("alice", out _);

        var result = service.TryApproveNextLogin("alice", out _);

        Assert.False(result);
    }

    [Fact]
    public void RecordsAndApprovals_ShouldBeIndependentPerUser()
    {
        var service = new LoginConfirmationService();
        service.RecordBlockedLogin("alice", "uuid-a", null);
        service.RecordBlockedLogin("bob", "uuid-b", null);
        service.TryApproveNextLogin("alice", out _);

        Assert.True(service.ConsumeApproval("alice", "uuid-a", null));
        Assert.False(service.ConsumeApproval("bob", "uuid-b", null));
    }
}
