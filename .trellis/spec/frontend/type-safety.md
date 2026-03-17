# Type Safety

> Type safety patterns in this project.

---

## Overview

There is currently no frontend type system in use because the repository has no frontend codebase.

The only active typed implementation in this repository is the backend C# code under `NextBotAdapter/`. Frontend-specific TypeScript conventions do not exist yet and should not be fabricated.

---

## Type Organization

Not applicable in the current codebase.

If a frontend is introduced later, document where UI types live only after the real structure exists.

---

## Validation

Not applicable in the current codebase.

There is currently no frontend runtime validation library or schema layer.

---

## Common Patterns

No frontend type patterns are established yet.

---

## Forbidden Patterns

- Do not document TypeScript-specific rules for a repository that has no TypeScript frontend.
- Do not assume shared frontend / backend generated types exist.
- If a frontend is added later, update this file from the actual implementation rather than generic templates.
