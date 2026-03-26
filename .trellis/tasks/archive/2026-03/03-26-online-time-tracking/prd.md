# Add Player Online Time Tracking

## Goal
Track how long each player has been online (accumulated across sessions),
persist the data to disk, expose it via the existing stats endpoint and a new leaderboard.

## Requirements
- Tracking starts on PlayerPostLogin (registered account login), not raw server join
- Persist accumulated seconds to `OnlineTime.json` in the NextBotAdapter config directory
- `GET /nextbot/users/{user}/stats` gains `onlineSeconds` field (historical + current session if online)
- `GET /nextbot/leaderboards/online-time` returns all players with recorded time sorted by onlineSeconds descending
- On server shutdown (plugin Dispose), persist all active sessions

## Acceptance Criteria
- [ ] `onlineSeconds` appears in stats API response
- [ ] Stats `onlineSeconds` reflects current session time if player is online
- [ ] `GET /nextbot/leaderboards/online-time` returns entries sorted by `onlineSeconds` descending
- [ ] `OnlineTime.json` is created on first login and updated on each logout/shutdown
- [ ] Thread-safe session tracking
- [ ] Route and permission registered

## Response Shape (stats)
```json
{
  "health": 400,
  "maxHealth": 500,
  "mana": 100,
  "maxMana": 200,
  "questsCompleted": 7,
  "deathsPve": 12,
  "deathsPvp": 3,
  "onlineSeconds": 36000
}
```

## Response Shape (leaderboard)
```json
{
  "entries": [
    { "username": "Arispex", "onlineSeconds": 36000 }
  ]
}
```

## Technical Notes
- `OnlineTimeStore` — JSON model: `{ "records": { "username": seconds } }`
- `OnlineTimeFileService` — loads/saves `OnlineTime.json`
- `IOnlineTimeService` / `OnlineTimeService` — in-memory session dict + persisted store
- `UserInfoResponse` — add `OnlineSeconds` with default 0
- `UserInfoService` — new overload taking `IOnlineTimeService?`
- `UserEndpoints` — static `OnlineTimeService` property, updated Stats handler
- Plugin hooks: `PlayerHooks.PlayerPostLogin`, `ServerApi.Hooks.ServerLeave`
