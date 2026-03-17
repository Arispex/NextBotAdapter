# State Management

> How state is managed in this project.

---

## Overview

There is currently no frontend state-management layer in this repository.

The runtime state that exists today is backend plugin state, for example:

- in-memory whitelist state inside `PersistedWhitelistService`
- plugin configuration loaded from JSON files by `WhitelistConfigService`
- TShock / Terraria runtime state accessed through service adapters

Those patterns are backend concerns and are documented in the backend guidelines, not here.

---

## State Categories

Frontend state categories are not applicable because no frontend exists.

---

## When to Use Global State

Not applicable in the current codebase.

---

## Server State

Not applicable in the current codebase.

If a frontend is later added to consume `docs/REST_API.md`, document caching and synchronization rules based on the chosen stack.

---

## Common Mistakes

- Do not invent Redux, Zustand, React Query, or similar conventions for a repository that has no frontend.
- Do not confuse backend plugin state with frontend UI state.
- If a frontend is introduced later, replace this placeholder with rules derived from real code.
