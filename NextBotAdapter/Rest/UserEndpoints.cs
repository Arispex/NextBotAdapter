using NextBotAdapter.Infrastructure;
using NextBotAdapter.Models.Responses;
using NextBotAdapter.Services;
using Rests;

namespace NextBotAdapter.Rest;

public static class UserEndpoints
{
    public static object Inventory(RestRequestArgs args)
        => Inventory(ReadRouteUser(args), UserDataService.Default);

    public static object Inventory(string? user, IPlayerDataAccessor accessor)
    {
        if (string.IsNullOrWhiteSpace(user))
        {
            return EndpointResponseFactory.MissingUser();
        }

        if (!UserInventoryService.TryGetInventory(user, accessor, out var inventory, out var error))
        {
            return EndpointResponseFactory.FromUserLookupError(error);
        }

        return EndpointResponseFactory.Success(inventory);
    }

    public static object Stats(RestRequestArgs args)
        => Stats(ReadRouteUser(args), UserDataService.Default);

    public static object Stats(string? user, IPlayerDataAccessor accessor)
    {
        if (string.IsNullOrWhiteSpace(user))
        {
            return EndpointResponseFactory.MissingUser();
        }

        if (!UserInfoService.TryGetUserInfo(user, accessor, out var response, out var error))
        {
            return EndpointResponseFactory.FromUserLookupError(error);
        }

        return EndpointResponseFactory.Success(response);
    }

    private static string? ReadRouteUser(RestRequestArgs args)
        => args.Parameters?[RequestParameters.User] ?? args.Request?.Parameters?[RequestParameters.User];
}
