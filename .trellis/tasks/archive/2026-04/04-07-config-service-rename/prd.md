# Rename WhitelistConfigService and Extract WhitelistFileService

## Goal

Fix the misleading naming of `WhitelistConfigService` which actually manages the entire plugin configuration (`NextBotAdapter.json`), not just whitelist config. Split it into two classes with accurate names.

## Requirements

1. Rename `WhitelistConfigService` to `PluginConfigService` — this class manages `NextBotAdapter.json` (global plugin settings)
2. Extract whitelist data file operations (`Whitelist.json`) into a new `WhitelistFileService` class
3. Update all references across the codebase
4. Update all test files to match new class names
5. Preserve all existing behavior — this is a pure rename + extract refactor

## Acceptance Criteria

- [ ] `WhitelistConfigService` no longer exists
- [ ] `PluginConfigService` handles all `NextBotAdapter.json` operations (EnsureConfigComplete, LoadWhitelistSettings, LoadLoginConfirmationSettings, SaveWhitelistSettings, ReadConfigRaw, TryUpdateConfig)
- [ ] `WhitelistFileService` handles all `Whitelist.json` operations (LoadWhitelist, SaveWhitelist)
- [ ] `PersistedWhitelistService` uses both `PluginConfigService` and `WhitelistFileService`
- [ ] `ConfigEndpoints` references `PluginConfigService`
- [ ] `NextBotAdapterPlugin` wiring updated
- [ ] All tests pass with updated class names
- [ ] No behavior changes — pure structural refactor

## Technical Notes

- `LoadSettings()` should be renamed to `LoadWhitelistSettings()` for clarity
- `SaveSettings()` should be renamed to `SaveWhitelistSettings()` for clarity
- `WhitelistFilePath` moves to `WhitelistFileService`
- `SettingsFilePath` stays in `PluginConfigService` (renamed to `ConfigFilePath` for clarity)
