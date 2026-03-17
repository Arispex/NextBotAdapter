# Hook Guidelines

> How hooks are used in this project.

---

## Overview

There are two meanings of "hook" relevant to this repository:

1. **Frontend hooks** such as React custom hooks - these do not exist in the current codebase
2. **Plugin / framework hooks** such as TShock or Terraria event handlers - these exist, but they are backend plugin hooks, not frontend hooks

This frontend guideline file only covers frontend hooks, so it is currently mostly not applicable.

For plugin lifecycle and event hooks, refer to backend code such as `NextBotAdapter/Plugin/NextBotAdapterPlugin.cs`.

---

## Custom Hook Patterns

No frontend custom hooks exist yet.

---

## Data Fetching

No frontend data-fetching layer exists yet.

The current integration surface is the plugin REST API documented in `docs/REST_API.md`.

---

## Naming Conventions

No frontend hook naming convention is established yet.

---

## Common Mistakes

- Do not interpret TShock event registration as a frontend hook pattern.
- Do not add React hook conventions to this repository unless a React frontend is actually introduced.
- If a frontend is added later, document hook rules from the real code and data-fetching stack.
