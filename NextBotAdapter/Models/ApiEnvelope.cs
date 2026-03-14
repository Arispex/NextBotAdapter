namespace NextBotAdapter.Models;

public sealed record ApiEnvelope<T>(T? Data, ApiError? Error);
