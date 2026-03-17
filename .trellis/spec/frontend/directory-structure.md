# Directory Structure

> How frontend code is organized in this project.

---

## Overview

There is currently **no frontend directory structure** in this repository.

Confirmed from the repository layout:

- no `src/` directory
- no `app/` directory
- no frontend package manifest such as `package.json`
- no frontend framework files or component trees

The repository is organized around:

- `NextBotAdapter/` for plugin backend code
- `NextBotAdapter.Tests/` for automated tests
- `docs/` for API and configuration contracts

---

## Directory Layout

```text
Repository root
├── NextBotAdapter/
├── NextBotAdapter.Tests/
├── docs/
├── .trellis/
└── AGENTS.md
```

There is currently no frontend module tree to follow.

---

## Module Organization

If future work introduces a frontend, define this file from the real module layout after the code exists.

Until then:

- do not create speculative frontend folders just to satisfy a template
- do not document invented feature-module boundaries
- do not assume a web stack for this plugin repository

---

## Naming Conventions

No frontend naming convention is established yet because no frontend code exists.

---

## Examples

The absence of frontend code is itself the current project convention.

Reference files showing the repository focus:

- `NextBotAdapter/Plugin/NextBotAdapterPlugin.cs`
- `NextBotAdapter/Rest/UserEndpoints.cs`
- `docs/REST_API.md`
