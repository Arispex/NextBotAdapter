# Add Death Leaderboard REST API

## Goal
Add a new REST API endpoint that returns a death leaderboard for all registered players, sorted by total deaths (PvE + PvP) descending. The endpoint lives under a `/leaderboards/` prefix to accommodate future leaderboards.

## Requirements
- Route: `GET /nextbot/leaderboards/deaths`
- Permission: `nextbot.leaderboards.deaths`
- Data scope: all registered players (iterate TShock user database)
- Sorting: total deaths (deathsPve + deathsPvp) descending
- Response fields per entry: `username` (string), `deaths` (integer, total)

## Acceptance Criteria
- [ ] `GET /nextbot/leaderboards/deaths` returns 200 with `entries` array
- [ ] Each entry contains `username` and `deaths` (total = PvE + PvP)
- [ ] Results are sorted by `deaths` descending
- [ ] Covers all registered players, not just online ones
- [ ] Players with no character data are skipped (not returned)
- [ ] Route, permission registered in `EndpointRoutes` / `Permissions`
- [ ] Endpoint registered in `EndpointRegistrar`
- [ ] `docs/REST_API.md` updated with new endpoint documentation

## Response Shape
```json
{
  "entries": [
    { "username": "Arispex", "deaths": 15 },
    { "username": "NextBot", "deaths": 8 }
  ]
}
```

## Technical Notes
- New `LeaderboardEndpoints.cs` in `Rest/`
- New response model `DeathLeaderboardResponse.cs` in `Models/Responses/`
- Extend `IUserDataGateway` + `UserDataService` with a method to enumerate all user accounts and their data
- Use existing `PlayerStatisticsReader.ReadDeaths()` for death count extraction
- Deaths field name in player data: same reflection-based approach already used in `UserInfoService`
