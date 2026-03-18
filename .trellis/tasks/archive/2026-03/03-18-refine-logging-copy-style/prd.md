# Refine logging copy style

## Goal
Refine and standardize backend log wording across the NextBotAdapter plugin so logs read more professional, concise, and natural while preserving useful operational context.

## Requirements
- Unify existing backend log wording across plugin initialization, configuration, whitelist, reload, REST, and shutdown-related paths.
- Use a professional and concise tone with natural Chinese sentence flow.
- Keep logs readable for humans while preserving useful operational context.
- Do not change the shared `PluginLogger` output format unless necessary.
- Do not add unnecessary new logs; focus on improving wording consistency and quality.
- Preserve existing log levels unless a wording review clearly reveals a level mismatch.
- Avoid exposing sensitive data.
- Keep the shutdown logging fix intact.
- Update tests only if wording-sensitive tests exist or new wording-sensitive coverage is needed.

## Acceptance Criteria
- [ ] Existing backend log messages follow a consistent professional style.
- [ ] Initialization, config, whitelist, REST, and critical error logs use natural, concise wording.
- [ ] No unnecessary logging noise is introduced.
- [ ] Existing shutdown disposal behavior remains safe.
- [ ] Build and tests pass.

## Technical Notes
- Reuse the current `PluginLogger` wrapper.
- Prioritize message wording changes over structural logging changes.
- Keep failure logs explicit about the reason.
