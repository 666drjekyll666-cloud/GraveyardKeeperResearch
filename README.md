# Graveyard Keeper Performance Diagnostics

Private cross-mod research repository for diagnosing freezes, microfreezes, stalls, and performance regressions in **Graveyard Keeper 1.407**.

This repository stores diagnosis and evidence. Production fixes belong in the repository that owns the proven defect.

## Four-layer working model

1. **Global engineering baseline** — `666drjekyll666-cloud/DevRules` (`ENGINEERING_RULES.md`, `CI_POLICY.md`, `GIT_WORKFLOW.md`, `PROJECT_BOOTSTRAP.md`).
2. **Repository-local contract** — `AGENTS.md` in this repository.
3. **ChatGPT Project rules** — `docs/CHATGPT_PROJECT_INSTRUCTIONS.md`; copy this into the ChatGPT Project instructions field.
4. **Fresh-chat bootstrap** — `docs/CHAT_START_PROMPT.md`; use this as the first message when starting a new diagnostic chat.

## Durable evidence

- `docs/PERFORMANCE_EVIDENCE.md` — accepted facts, hypotheses, ruled-out causes, root causes, cross-project conclusions.
- `docs/TEST_LOG.md` — controlled runtime tests and supplied diagnostic evidence.

The intended workflow is:

`reproduce -> isolate -> measure/verify -> root cause -> narrow fix in owning repo -> retest`

Do not commit copied game assemblies, full decompiled game source, or extracted proprietary assets.
