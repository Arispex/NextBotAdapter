using Rests;

namespace NextBotAdapter.Infrastructure;

public static class EndpointResponseFactory
{
    public static RestObject MissingUser()
        => Error("Missing required route parameter 'user'.");

    public static RestObject Error(string message, string code = "400")
    {
        var obj = new RestObject(code) { Error = message };
        return obj;
    }
}
