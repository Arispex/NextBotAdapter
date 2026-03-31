using NextBotAdapter.Rest;
using NextBotAdapter.Services;
using Rests;

namespace NextBotAdapter.Tests;

public sealed class SecurityEndpointsTests
{
    [Fact]
    public void ConfirmLogin_ShouldReturnBadRequestWhenUserIsBlank()
    {
        var result = Assert.IsType<RestObject>(
            SecurityEndpoints.ConfirmLogin(" ", new FakeLoginConfirmationService(), new FakeGateway()));

        Assert.Equal("400", result.Status);
        Assert.Equal("Missing required route parameter 'user'.", result.Error);
    }

    [Fact]
    public void ConfirmLogin_ShouldReturnBadRequestWhenUserNotFound()
    {
        var result = Assert.IsType<RestObject>(
            SecurityEndpoints.ConfirmLogin("alice", new FakeLoginConfirmationService(), new FakeGateway(accountExists: false)));

        Assert.Equal("400", result.Status);
        Assert.Equal("User was not found.", result.Error);
    }

    [Fact]
    public void ConfirmLogin_ShouldReturnBadRequestWhenNoPendingLogin()
    {
        var service = new FakeLoginConfirmationService(approveSucceeds: false, approveError: "No pending login request found for user 'alice'.");

        var result = Assert.IsType<RestObject>(
            SecurityEndpoints.ConfirmLogin("alice", service, new FakeGateway()));

        Assert.Equal("400", result.Status);
        Assert.Contains("alice", result.Error ?? "");
    }

    [Fact]
    public void ConfirmLogin_ShouldReturnOkWhenApproveSucceeds()
    {
        var service = new FakeLoginConfirmationService(approveSucceeds: true);

        var result = Assert.IsType<RestObject>(
            SecurityEndpoints.ConfirmLogin("alice", service, new FakeGateway()));

        Assert.Equal("200", result.Status);
        Assert.Contains("alice", result["response"]?.ToString());
    }

    private sealed class FakeLoginConfirmationService(bool approveSucceeds = true, string? approveError = null) : ILoginConfirmationService
    {
        public void RecordBlockedLogin(string username, string? detectedUuid, string? detectedIp) { }

        public bool TryApproveNextLogin(string username, out string? error)
        {
            error = approveSucceeds ? null : approveError;
            return approveSucceeds;
        }

        public bool ConsumeApproval(string username, string? currentUuid, string? currentIp) => false;

        public bool HasActiveApproval(string username) => false;
    }

    private sealed class FakeGateway(bool accountExists = true) : IUserDataGateway
    {
        public bool TryGetUserAccountId(string user, out int accountId)
        {
            accountId = 1;
            return accountExists;
        }

        public bool TryGetPlayerData(int accountId, out object playerData)
        {
            playerData = null!;
            return false;
        }

        public IReadOnlyList<(int AccountId, string Username)> GetAllUserAccounts() => [];
    }
}
