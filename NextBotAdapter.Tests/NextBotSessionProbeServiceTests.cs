using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NextBotAdapter.Models;
using NextBotAdapter.Services;

namespace NextBotAdapter.Tests;

public sealed class NextBotSessionProbeServiceTests
{
    [Fact]
    public async Task Probe_ReturnsSkipped_WhenBaseUrlEmpty()
    {
        var probe = new NextBotSessionProbeService(new HttpClient(new FakeHandler(_ => throw new InvalidOperationException("should not be called"))));
        var result = await probe.ProbeAsync(new NextBotSettings(string.Empty, "token"));

        Assert.Equal(NextBotProbeStatus.Skipped, result.Status);
        Assert.Null(result.HttpStatus);
    }

    [Fact]
    public async Task Probe_ReturnsSkipped_WhenTokenEmpty()
    {
        var probe = new NextBotSessionProbeService(new HttpClient(new FakeHandler(_ => throw new InvalidOperationException("should not be called"))));
        var result = await probe.ProbeAsync(new NextBotSettings("https://example.com", string.Empty));

        Assert.Equal(NextBotProbeStatus.Skipped, result.Status);
    }

    [Fact]
    public async Task Probe_ReturnsOk_On201Created()
    {
        var probe = new NextBotSessionProbeService(new HttpClient(new FakeHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.Created))));

        var result = await probe.ProbeAsync(new NextBotSettings("https://example.com/", "secret"));

        Assert.Equal(NextBotProbeStatus.Ok, result.Status);
        Assert.Equal(201, result.HttpStatus);
    }

    [Fact]
    public async Task Probe_ReturnsUnauthorized_On401()
    {
        var probe = new NextBotSessionProbeService(new HttpClient(new FakeHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.Unauthorized))));

        var result = await probe.ProbeAsync(new NextBotSettings("https://example.com", "bad"));

        Assert.Equal(NextBotProbeStatus.Unauthorized, result.Status);
        Assert.Equal(401, result.HttpStatus);
    }

    [Fact]
    public async Task Probe_ReturnsInvalidToken_On422()
    {
        var probe = new NextBotSessionProbeService(new HttpClient(new FakeHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.UnprocessableEntity))));

        var result = await probe.ProbeAsync(new NextBotSettings("https://example.com", "x"));

        Assert.Equal(NextBotProbeStatus.InvalidToken, result.Status);
        Assert.Equal(422, result.HttpStatus);
    }

    [Fact]
    public async Task Probe_ReturnsUnreachable_OnUnexpectedStatus()
    {
        var probe = new NextBotSessionProbeService(new HttpClient(new FakeHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.InternalServerError))));

        var result = await probe.ProbeAsync(new NextBotSettings("https://example.com", "x"));

        Assert.Equal(NextBotProbeStatus.Unreachable, result.Status);
        Assert.Equal(500, result.HttpStatus);
    }

    [Fact]
    public async Task Probe_ReturnsUnreachable_OnHttpRequestException()
    {
        var probe = new NextBotSessionProbeService(new HttpClient(new FakeHandler(_ =>
            throw new HttpRequestException("dns failure"))));

        var result = await probe.ProbeAsync(new NextBotSettings("https://example.com", "x"));

        Assert.Equal(NextBotProbeStatus.Unreachable, result.Status);
        Assert.Contains("dns failure", result.Message);
    }

    [Fact]
    public async Task Probe_ReturnsUnreachable_OnInvalidBaseUrl()
    {
        var probe = new NextBotSessionProbeService(new HttpClient(new FakeHandler(_ =>
            throw new InvalidOperationException("should not be called"))));

        var result = await probe.ProbeAsync(new NextBotSettings("not a url", "x"));

        Assert.Equal(NextBotProbeStatus.Unreachable, result.Status);
        Assert.Contains("不是合法", result.Message);
    }

    [Fact]
    public async Task NotifyLoginRequest_ReturnsFailure_WhenNotConfigured()
    {
        var probe = new NextBotSessionProbeService(new HttpClient(new FakeHandler(_ =>
            throw new InvalidOperationException("should not be called"))));

        var result = await probe.NotifyLoginRequestAsync(new NextBotSettings(string.Empty, "t"), "Arispex");

        Assert.False(result.Success);
        Assert.Null(result.HttpStatus);
        Assert.Contains("未配置", result.Message);
    }

    [Fact]
    public async Task NotifyLoginRequest_ReturnsSuccess_On201()
    {
        string? capturedUri = null;
        string? capturedBody = null;
        var probe = new NextBotSessionProbeService(new HttpClient(new FakeHandler(req =>
        {
            capturedUri = req.RequestUri?.ToString();
            capturedBody = req.Content?.ReadAsStringAsync().Result;
            return new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent("{\"data\":{\"name\":\"Arispex\"}}"),
            };
        })));

        var result = await probe.NotifyLoginRequestAsync(new NextBotSettings("https://example.com/", "secret"), "Arispex", newDevice: true, newLocation: false);

        Assert.True(result.Success);
        Assert.Equal(201, result.HttpStatus);
        Assert.NotNull(capturedUri);
        Assert.Contains("token=secret", capturedUri);
        Assert.Contains("/webui/api/login-requests", capturedUri);
        Assert.NotNull(capturedBody);
        Assert.Contains("\"newDevice\":true", capturedBody);
        Assert.Contains("\"newLocation\":false", capturedBody);
    }

    [Fact]
    public async Task NotifyLoginRequest_ReturnsFailure_OnUnauthorized()
    {
        var probe = new NextBotSessionProbeService(new HttpClient(new FakeHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.Unauthorized))));

        var result = await probe.NotifyLoginRequestAsync(new NextBotSettings("https://example.com", "bad"), "Arispex");

        Assert.False(result.Success);
        Assert.Equal(401, result.HttpStatus);
        Assert.Contains("token 错误", result.Message);
    }

    [Fact]
    public async Task NotifyLoginRequest_ParsesErrorBodyForStructuredFailures()
    {
        var probe = new NextBotSessionProbeService(new HttpClient(new FakeHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("{\"error\":{\"code\":\"not_found\",\"message\":\"用户不存在\"}}"),
            })));

        var result = await probe.NotifyLoginRequestAsync(new NextBotSettings("https://example.com", "t"), "Arispex");

        Assert.False(result.Success);
        Assert.Equal(404, result.HttpStatus);
        Assert.Contains("not_found", result.Message);
        Assert.Contains("用户不存在", result.Message);
    }

    [Fact]
    public async Task NotifyLoginRequest_ReturnsFailure_OnNetworkException()
    {
        var probe = new NextBotSessionProbeService(new HttpClient(new FakeHandler(_ =>
            throw new HttpRequestException("dns failure"))));

        var result = await probe.NotifyLoginRequestAsync(new NextBotSettings("https://example.com", "t"), "Arispex");

        Assert.False(result.Success);
        Assert.Null(result.HttpStatus);
        Assert.Contains("dns failure", result.Message);
    }

    [Fact]
    public async Task FetchUsers_ReturnsUsers_On200()
    {
        var probe = new NextBotSessionProbeService(new HttpClient(new FakeHandler(_ =>
            new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("{\"data\":[{\"name\":\"Alice\",\"is_banned\":false,\"ban_reason\":\"\"},{\"name\":\"Bob\",\"is_banned\":true,\"ban_reason\":\"cheating\"}]}"),
            })));

        var result = await probe.FetchUsersAsync(new NextBotSettings("https://example.com/", "secret"));

        Assert.True(result.Success);
        Assert.NotNull(result.Users);
        Assert.Equal(2, result.Users!.Count);
        Assert.Equal("Alice", result.Users[0].Name);
        Assert.False(result.Users[0].IsBanned);
        Assert.Equal("Bob", result.Users[1].Name);
        Assert.True(result.Users[1].IsBanned);
        Assert.Equal("cheating", result.Users[1].BanReason);
    }

    [Fact]
    public async Task FetchUsers_ReturnsFailure_OnHttpError()
    {
        var probe = new NextBotSessionProbeService(new HttpClient(new FakeHandler(_ =>
            new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError))));

        var result = await probe.FetchUsersAsync(new NextBotSettings("https://example.com", "secret"));

        Assert.False(result.Success);
        Assert.Null(result.Users);
        Assert.Contains("500", result.Message);
    }

    [Fact]
    public async Task FetchUsers_ReturnsFailure_WhenNotConfigured()
    {
        var probe = new NextBotSessionProbeService(new HttpClient(new FakeHandler(_ =>
            throw new InvalidOperationException("should not be called"))));

        var result = await probe.FetchUsersAsync(new NextBotSettings(string.Empty, "token"));

        Assert.False(result.Success);
        Assert.Null(result.Users);
        Assert.Contains("未配置", result.Message);
    }

    [Fact]
    public async Task FetchUsers_ReturnsFailure_OnNetworkException()
    {
        var probe = new NextBotSessionProbeService(new HttpClient(new FakeHandler(_ =>
            throw new HttpRequestException("dns failure"))));

        var result = await probe.FetchUsersAsync(new NextBotSettings("https://example.com", "secret"));

        Assert.False(result.Success);
        Assert.Null(result.Users);
        Assert.Contains("dns failure", result.Message);
    }

    [Fact]
    public async Task NotifyPlayerEvent_ReturnsSuccess_On200()
    {
        string? capturedUri = null;
        string? capturedBody = null;
        var probe = new NextBotSessionProbeService(new HttpClient(new FakeHandler(req =>
        {
            capturedUri = req.RequestUri?.ToString();
            capturedBody = req.Content?.ReadAsStringAsync().Result;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"success\":true,\"data\":{\"sent_groups\":[1],\"failed_groups\":[]}}"),
            };
        })));

        var result = await probe.NotifyPlayerEventAsync(
            new NextBotSettings("https://example.com/", "secret"),
            "Steve",
            "online",
            "主服");

        Assert.True(result.Success);
        Assert.Equal(200, result.HttpStatus);
        Assert.NotNull(capturedUri);
        Assert.Contains("/webui/api/player-events", capturedUri);
        Assert.Contains("token=secret", capturedUri);
        Assert.NotNull(capturedBody);
        Assert.Contains("\"player_name\":\"Steve\"", capturedBody);
        Assert.Contains("\"event\":\"online\"", capturedBody);
        Assert.Contains("\"server_name\":\"主服\"", capturedBody);
    }

    [Fact]
    public async Task NotifyPlayerEvent_ReturnsFailure_WhenNotConfigured()
    {
        var probe = new NextBotSessionProbeService(new HttpClient(new FakeHandler(_ =>
            throw new InvalidOperationException("should not be called"))));

        var result = await probe.NotifyPlayerEventAsync(
            new NextBotSettings(string.Empty, "t"), "Steve", "online", "主服");

        Assert.False(result.Success);
        Assert.Null(result.HttpStatus);
        Assert.Contains("未配置", result.Message);
    }

    [Fact]
    public async Task NotifyPlayerEvent_ParsesErrorBody_OnValidationError()
    {
        var probe = new NextBotSessionProbeService(new HttpClient(new FakeHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.UnprocessableEntity)
            {
                Content = new StringContent("{\"success\":false,\"error\":{\"code\":\"validation_error\",\"message\":\"事件类型仅支持 online 或 offline\"}}"),
            })));

        var result = await probe.NotifyPlayerEventAsync(
            new NextBotSettings("https://example.com", "t"), "Steve", "invalid", "主服");

        Assert.False(result.Success);
        Assert.Equal(422, result.HttpStatus);
        Assert.Contains("validation_error", result.Message);
        Assert.Contains("事件类型仅支持", result.Message);
    }

    [Fact]
    public async Task NotifyPlayerEvent_ReturnsFailure_OnNetworkException()
    {
        var probe = new NextBotSessionProbeService(new HttpClient(new FakeHandler(_ =>
            throw new HttpRequestException("dns failure"))));

        var result = await probe.NotifyPlayerEventAsync(
            new NextBotSettings("https://example.com", "t"), "Steve", "online", "主服");

        Assert.False(result.Success);
        Assert.Null(result.HttpStatus);
        Assert.Contains("dns failure", result.Message);
    }

    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(responder(request));
    }
}
