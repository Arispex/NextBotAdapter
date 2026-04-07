# Quality Guidelines

> Code quality standards for backend development.

---

## Overview

Backend code in this repository is intentionally small, explicit, and heavily test-covered. The project favors:

- thin static REST endpoint classes
- service-layer business logic
- immutable record-based contracts
- centralized string constants for routes, permissions, and error codes
- focused xUnit tests for contracts and behavior

Because this is a plugin project, avoid importing large web-framework habits that are not already present in the codebase.

---

## Forbidden Patterns

- Do not put business logic directly in `Plugin/` or `Rest/` classes.
- Do not duplicate route strings, permission nodes, or error-code literals when constants already exist in `Infrastructure/`.
- Do not return raw TShock integration objects directly from endpoints.
- Do not use exceptions for ordinary validation and lookup failures.
- Do not bypass `PluginLogger` and hand-roll log formatting.
- Do not introduce mutable DTO classes when the existing pattern is record-based contracts.
- Do not invent ASP.NET controller, DI container, or middleware abstractions unless the project actually adopts them.
- Do not change public REST field names or file-backed config field names casually, because they are documented contracts.

---

## Required Patterns

- Keep endpoint methods thin: validate input, call a service, map via `EndpointResponseFactory`.
- Prefer `Try*` service methods for expected business outcomes.
- Centralize stable contract strings in `Infrastructure/`.
- Use `record` types for response and config models.
- Keep plugin-owned persistence behind `PluginConfigService`-style classes.
- Add or update tests when changing route definitions, error behavior, config contracts, response shapes, or logger behavior.
- Preserve readable, explicit code over generic abstractions.

Examples:
- thin endpoints: `NextBotAdapter/Rest/UserEndpoints.cs`
- response factory usage: `NextBotAdapter/Infrastructure/EndpointResponseFactory.cs`
- immutable contracts: `NextBotAdapter/Models/Responses/UserInfoResponse.cs`
- explicit service orchestration: `NextBotAdapter/Services/PersistedWhitelistService.cs`

---

## Testing Requirements

The repository uses xUnit in `NextBotAdapter.Tests/`.

Current testing style includes:

- endpoint contract tests: `RestEndpointLogicTests.cs`, `WhitelistEndpointsTests.cs`, `ConfigEndpointsTests.cs`
- helper and factory tests: `EndpointResponseFactoryTests.cs`, `PluginLoggerTests.cs`
- route and permission constant tests: `EndpointRouteDefinitionsTests.cs`
- service behavior tests: `ServiceBehaviorTests.cs`, `PluginConfigServiceTests.cs`, `WhitelistFileServiceTests.cs`

When backend behavior changes, update or add tests close to the affected concept.

Minimum expectation for backend changes:

- behavior-changing logic should have an automated test
- public REST contract changes should update tests and `docs/REST_API.md`
- config contract changes should update tests and `docs/CONFIGURATION.md`

---

## Code Review Checklist

Reviewers should verify:

- layer placement is correct (`Plugin` vs `Rest` vs `Services` vs `Infrastructure` vs `Models`)
- REST responses still use the documented `status` + `data` / `error` structure
- error codes and permission / route constants are centralized
- logging uses `PluginLogger` and includes useful context
- malformed config fallback behavior remains safe
- tests cover the changed behavior
- user-facing operational messages preserve the concrete failure reason when relevant
