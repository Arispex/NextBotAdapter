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

## In-memory cache + persistent store consistency

When a service holds an in-memory cache (e.g. `Dictionary<string, T>`) backed by a persistent store with multiple write paths (stamp / mutate, lazy load, explicit `Load`, save), the cache and store must stay consistent under concurrency. Two patterns are mandatory.

### Don't: two-phase lock that captures a reference then re-locks to write

```csharp
// Bad: get-or-create under lock1, release, then re-lock to mutate the captured ref.
var bitmap = GetOrCreateBitmap(name);  // lock1 inside; releases on return
lock (_lock)                           // lock2: writes into the ref captured above
{
    MarkBox(bitmap, w, h, x, y);
}
```

Why it's bad: between `lock1` and `lock2`, another thread can replace `_bitmaps[name]` (e.g. via `Load`). The mutation then writes into an **orphan** object that is no longer the cache entry - the update is silently dropped.

### Do: single-lock atomic get-or-create-and-write

Express "caller must already hold the lock" as a `*Locked` helper, then keep get-or-create and the mutation inside one lock region.

```csharp
public void MarkArea(string name, int w, int h, int x, int y)
{
    lock (_lock)
    {
        var bitmap = GetOrCreateBitmapLocked(name, w, h);
        MarkBox(bitmap, w, h, x, y);
    }
}

// Caller must already hold _lock. Lock-free by contract.
private BitArray GetOrCreateBitmapLocked(string name, int w, int h) { /* ... */ }
```

### Don't: `Load` unconditionally overwrites the cache entry

```csharp
// Bad: clobbers in-memory data that other write paths already populated.
var bitmap = _storage.Load(name, ...);
if (bitmap is null) return;
lock (_lock)
{
    _bitmaps[name] = bitmap;
}
```

Why it's bad: if another path (lazy create, stamp/mutate) has already populated `_bitmaps[name]` with newer in-memory data, `Load` erases it.

### Do: conditional insert (preserve in-memory data)

```csharp
lock (_lock)
{
    if (_bitmaps.ContainsKey(name)) return;  // in-memory data wins; do not overwrite
    _bitmaps[name] = bitmap;
}
```

Reference task: `05-06-fix-exploration-tracker-audit-followups`.
Reference code: `NextBotAdapter/Services/Exploration/PlayerExplorationTracker.cs`.

---

## Storage interface return-value expressiveness

A plugin persistence interface (`Load` / `Save` / `Delete` style) is consumed by callers that must decide whether to negative-cache, retry, or aggregate failure rates. Pick return types that let callers distinguish these outcomes; do not collapse different fail-safe meanings into a single sentinel or hide failures behind `void`.

### Don't: a single `null` that conflates "missing" with "failed"

```csharp
// Bad: null means three different things.
BitArray? Load(string name, int count);
//   null = input invalid
//   null = file does not exist        (safe to negative-cache)
//   null = file exists but IO failed  (transient; must NOT negative-cache)
```

Why it's bad: callers that add `null` results to a missing-file negative cache will permanently mark transient IO errors as "missing" for the rest of the process lifetime. Player data appears lost until the server restarts.

### Do: distinguish "confirmed missing" from "other failures"

```csharp
public sealed record ExplorationLoadResult(BitArray? Bitmap, bool FileMissing);

ExplorationLoadResult Load(string name, int count);
//   (bitmap, false) = success
//   (null, true)    = confirmed missing  -> safe to negative-cache
//   (null, false)   = invalid input / corrupt / IO error -> do NOT cache; retry next time
```

Callers branch on `FileMissing` before populating any negative cache. Transient errors retry naturally on the next access.

### Don't: `void` save that swallows failures into logs

```csharp
// Bad: caller cannot tell if the write succeeded.
void Save(string name, BitArray bitmap);
// Internal try/catch -> PluginLogger.Error only.
```

Why it's bad: a batch caller (e.g. `SaveAll` at shutdown) cannot count successes vs failures or emit a completion-rate summary. Operators see scattered ERROR lines with no aggregate signal about how many accounts actually persisted.

### Do: return `bool` (or a richer result) so batches can summarize

```csharp
bool Save(string name, BitArray bitmap);

// Caller aggregates and emits one summary line.
int success = 0, failure = 0;
foreach (var (name, bitmap) in snapshot)
{
    if (_storage.Save(name, bitmap)) success++;
    else failure++;
}
PluginLogger.Info($"SaveAll completed, success={success}, failure={failure}");
```

Apply the same shape to other persistence operations (`Delete`, `Rename`, etc.) when the caller needs to summarize or react to per-item failure.

Reference task: `05-06-fix-exploration-tracker-audit-round2`.
Reference code: `NextBotAdapter/Services/Exploration/IExplorationStorage.cs`, `NextBotAdapter/Services/Exploration/PlayerExplorationTracker.cs` (`SaveAll`).

---

## Common Mistakes

- Do not query TShock APIs directly from endpoint classes.
- Do not add raw SQL strings to `Rest/` or mapping classes.
- Do not mix persistence concerns with response-shaping concerns.
- Do not overwrite malformed JSON files during fallback recovery.
- Do not change persisted file names or JSON field names without updating `docs/CONFIGURATION.md` and related tests.
- Do not assume this repository has a migration toolchain when it currently does not.
- Do not use `Account.UUID` as a persistence key; it is a per-login device fingerprint, not account identity. Use `Account.Name` (or `Account.ID` if rename-stability is required).
- Do not split get-or-create and mutation across two `lock` regions for the same in-memory cache; another writer can replace the entry between the regions and your mutation lands on an orphan. Use a single lock plus a `*Locked` helper.
- Do not let `Load` (or any rehydration path) unconditionally overwrite an existing in-memory cache entry; check `ContainsKey` first so newer in-memory data is preserved.
- Do not return a single `null` from a storage `Load` method to mean both "file missing" and "IO error / corrupt / invalid input"; callers cannot tell whether negative-caching is safe and will permanently hide transient errors. Return a result type that distinguishes confirmed-missing from other failures.
- Do not declare a storage `Save` (or other persistence write) as `void` and only log on failure when callers need to aggregate success/failure counts. Return `bool` (or a result type) so batch callers like `SaveAll` can emit a completion-rate summary.
