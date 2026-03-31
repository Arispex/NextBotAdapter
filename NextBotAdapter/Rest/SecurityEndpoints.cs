using NextBotAdapter.Infrastructure;
using NextBotAdapter.Services;
using Rests;

namespace NextBotAdapter.Rest;

public static class SecurityEndpoints
{
    public static ILoginConfirmationService? Service { get; set; }

    public static object ConfirmLogin(RestRequestArgs args)
        => ConfirmLogin(ReadRouteUser(args), Service!, UserDataService.DefaultGateway);

    public static object ConfirmLogin(string? user, ILoginConfirmationService service, IUserDataGateway gateway)
    {
        if (string.IsNullOrWhiteSpace(user))
        {
            return EndpointResponseFactory.MissingUser();
        }

        if (!gateway.TryGetUserAccountId(user, out _))
        {
            return EndpointResponseFactory.Error("User was not found.");
        }

        if (!service.TryApproveNextLogin(user, out var error))
        {
            return EndpointResponseFactory.Error(error ?? "Failed to approve login.");
        }

        return new RestObject("200") { { "response", $"User '{user}' has been approved for next login." } };
    }

    private static string? ReadRouteUser(RestRequestArgs args)
        => args.Verbs?[RequestParameters.User] ?? args.Parameters?[RequestParameters.User] ?? args.Request?.Parameters?[RequestParameters.User];
}
