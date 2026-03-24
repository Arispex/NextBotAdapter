using NextBotAdapter.Infrastructure;
using NextBotAdapter.Services;
using Rests;

namespace NextBotAdapter.Rest;

public static class WhitelistEndpoints
{
    public static IWhitelistService Service { get; set; } = null!;

    public static object List(RestRequestArgs _)
        => List(Service);

    public static object List(IWhitelistService service)
        => new RestObject("200") { { "users", service.GetAll() } };

    public static object Add(RestRequestArgs args)
        => Add(ReadRouteUser(args), Service);

    public static object Add(string? user, IWhitelistService service)
    {
        if (string.IsNullOrWhiteSpace(user))
        {
            return EndpointResponseFactory.Error("Whitelist user is invalid.");
        }

        if (!service.TryAdd(user, out var error))
        {
            return EndpointResponseFactory.Error(error ??"Whitelist user is invalid.");
        }

        return new RestObject("200") { { "response", $"User '{user}' has been added to the whitelist." } };
    }

    public static object Remove(RestRequestArgs args)
        => Remove(ReadRouteUser(args), Service);

    public static object Remove(string? user, IWhitelistService service)
    {
        if (string.IsNullOrWhiteSpace(user))
        {
            return EndpointResponseFactory.Error("Whitelist user is invalid.");
        }

        if (!service.TryRemove(user, out var error))
        {
            return EndpointResponseFactory.Error(error ??"Whitelist user is invalid.");
        }

        return new RestObject("200") { { "response", $"User '{user}' has been removed from the whitelist." } };
    }

    private static string? ReadRouteUser(RestRequestArgs args)
        => args.Verbs?[RequestParameters.User] ?? args.Parameters?[RequestParameters.User] ?? args.Request?.Parameters?[RequestParameters.User];
}
