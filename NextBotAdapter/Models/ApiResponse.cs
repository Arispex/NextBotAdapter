namespace NextBotAdapter.Models;

public static class ApiResponse
{
    public static ApiEnvelope<T> Success<T>(T data)
        => new(data, null);

    public static ApiEnvelope<object?> Failure(string message)
        => new(null, new ApiError(message));

    public static ApiEnvelope<T> Failure<T>(string message)
        => new(default, new ApiError(message));
}
