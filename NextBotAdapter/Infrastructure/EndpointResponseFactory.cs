using NextBotAdapter.Models;
using Rests;

namespace NextBotAdapter.Infrastructure;

public static class EndpointResponseFactory
{
    public static RestObject MissingUser(string code = "400")
        => Error(code, ErrorCodes.MissingUser, "Missing required route parameter 'user'.");

    public static RestObject UserNotFound(string message, string code = "404")
        => Error(code, ErrorCodes.UserNotFound, message);

    public static RestObject UserDataNotFound(string message, string code = "404")
        => Error(code, ErrorCodes.UserDataNotFound, message);

    public static RestObject FromUserLookupError(UserLookupError? error)
    {
        return error?.Code switch
        {
            ErrorCodes.UserDataNotFound => UserDataNotFound(error.Message),
            ErrorCodes.MissingUser => MissingUser(),
            _ => UserNotFound(error?.Message ?? "User was not found.")
        };
    }

    public static RestObject Success<T>(T data, string code = "200")
    {
        return new RestObject(code)
        {
            { "data", data }
        };
    }

    private static RestObject Error(string code, string errorCode, string message)
    {
        return new RestObject(code)
        {
            {
                "error",
                new ApiError(errorCode, message)
            }
        };
    }
}
