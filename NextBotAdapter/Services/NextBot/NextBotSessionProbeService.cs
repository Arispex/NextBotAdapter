using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NextBotAdapter.Models;

namespace NextBotAdapter.Services;

public enum NextBotProbeStatus
{
    Skipped,
    Ok,
    Unauthorized,
    InvalidToken,
    Unreachable,
}

public sealed record NextBotProbeResult(
    NextBotProbeStatus Status,
    int? HttpStatus,
    string Message);

public sealed record NextBotLoginRequestResult(
    bool Success,
    int? HttpStatus,
    string Message);

public interface INextBotSessionProbeService
{
    Task<NextBotProbeResult> ProbeAsync(NextBotSettings settings, CancellationToken ct = default);

    Task<NextBotLoginRequestResult> NotifyLoginRequestAsync(NextBotSettings settings, string playerName, bool newDevice = false, bool newLocation = false, CancellationToken ct = default);
}

public sealed class NextBotSessionProbeService : INextBotSessionProbeService
{
    private static readonly HttpClient DefaultClient = new() { Timeout = TimeSpan.FromSeconds(5) };
    private readonly HttpClient _httpClient;

    public NextBotSessionProbeService()
        : this(DefaultClient)
    {
    }

    public NextBotSessionProbeService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<NextBotProbeResult> ProbeAsync(NextBotSettings settings, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(settings.BaseUrl) || string.IsNullOrWhiteSpace(settings.Token))
        {
            return new NextBotProbeResult(
                NextBotProbeStatus.Skipped,
                null,
                "未配置 baseUrl 或 token");
        }

        if (!Uri.TryCreate($"{settings.BaseUrl.TrimEnd('/')}/webui/api/session", UriKind.Absolute, out var uri))
        {
            return new NextBotProbeResult(
                NextBotProbeStatus.Unreachable,
                null,
                $"baseUrl 不是合法的 URL：{settings.BaseUrl}");
        }

        var body = JsonConvert.SerializeObject(new { token = settings.Token });
        using var request = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };

        try
        {
            using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
            var httpStatus = (int)response.StatusCode;

            return response.StatusCode switch
            {
                HttpStatusCode.Created => new NextBotProbeResult(
                    NextBotProbeStatus.Ok,
                    httpStatus,
                    "上游返回 201 Created，token 有效"),
                HttpStatusCode.Unauthorized => new NextBotProbeResult(
                    NextBotProbeStatus.Unauthorized,
                    httpStatus,
                    "token 错误"),
                HttpStatusCode.UnprocessableEntity => new NextBotProbeResult(
                    NextBotProbeStatus.InvalidToken,
                    httpStatus,
                    "token 不能为空"),
                _ => new NextBotProbeResult(
                    NextBotProbeStatus.Unreachable,
                    httpStatus,
                    $"上游返回非预期状态码 {httpStatus}"),
            };
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            return new NextBotProbeResult(
                NextBotProbeStatus.Unreachable,
                null,
                $"请求超时：{ex.Message}");
        }
        catch (HttpRequestException ex)
        {
            return new NextBotProbeResult(
                NextBotProbeStatus.Unreachable,
                null,
                $"网络异常：{ex.Message}");
        }
    }

    public async Task<NextBotLoginRequestResult> NotifyLoginRequestAsync(
        NextBotSettings settings,
        string playerName,
        bool newDevice = false,
        bool newLocation = false,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(settings.BaseUrl) || string.IsNullOrWhiteSpace(settings.Token))
        {
            return new NextBotLoginRequestResult(false, null, "未配置 baseUrl 或 token");
        }

        var url = $"{settings.BaseUrl.TrimEnd('/')}/webui/api/login-requests?token={Uri.EscapeDataString(settings.Token)}";
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return new NextBotLoginRequestResult(false, null, $"baseUrl 不是合法的 URL：{settings.BaseUrl}");
        }

        var body = JsonConvert.SerializeObject(new { name = playerName, newDevice, newLocation });
        using var request = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };

        try
        {
            using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
            var httpStatus = (int)response.StatusCode;
            var text = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.Created)
            {
                return new NextBotLoginRequestResult(true, httpStatus, $"NextBot 已接收玩家 {playerName} 的登入请求");
            }

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                return new NextBotLoginRequestResult(false, httpStatus, "token 错误 (HTTP 401)");
            }

            var (code, message) = TryParseErrorBody(text);
            var reason = code is null
                ? $"HTTP {httpStatus}"
                : $"{code}: {message} (HTTP {httpStatus})";
            return new NextBotLoginRequestResult(false, httpStatus, reason);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            return new NextBotLoginRequestResult(false, null, $"请求超时：{ex.Message}");
        }
        catch (HttpRequestException ex)
        {
            return new NextBotLoginRequestResult(false, null, $"网络异常：{ex.Message}");
        }
    }

    private static (string? Code, string? Message) TryParseErrorBody(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return (null, null);
        }

        try
        {
            var err = JObject.Parse(text)["error"];
            if (err is null)
            {
                return (null, null);
            }
            return (err["code"]?.ToString(), err["message"]?.ToString());
        }
        catch
        {
            return (null, null);
        }
    }
}
