# Backend Development Guidelines

> Project-specific backend conventions for the NextBotAdapter TShock plugin.

---

## Overview

This repository contains a small .NET 9 backend plugin for TShock. The backend code lives in `NextBotAdapter/`, tests live in `NextBotAdapter.Tests/`, and operational contracts are documented in `docs/`.

Use these guides to match the code that already exists in the repository rather than introducing generic ASP.NET or ORM conventions that the project does not use.

---

## Guidelines Index

| Guide | Description | Status |
|-------|-------------|--------|
| [Directory Structure](./directory-structure.md) | Module organization and file layout | Current |
| [Database Guidelines](./database-guidelines.md) | TShock data access and file-backed persistence patterns | Current |
| [Error Handling](./error-handling.md) | Error types, handling strategies, REST error contracts | Current |
| [Quality Guidelines](./quality-guidelines.md) | Code standards, testing requirements, forbidden patterns | Current |
| [Logging Guidelines](./logging-guidelines.md) | Plugin logging format, levels, and boundaries | Current |

---

## Pre-Development Checklist

Before changing backend code, read the guides that match the task instead of relying on generic .NET habits.

### Always read

- [Directory Structure](./directory-structure.md) - where code belongs
- [Quality Guidelines](./quality-guidelines.md) - required patterns, tests, and review checks

### Read when the task touches API contracts

- [Error Handling](./error-handling.md) - status codes, error payloads, and response boundaries
- `docs/REST_API.md` - public endpoint, response, and compatibility contract

Additional API rules for this repository:

- keep success responses focused on `data`; do not add frontend-facing success `message` fields
- keep `error.message` limited to the effective reason; do not turn it into frontend display copy such as "动作 + 结果"
- preserve raw response field names and documented payload structure

### Read when the task touches persistence or config contracts

- [Database Guidelines](./database-guidelines.md) - TShock data access and JSON persistence rules
- `docs/CONFIGURATION.md` - file names, JSON shape, and fallback behavior

### Read when the task touches observability or failure handling

- [Logging Guidelines](./logging-guidelines.md) - logger entry point, level usage, and safe message style
- [Error Handling](./error-handling.md) - boundary catches and client-safe failures

### Quick release checklist

Before considering backend work done:

- [ ] New code is placed in the correct layer
- [ ] Routes, permissions, and error codes stay centralized
- [ ] API response shape still matches `docs/REST_API.md`
- [ ] Config / persistence changes still match `docs/CONFIGURATION.md`
- [ ] Logs use `PluginLogger` rather than handwritten formatting
- [ ] Tests were added or updated where behavior changed

---

## Scope Notes

- The plugin exposes REST endpoints through TShock's `Rests` integration, not ASP.NET controllers.
- Persistent plugin-owned state is stored in JSON files, not in a project-owned migration system.
- Existing backend examples are concentrated in `Plugin/`, `Rest/`, `Services/`, `Infrastructure/`, and `Models/`.

---

**Language**: All documentation should be written in **English**.
