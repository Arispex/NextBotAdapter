# Directory Structure

> How backend code is organized in this project.

---

## Overview

The repository is a .NET solution for a TShock plugin, not a conventional web application. Keep layers small and explicit:

- `Plugin/` wires the plugin into Terraria / TShock lifecycle hooks
- `Rest/` exposes REST endpoints and performs request-edge validation
- `Services/` contains business logic, mapping, adapters, and persistence orchestration
- `Infrastructure/` stores shared constants and response helpers
- `Models/` stores immutable data contracts, with response DTOs under `Models/Responses/`

Tests live in the separate `NextBotAdapter.Tests/` project and usually mirror the production concept they verify.

---

## Directory Layout

```text
NextBotAdapter/
├── Infrastructure/
│   ├── EndpointResponseFactory.cs
│   ├── EndpointRoutes.cs
│   ├── ErrorCodes.cs
│   ├── Permissions.cs
│   └── RequestParameters.cs
├── Models/
│   ├── ApiError.cs
│   ├── NextBotAdapterConfig.cs
│   ├── UserLookupError.cs
│   ├── WhitelistSettings.cs
│   ├── WhitelistStore.cs
│   └── Responses/
│       ├── UserInfoResponse.cs
│       ├── UserInventoryResponse.cs
│       ├── WhitelistListResponse.cs
│       └── WorldProgressResponse.cs
├── Plugin/
│   └── NextBotAdapterPlugin.cs
├── Rest/
│   ├── ConfigEndpoints.cs
│   ├── EndpointRegistrar.cs
│   ├── UserEndpoints.cs
│   ├── WhitelistEndpoints.cs
│   └── WorldEndpoints.cs
└── Services/
    ├── ConfigurationReloadService.cs
    ├── PersistedWhitelistService.cs
    ├── PluginLogger.cs
    ├── UserDataService.cs
    ├── PluginConfigService.cs
    ├── WhitelistFileService.cs
    ├── WorldProgressMapper.cs
    └── WorldProgressService.cs

NextBotAdapter.Tests/
├── ConfigEndpointsTests.cs
├── EndpointResponseFactoryTests.cs
├── PluginLoggerTests.cs
├── RestEndpointLogicTests.cs
├── PluginConfigServiceTests.cs
├── WhitelistFileServiceTests.cs
└── WhitelistEndpointsTests.cs
```

---

## Module Organization

### Plugin bootstrap

The plugin entrypoint should stay thin and focus on wiring:

- create long-lived services
- assign endpoint dependencies
- register REST commands
- register or unregister Terraria / TShock hooks

Example: `NextBotAdapter/Plugin/NextBotAdapterPlugin.cs`

### REST layer

Endpoint classes are static and grouped by resource or capability:

- `UserEndpoints.cs` for user inventory and stats
- `WorldEndpoints.cs` for world progress
- `WhitelistEndpoints.cs` for whitelist operations
- `ConfigEndpoints.cs` for configuration reload

Endpoints should validate route parameters, call a service, and convert the result into `RestObject` using `EndpointResponseFactory`.

Examples:
- `NextBotAdapter/Rest/UserEndpoints.cs`
- `NextBotAdapter/Rest/WhitelistEndpoints.cs`
- `NextBotAdapter/Rest/ConfigEndpoints.cs`

### Services

`Services/` contains most implementation logic. Keep logic here instead of in endpoint methods.

Common service roles already present in the codebase:

- mappers: `WorldProgressMapper.cs`, `UserInfoMapper.cs`, `PlayerInventoryMapper.cs`
- domain services: `UserInfoService.cs`, `UserInventoryService.cs`, `WorldProgressService.cs`
- adapters and gateways: `UserDataService.cs`, `WorldProgressSourceAdapter.cs`
- persistence services: `PluginConfigService.cs`, `WhitelistFileService.cs`, `PersistedWhitelistService.cs`
- logging: `PluginLogger.cs`

### Infrastructure

Cross-cutting constants and response helpers belong in `Infrastructure/`.

Examples:
- `EndpointRoutes.cs` centralizes URL patterns
- `Permissions.cs` centralizes permission nodes
- `ErrorCodes.cs` centralizes API error codes
- `EndpointResponseFactory.cs` centralizes success and error payload creation

### Models

Use records for API contracts and persisted data structures.

Examples:
- `NextBotAdapter/Models/NextBotAdapterConfig.cs`
- `NextBotAdapter/Models/WhitelistStore.cs`
- `NextBotAdapter/Models/Responses/UserInfoResponse.cs`
- `NextBotAdapter/Models/Responses/WhitelistListResponse.cs`

---

## Naming Conventions

- Use **PascalCase** for directories, files, types, and public members.
- Name interfaces with an `I` prefix, such as `IWhitelistService` and `IPlayerDataAccessor`.
- Name endpoint groups with the `*Endpoints` suffix.
- Name mappers with the `*Mapper` suffix.
- Name helper factories with the `*Factory` suffix.
- Keep route, permission, parameter, and error string constants in `Infrastructure/` instead of scattering literals.
- Keep test file names aligned with the production type or behavior they verify, such as `ConfigEndpointsTests.cs` and `PluginLoggerTests.cs`.

---

## Examples

Good reference files for this structure:

- `NextBotAdapter/Plugin/NextBotAdapterPlugin.cs` - plugin lifecycle composition root
- `NextBotAdapter/Rest/UserEndpoints.cs` - thin endpoint layer with input validation
- `NextBotAdapter/Services/PluginConfigService.cs` - file-backed plugin configuration service
- `NextBotAdapter/Services/WhitelistFileService.cs` - whitelist data file persistence
- `NextBotAdapter/Infrastructure/EndpointResponseFactory.cs` - centralized REST response creation
- `NextBotAdapter/Models/Responses/WorldProgressResponse.cs` - immutable response DTO
- `NextBotAdapter.Tests/RestEndpointLogicTests.cs` - test project mirroring backend behavior
