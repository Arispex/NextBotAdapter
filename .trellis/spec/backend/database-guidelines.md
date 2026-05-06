# Database Guidelines

> Database patterns and persistence conventions for this project.

---

## Overview

This project does **not** own a relational schema or migration system. Backend persistence currently follows two patterns:

1. **Read player data from TShock-owned storage** through TShock APIs
2. **Store plugin-owned state in JSON files** under the TShock save directory

Do not invent Entity Framework, Dapper, or migration conventions for this repository unless the codebase actually adds them.

---

## Query Patterns

### Access TShock data through a gateway

The only database-like access in the current codebase is wrapped behind interfaces and adapters:

- `UserDataService` implements `IPlayerDataAccessor`
- `TShockUserDataGateway` implements `IUserDataGateway`
- TShock APIs are called from the gateway, not from endpoint classes

Example: `NextBotAdapter/Services/UserDataService.cs`

Current pattern:

- resolve the TShock account id with `TShock.UserAccounts.GetUserAccountByName`
- load player data with `TShock.CharacterDB.GetPlayerData`
- return `UserLookupError` on failure instead of throwing for ordinary lookup misses

### Map integration data before returning it

Do not expose raw TShock data structures directly from the REST layer. Read from the integration boundary, then map into response DTOs.

Examples:
- `NextBotAdapter/Services/UserInventoryService.cs`
- `NextBotAdapter/Services/UserInfoService.cs`
- `NextBotAdapter/Services/PlayerInventoryMapper.cs`
- `NextBotAdapter/Services/UserInfoMapper.cs`

### Plugin-owned persistence is file-backed

Whitelist state and plugin settings are stored as JSON files through `PluginConfigService` and `WhitelistFileService`, not in a database table.

Examples:
- `NextBotAdapter/Services/PluginConfigService.cs`
- `NextBotAdapter/Services/WhitelistFileService.cs`
- `docs/CONFIGURATION.md`

### TShock account identity for persistence keys

When you need a stable per-account key (file name, dictionary key, bitmap owner, progress record id, etc.), use **`Account.Name`**, not `Account.UUID`.

- Do: `args.Player.Account.Name`
- Do not: `args.Player.Account.UUID`

Why: TShock's `UserAccount.UUID` is **the last UUID the client sent at login**, i.e. a device fingerprint that gets overwritten on every successful login (both by TShock's built-in UUID auto-login and by this plugin's `PerformAutoLogin`, which calls `TShock.UserAccounts.SetUserAccountUUID(account, player.UUID)`). Using it as a persistence key causes:

- same device, multiple accounts -> records cross-contaminate
- same account, new device -> previous record looks lost

This matches existing usage in the codebase, e.g. `_onlineTimeService.StartSession(args.Player.Account.Name)`. If a future feature needs to survive account renames, switch to `Account.ID` (the database primary key); do not fall back to `Account.UUID`.

Reference task: `05-06-fix-player-exploration-key-use-account-name`.

---

## Migrations

There is currently **no migration workflow** in this repository.

If you need to add new persistent plugin-owned data:

- prefer an explicit JSON file under the plugin config directory
- update `docs/CONFIGURATION.md` when the file contract changes
- preserve safe fallback behavior when files are missing or malformed
- do not silently overwrite corrupted files during recovery

Existing examples:
- missing settings file -> create defaults: `PluginConfigService.LoadWhitelistSettings()`
- invalid settings JSON -> log and use defaults without overwriting the bad file
- invalid whitelist JSON -> log and use an empty in-memory whitelist without overwriting the bad file

---

## Naming Conventions

### File-backed contracts

- Config directory: `TShock.SavePath/NextBotAdapter`
- Main config file: `NextBotAdapter.json`
- Whitelist file: `Whitelist.json`

These names are part of the operational contract and are also documented in `docs/CONFIGURATION.md`.

### JSON field naming

- JSON serialization uses `JsonSerializerDefaults.Web`, so contracts are camelCase by default.
- Use `[JsonPropertyName(...)]` when the field name must remain explicit and stable.

Examples:
- `NextBotAdapter/Models/WhitelistStore.cs` -> `users`
- `NextBotAdapter/Models/Responses/WhitelistListResponse.cs` -> `users`

### Centralized API constants

Even though routes and permissions are not database objects, they are part of the contract surface and should stay centralized:

- `NextBotAdapter/Infrastructure/EndpointRoutes.cs`
- `NextBotAdapter/Infrastructure/Permissions.cs`

---

## Common Mistakes

- Do not query TShock APIs directly from endpoint classes.
- Do not add raw SQL strings to `Rest/` or mapping classes.
- Do not mix persistence concerns with response-shaping concerns.
- Do not overwrite malformed JSON files during fallback recovery.
- Do not change persisted file names or JSON field names without updating `docs/CONFIGURATION.md` and related tests.
- Do not assume this repository has a migration toolchain when it currently does not.
- Do not use `Account.UUID` as a persistence key; it is a per-login device fingerprint, not account identity. Use `Account.Name` (or `Account.ID` if rename-stability is required).
