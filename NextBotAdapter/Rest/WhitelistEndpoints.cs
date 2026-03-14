using NextBotAdapter.Infrastructure;
using NextBotAdapter.Models.Responses;
using NextBotAdapter.Services;
using Rests;

namespace NextBotAdapter.Rest;

public static class WhitelistEndpoints
{
    public static IWhitelistService Service { get; set; } = null!;

    public static object List(RestRequestArgs _)
        => List(Service);

    public static object List(IWhitelistService service)
        => EndpointResponseFactory.Success(new WhitelistListResponse(service.GetAll()));

    public static object Add(RestRequestArgs args)
        => Add(ReadRouteUser(args), Service);

    public static object Add(string? user, IWhitelistService service)
    {
        if (string.IsNullOrWhiteSpace(user))
        {
            return EndpointResponseFactory.Error("400", ErrorCodes.WhitelistUserInvalid, "Whitelist user is invalid.");
        }

        if (!service.TryAdd(user, out var error))
        {
            return error?.Code switch
            {
                ErrorCodes.WhitelistUserExists => EndpointResponseFactory.Error("409", error.Code, error.Message),
                _ => EndpointResponseFactory.Error("400", error?.Code ?? ErrorCodes.WhitelistUserInvalid, error?.Message ?? "Whitelist user is invalid.")
            };
        }

        return EndpointResponseFactory.Success(new WhitelistListResponse(service.GetAll()));
    }

    public static object Remove(RestRequestArgs args)
        => Remove(ReadRouteUser(args), Service);

    public static object Remove(string? user, IWhitelistService service)
    {
        if (string.IsNullOrWhiteSpace(user))
        {
            return EndpointResponseFactory.Error("400", ErrorCodes.WhitelistUserInvalid, "Whitelist user is invalid.");
        }

        if (!service.TryRemove(user, out var error))
        {
            return error?.Code switch
            {
                ErrorCodes.WhitelistUserNotFound => EndpointResponseFactory.Error("404", error.Code, error.Message),
                _ => EndpointResponseFactory.Error("400", error?.Code ?? ErrorCodes.WhitelistUserInvalid, error?.Message ?? "Whitelist user is invalid.")
            };
        }

        return EndpointResponseFactory.Success(new WhitelistListResponse(service.GetAll()));
    }

    private static string? ReadRouteUser(RestRequestArgs args)
        => args.Verbs?[RequestParameters.User] ?? args.Parameters?[RequestParameters.User] ?? args.Request?.Parameters?[RequestParameters.User];
}
