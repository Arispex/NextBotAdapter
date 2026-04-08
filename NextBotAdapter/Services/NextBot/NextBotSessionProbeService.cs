using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
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

public interface INextBotSessionProbeService
{
    Task<NextBotProbeResult> ProbeAsync(NextBotSettings settings, CancellationToken ct = default);
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
}
