# Add Fishing Quests Leaderboard REST API

## Goal
Add a new REST API endpoint that returns a fishing quests completion leaderboard for all registered players, sorted by questsCompleted descending.

## Requirements
- Route: `GET /nextbot/leaderboards/fishing-quests`
- Permission: `nextbot.leaderboards.fishing_quests`
- Data scope: all registered players
- Sorting: questsCompleted descending
- Response fields per entry: `username` (string), `questsCompleted` (integer)

## Acceptance Criteria
- [ ] `GET /nextbot/leaderboards/fishing-quests` returns 200 with `entries` array
- [ ] Each entry contains `username` and `questsCompleted`
- [ ] Results are sorted by `questsCompleted` descending
- [ ] Covers all registered players, not just online ones
- [ ] Players with no character data are skipped
- [ ] Route and permission registered in `EndpointRoutes` / `Permissions`
- [ ] Endpoint registered in `EndpointRegistrar`
- [ ] `docs/REST_API.md` updated

## Response Shape
```json
{
  "entries": [
    { "username": "Arispex", "questsCompleted": 42 },
    { "username": "NextBot", "questsCompleted": 7 }
  ]
}
```

## Technical Notes
- Follow the same pattern as the death leaderboard
- New response model `FishingQuestsLeaderboardEntryResponse.cs`
- New service `FishingQuestsLeaderboardService.cs` using `IUserDataGateway`
- New `FishingQuestsLeaderboardEndpoints.cs` (or extend `LeaderboardEndpoints.cs`)
- Field name in player data: `questsCompleted` (matches UserInfoMapper)
