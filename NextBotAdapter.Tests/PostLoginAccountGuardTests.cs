using NextBotAdapter.Models;
using NextBotAdapter.Plugin;
using NextBotAdapter.Services;

namespace NextBotAdapter.Tests;

public sealed class PostLoginAccountGuardTests
{
    [Fact]
    public void Validate_ShouldReturnTrue_WhenServicesNull()
    {
        var (allowed, denialReason) = PostLoginAccountGuard.Validate("Alice", null, null);

        Assert.True(allowed);
        Assert.Null(denialReason);
    }

    [Fact]
    public void Validate_ShouldReturnTrue_WhenAccountNameEmpty()
    {
        var blacklist = new FakeBlacklistService(blacklisted: new[] { "Alice" });
        var whitelist = new FakeWhitelistService(allowed: Array.Empty<string>(), enabled: true);

        var (allowed, denialReason) = PostLoginAccountGuard.Validate(string.Empty, blacklist, whitelist);

        Assert.True(allowed);
        Assert.Null(denialReason);
    }

    [Fact]
    public void Validate_ShouldReturnTrue_WhenAccountAllowed()
    {
        var blacklist = new FakeBlacklistService(blacklisted: Array.Empty<string>());
        var whitelist = new FakeWhitelistService(allowed: new[] { "Alice" }, enabled: true);

        var (allowed, denialReason) = PostLoginAccountGuard.Validate("Alice", blacklist, whitelist);

        Assert.True(allowed);
        Assert.Null(denialReason);
    }

    [Fact]
    public void Validate_ShouldReturnFalse_WhenAccountBlacklisted()
    {
        var blacklist = new FakeBlacklistService(blacklisted: new[] { "Alice" }, denyMessageFor: "你已被封禁，原因：作弊");
        var whitelist = new FakeWhitelistService(allowed: new[] { "Alice" }, enabled: true);

        var (allowed, denialReason) = PostLoginAccountGuard.Validate("Alice", blacklist, whitelist);

        Assert.False(allowed);
        Assert.Equal("你已被封禁，原因：作弊", denialReason);
    }

    [Fact]
    public void Validate_ShouldReturnFalse_WhenAccountNotInWhitelist()
    {
        var blacklist = new FakeBlacklistService(blacklisted: Array.Empty<string>());
        var whitelist = new FakeWhitelistService(allowed: new[] { "Alice" }, enabled: true, denyMessage: "你不在白名单中");

        var (allowed, denialReason) = PostLoginAccountGuard.Validate("Bob", blacklist, whitelist);

        Assert.False(allowed);
        Assert.Equal("你不在白名单中", denialReason);
    }

    [Fact]
    public void Validate_ShouldReturnBlacklistReason_BeforeWhitelistCheck()
    {
        // Account is on the blacklist AND missing from the whitelist. The
        // guard must report the blacklist denial first so banned users always
        // see their ban message.
        var blacklist = new FakeBlacklistService(blacklisted: new[] { "Alice" }, denyMessageFor: "你已被封禁，原因：作弊");
        var whitelist = new FakeWhitelistService(allowed: Array.Empty<string>(), enabled: true, denyMessage: "你不在白名单中");

        var (allowed, denialReason) = PostLoginAccountGuard.Validate("Alice", blacklist, whitelist);

        Assert.False(allowed);
        Assert.Equal("你已被封禁，原因：作弊", denialReason);
    }

    private sealed class FakeBlacklistService : IBlacklistService
    {
        private readonly HashSet<string> _blacklisted;
        private readonly string _denyMessage;

        public FakeBlacklistService(IReadOnlyCollection<string> blacklisted, string denyMessageFor = "")
        {
            _blacklisted = new HashSet<string>(blacklisted, StringComparer.OrdinalIgnoreCase);
            _denyMessage = denyMessageFor;
        }

        public BlacklistSettings Settings => new(true, _denyMessage);

        public IReadOnlyList<BlacklistEntry> GetAll()
            => _blacklisted.Select(u => new BlacklistEntry(u, "")).ToList();

        public bool IsBlacklisted(string user) => _blacklisted.Contains(user);

        public bool TryAdd(string user, string reason, out string? error)
        {
            error = null;
            return _blacklisted.Add(user);
        }

        public bool TryRemove(string user, out string? error)
        {
            error = null;
            return _blacklisted.Remove(user);
        }

        public bool TryValidateJoin(string user, out string? denialReason)
        {
            if (_blacklisted.Contains(user))
            {
                denialReason = _denyMessage;
                return false;
            }

            denialReason = null;
            return true;
        }
    }

    private sealed class FakeWhitelistService : IWhitelistService
    {
        private readonly HashSet<string> _allowed;
        private readonly bool _enabled;
        private readonly string _denyMessage;

        public FakeWhitelistService(IReadOnlyCollection<string> allowed, bool enabled, string denyMessage = "")
        {
            _allowed = new HashSet<string>(allowed, StringComparer.OrdinalIgnoreCase);
            _enabled = enabled;
            _denyMessage = denyMessage;
        }

        public WhitelistSettings Settings => new(_enabled, _denyMessage);

        public IReadOnlyList<string> GetAll() => _allowed.ToList();

        public bool IsWhitelisted(string user) => _allowed.Contains(user);

        public bool TryAdd(string user, out string? error)
        {
            error = null;
            return _allowed.Add(user);
        }

        public bool TryRemove(string user, out string? error)
        {
            error = null;
            return _allowed.Remove(user);
        }

        public bool TryValidateJoin(string user, out string? denialReason)
        {
            if (!_enabled || _allowed.Contains(user))
            {
                denialReason = null;
                return true;
            }

            denialReason = _denyMessage;
            return false;
        }
    }
}
