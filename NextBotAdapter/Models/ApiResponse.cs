namespace NextBotAdapter.Models;

public static class ApiResponse
{
    public static ApiEnvelope<T> Success<T>(T data)
        => new(data, null);

    public static ApiEnvelope<object?> Failure(string code, string message)
        => new(null, new ApiError(code, message));

    public static ApiEnvelope<T> Failure<T>(string code, string message)
        => new(default, new ApiError(code, message));
}
