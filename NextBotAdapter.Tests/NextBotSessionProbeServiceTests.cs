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

    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(responder(request));
    }
}
