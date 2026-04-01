# Add Login Confirmation API for UUID/IP Change Detection

## Goal
When a player's UUID or IP differs from the last recorded values in TShock's database,
deny their login immediately and instruct them to request confirmation via QQ group.
A REST API allows an external bot to approve the player's next login attempt.

## Requirements
- Hook `PlayerHooks.PlayerPreLogin` to compare current UUID/IP against stored values
- If changed and not pre-approved → deny login, disconnect with message
- Message: "你的 {changed} 发生变化，请在 QQ 群发送「登入」后重新连接。"
- `GET /nextbot/security/confirm-login/{user}` → creates a 5-minute approval window
- On next login within the window → consume approval, allow login (TShock updates UUID/IP naturally)
- If approval has expired → treat as not approved

## Data Source
- UUID: `account.UUID` vs `player.UUID`
- IP: last entry of `account.KnownIps` (JSON array) vs `player.IP`
- Both are persisted by TShock in its SQLite DB — no extra storage needed
- Skip check if stored value is empty (first-ever login for that field)

## API

### GET `/nextbot/security/confirm-login/{user}`
**Permission:** `nextbot.security.confirm_login`

Success 200:
```json
{ "response": "User 'Arispex' has been approved for next login." }
```

Errors:
- 400 `Missing required route parameter 'user'.`
- 400 `User was not found.`

## Acceptance Criteria
- [ ] UUID or IP change triggers disconnect with correct message
- [ ] Approval is consumed on the approved login attempt
- [ ] Expired approval (> 5 min) is treated as not approved
- [ ] API returns 400 for unknown user
- [ ] Approval is thread-safe
- [ ] Route and permission registered

## Technical Notes
- `ILoginConfirmationService` / `LoginConfirmationService` — in-memory approval store (lock-based)
- `SecurityEndpoints` — new endpoint file
- Plugin: register `PlayerHooks.PlayerPreLogin` += `OnPlayerPreLogin`
- KnownIps parse: JSON array, take last element; fall back gracefully on parse error
