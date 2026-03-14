namespace NextBotAdapter.Infrastructure;

public static class ErrorCodes
{
    public const string MissingUser = "missing_user";
    public const string UserNotFound = "user_not_found";
    public const string UserDataNotFound = "user_data_not_found";
    public const string WhitelistUserExists = "whitelist_user_exists";
    public const string WhitelistUserNotFound = "whitelist_user_not_found";
    public const string WhitelistUserInvalid = "whitelist_user_invalid";
}
