# Error Handling

> How errors are handled in this project.

---

## Overview

The project prefers **explicit error values and response factories** over exception-driven control flow.

Current error-handling flow:

1. Validate request-edge input in endpoint methods
2. Use `Try*` service methods for expected business failures
3. Represent domain lookup failures with `UserLookupError`
4. Convert failures into a consistent REST error payload with `EndpointResponseFactory`
5. Catch exceptions only at integration boundaries where fallback or a `500` response is required

This keeps normal failure paths testable and predictable.

---

## Error Types

### `UserLookupError`

Use `UserLookupError` for expected service failures such as:

- missing route user
- unknown user account
- missing player data
- whitelist validation failures

Example: `NextBotAdapter/Models/UserLookupError.cs`

### `ApiError`

REST errors are serialized with `ApiError` and wrapped into a `RestObject`.

Example: `NextBotAdapter/Models/ApiError.cs`

### Centralized error codes

Keep error code strings in `Infrastructure/ErrorCodes.cs` and reuse them across services, endpoints, tests, and docs.

Examples referenced from:
- `NextBotAdapter/Infrastructure/EndpointResponseFactory.cs`
- `NextBotAdapter/Rest/WhitelistEndpoints.cs`
- `NextBotAdapter.Tests/EndpointResponseFactoryTests.cs`

---

## Error Handling Patterns

### Validate early in endpoints

Endpoint methods should reject missing or blank route parameters before invoking services.

Examples:
- `NextBotAdapter/Rest/UserEndpoints.cs`
- `NextBotAdapter/Rest/WhitelistEndpoints.cs`

Typical behavior:

- blank `user` route value -> return `400`
- business not-found condition -> return `404`
- duplicate whitelist add -> return `409`

### Prefer `Try*` flows for expected failures

Use boolean-returning methods with `out` values for domain logic.

Examples:
- `UserInventoryService.TryGetInventory(...)`
- `UserInfoService.TryGetUserInfo(...)`
- `IWhitelistService.TryAdd(...)`
- `IWhitelistService.TryRemove(...)`
- `PersistedWhitelistService.TryValidateJoin(...)`

### Validate identity by account name, not display name, and re-check after login

TShock distinguishes the player's **display name** (`player.Name`, set client-side, can be anything) from the **account name** (`player.Account.Name`, the canonical identity established by `/login`). For any access-control check (whitelist, blacklist, role gating), the rule is:

- Treat display name as untrusted: it can be set freely on the connect packet and a player can rename it before logging in.
- Treat `account.Name` as the real identity: it is established only after authentication.
- Run authorization checks at **both** ingress and post-login - same predicate, different identifier:
  - `OnPlayerInfo` (or equivalent connect hook): check by `args.Name` to fail fast on obvious denials.
  - `OnPlayerPostLogin`: re-check by `args.Player.Account.Name` and `Disconnect` if the real identity is denied.

Why: a player can connect with a display name that passes the ingress check, then `/login <bannedAccount> <password>` to assume a denied account. Without the post-login re-check, the ingress filter is bypassed entirely.

Express the rule as a helper that returns an explicit `(Allowed, DenialReason)` value, not a thrown exception, and reuse it from every entry point:

```csharp
public static class PostLoginAccountGuard
{
    public static (bool Allowed, string? DenialReason) Validate(
        string accountName,
        IBlacklistService blacklist,
        IWhitelistService whitelist) { /* ... */ }
}

// Plugin hook:
var guard = PostLoginAccountGuard.Validate(args.Player.Account.Name, _blacklist, _whitelist);
if (!guard.Allowed)
{
    PluginLogger.Warn($"账号 {args.Player.Account.Name} 登录后被拒绝（按账号名核验）：{guard.DenialReason}");
    args.Player?.Disconnect(guard.DenialReason!);
    return;
}
```

Tests required: a player whose display name is allowed but whose post-login account is denied must be disconnected with the same denial reason that the ingress path would emit. Cover both whitelist (account not in list) and blacklist (account in list) variants.

### Catch exceptions only at boundaries

Current boundary catches in the codebase:

- `ConfigEndpoints.Reload(...)` catches unexpected reload failures, logs them, and returns `500`
- `PluginConfigService.LoadWhitelistSettings()` catches malformed JSON and falls back to defaults
- `PluginConfigService.LoadLoginConfirmationSettings()` catches malformed JSON and falls back to defaults
- `WhitelistFileService.LoadWhitelist()` catches malformed JSON and falls back to an empty whitelist

If a failure is expected and part of normal business flow, prefer an error value over `throw`.

---

## API Error Responses

Use `EndpointResponseFactory` to produce REST payloads. The project's documented error body shape is:

```json
{
  "status": "404",
  "error": {
    "code": "user_not_found",
    "message": "User was not found."
  }
}
```

Notes:

- The TShock `RestObject` contributes the `status` field.
- The `error.code` value is stable and machine-readable.
- The `error.message` value should carry the concrete reason without hiding it.
- Success responses should not introduce frontend-facing `message` text; frontend copy should be generated by the client side.
- `error.message` should describe the effective reason only and should not be rewritten into UI copy such as "动作 + 结果".
- Do not translate or rename raw response field names.

Examples:
- `NextBotAdapter/Infrastructure/EndpointResponseFactory.cs`
- `docs/REST_API.md`
- `NextBotAdapter.Tests/RestEndpointLogicTests.cs`

---

## Common Mistakes

- Do not throw exceptions for ordinary validation or not-found cases.
- Do not return ad-hoc anonymous error shapes from endpoints.
- Do not duplicate error-code strings instead of reusing `ErrorCodes`.
- Do not swallow exceptions without logging them.
- Do not replace concrete error messages with vague placeholders when the real reason is safe to expose.
- Do not change response field names such as `status`, `error`, `code`, or `message` without updating the public API contract.
- Do not authorize a connection by display name (`player.Name`) only; display name is untrusted and the player can `/login` to a different account afterward. Re-check by `player.Account.Name` in `OnPlayerPostLogin` and `Disconnect` on denial.
