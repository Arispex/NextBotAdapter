# Quality Guidelines

> Code quality standards for frontend development.

---

## Overview

There is currently no frontend implementation in this repository, so there are no frontend lint, build, test, or review rules derived from real UI code.

Until a frontend exists, the correct quality rule is: **do not invent frontend conventions that the repository does not use**.

---

## Forbidden Patterns

- Do not assume a React, Vue, Next.js, or other web framework stack exists here.
- Do not add speculative frontend quality rules that are unsupported by actual code.
- Do not describe nonexistent commands such as frontend lint, bundle, or component test commands.

---

## Required Patterns

- If future work adds a frontend, update this file based on the real stack and real tooling.
- Keep frontend guidance aligned with actual repository structure and scripts, not generic expectations.

---

## Testing Requirements

There are currently no frontend tests because there is no frontend code.

---

## Code Review Checklist

If a change claims to touch frontend concerns in the current repository, reviewers should first verify whether a frontend actually exists. Right now, the repository's active implementation surface is backend plugin code plus docs and tests.
