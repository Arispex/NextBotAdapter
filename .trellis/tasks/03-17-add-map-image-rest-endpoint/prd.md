# Add map image REST endpoint

## Goal
Add a REST-only map image generation capability to NextBotAdapter by migrating the map image export behavior from the GenerateMap plugin into the current plugin architecture.

## Requirements
- Add a new REST endpoint to generate the current world map image.
- Do not add any chat command or non-REST trigger.
- Save generated image files under the plugin configuration directory's `cache` subdirectory.
- Ensure the `cache` directory is created during plugin configuration initialization.
- Keep the endpoint aligned with the current `status` + `data` / `error` response contract.
- Keep success responses free of frontend-facing `message` fields.
- Log important initialization and failure paths through `PluginLogger`.
- Follow existing backend layering (`Rest`, `Services`, `Infrastructure`, `Models`).
- Add or update automated tests for route registration, endpoint behavior, and config directory initialization behavior.
- Update public docs for configuration and REST API.

## Acceptance Criteria
- [ ] A new REST endpoint is exposed and registered in `EndpointRegistrar`.
- [ ] The endpoint returns a `200` success response with generated map image data wrapped in `data`.
- [ ] The image file is written to `<configDir>/cache/`.
- [ ] The plugin creates the `cache` directory during configuration initialization.
- [ ] Unexpected generation failures return a `500` error response with a stable error code and effective reason.
- [ ] No chat command is added.
- [ ] Relevant tests pass.
- [ ] `docs/REST_API.md` and `docs/CONFIGURATION.md` are updated.

## Technical Notes
- Reuse the existing plugin config directory managed by `WhitelistConfigService`.
- Keep the new feature REST-focused and compatible with current TShock `RestObject` usage.
- Prefer small service abstractions over putting image-generation logic directly into endpoint or plugin bootstrap code.
