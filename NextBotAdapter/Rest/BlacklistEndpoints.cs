using NextBotAdapter.Infrastructure;
using NextBotAdapter.Services;
using Rests;

namespace NextBotAdapter.Rest;

public static class BlacklistEndpoints
{
    public static IBlacklistService Service { get; set; } = null!;

    public static object List(RestRequestArgs _)
        => List(Service);

    public static object List(IBlacklistService service)
        => new RestObject("200") { { "entries", service.GetAll() } };

    public static object Add(RestRequestArgs args)
        => Add(ReadRouteUser(args), args.Parameters?["reason"], Service);

    public static object Add(string? user, string? reason, IBlacklistService service)
    {
        if (string.IsNullOrWhiteSpace(user))
        {
            return EndpointResponseFactory.MissingUser();
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return EndpointResponseFactory.Error("Missing required parameter 'reason'.");
        }

        if (!service.TryAdd(user, reason, out var error))
        {
            return EndpointResponseFactory.Error(error ?? "Blacklist user is invalid.");
        }

        return new RestObject("200") { { "response", $"User '{user}' has been added to the blacklist." } };
    }

    public static object Remove(RestRequestArgs args)
        => Remove(ReadRouteUser(args), Service);

    public static object Remove(string? user, IBlacklistService service)
    {
        if (string.IsNullOrWhiteSpace(user))
        {
            return EndpointResponseFactory.MissingUser();
        }

        if (!service.TryRemove(user, out var error))
        {
            return EndpointResponseFactory.Error(error ?? "Blacklist user is invalid.");
        }

        return new RestObject("200") { { "response", $"User '{user}' has been removed from the blacklist." } };
    }

    private static string? ReadRouteUser(RestRequestArgs args)
        => args.Verbs?[RequestParameters.User] ?? args.Parameters?[RequestParameters.User] ?? args.Request?.Parameters?[RequestParameters.User];
}
