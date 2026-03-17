# Frontend Development Guidelines

> Frontend guidance for this repository.

---

## Overview

This repository currently does **not** contain an application frontend. There is no `src/`, `app/`, React, Vue, or other browser UI layer in the codebase.

The project is currently a backend-oriented .NET 9 TShock plugin with supporting docs and tests.

---

## Guidelines Index

| Guide | Description | Status |
|-------|-------------|--------|
| [Directory Structure](./directory-structure.md) | Frontend directory expectations for this repository | Current |
| [Component Guidelines](./component-guidelines.md) | Component expectations if a frontend is introduced later | Current |
| [Hook Guidelines](./hook-guidelines.md) | Hook expectations if a React frontend is introduced later | Current |
| [State Management](./state-management.md) | State expectations if a frontend is introduced later | Current |
| [Quality Guidelines](./quality-guidelines.md) | Frontend quality notes for this repository | Current |
| [Type Safety](./type-safety.md) | Frontend type-safety notes for this repository | Current |

---

## Important Note

Do not invent frontend architecture, component conventions, or React-specific patterns in implementation work unless the repository actually gains a frontend codebase.

If a frontend is added in the future, update these files based on the real code rather than copying generic web guidance.

---

**Language**: All documentation should be written in **English**.
