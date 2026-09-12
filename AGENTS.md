# Graveyard Keeper Performance Diagnostics — Working Contract

This repository follows the canonical global development rules in `666drjekyll666-cloud/DevRules`.

Before substantive technical work, read:

- `ENGINEERING_RULES.md`
- `CI_POLICY.md`
- `GIT_WORKFLOW.md`
- `PROJECT_BOOTSTRAP.md`

This local `AGENTS.md` contains only project-specific additions, constraints, and explicit exceptions.

## Project identity

- Project: **Graveyard Keeper Performance Diagnostics**
- Repository: `666drjekyll666-cloud/GraveyardKeeperResearch`
- Game: **Graveyard Keeper 1.407**
- Environment: modded PC installation using BepInEx and multiple independently maintained mods
- Purpose: identify, prove, and eliminate intermittent freezes, microfreezes, stalls, excessive loading work, and performance regressions affecting the user's current Graveyard Keeper setup.

This is a diagnostic/research project, not a general optimization mod and not the canonical production repository for other mods.

## Scope

The primary workflow is:

`reproduce symptom -> isolate trigger/owner -> measure or establish evidence -> identify root cause -> fix in the owning repository -> retest the original scenario`

The project may investigate:

- vanilla/game-engine behavior;
- BepInEx/plugin loading and lifecycle interactions;
- one mod in isolation;
- interactions between multiple mods;
- excessive allocations, object creation, resource scans, reflection, logging, serialization, polling, Harmony patch behavior, scene/HUD recreation, loading-time work, and event storms;
- regressions that appeared after recent mod or configuration changes.

Do not turn this repository into a grab-bag production mod. If a proven defect belongs to a specific mod, implement the production fix in that mod's own repository under its own `AGENTS.md`, versioning, test-build log, and acceptance rules. Record the cross-project diagnosis here and link the owning source state/fix.

## Mandatory start-of-work checks

Before substantive diagnostic or code/GitHub work:

1. inspect the current `GraveyardKeeperResearch` repository and read this `AGENTS.md`;
2. read the current global DevRules contract;
3. inspect `docs/PERFORMANCE_EVIDENCE.md` and `docs/TEST_LOG.md` for already established findings and prior A/B tests;
4. inspect the current repository/source/history of every mod that becomes a concrete suspect before attributing a defect or changing it;
5. read that suspect repository's local `AGENTS.md` before substantive work in it;
6. prefer accepted existing evidence over repeating expensive or intrusive tests.

Do not start from chat memory when repository evidence can answer the question.

## Evidence contract

Never guess Graveyard Keeper or mod internals.

A performance claim must distinguish clearly between:

- **observed fact** — directly present in logs, measurements, source, runtime evidence, or a controlled A/B test;
- **working hypothesis** — plausible but not yet proven;
- **root cause** — supported strongly enough that a targeted change or controlled test predicts the result;
- **accepted result** — confirmed by the user's real game environment when runtime confirmation is required.

Preferred evidence order:

1. reproducible symptom and controlled comparison;
2. current source/configuration/history of the suspected owner;
3. existing logs and accepted project diagnostics;
4. direct assembly/resource/runtime inspection;
5. narrow instrumentation or probe;
6. user-supplied in-game evidence when only the running game can prove the behavior.

Do not infer causality merely because a warning/error appears near a freeze in a log. Establish temporal or behavioral correlation and, where practical, an A/B result.

## Diagnostic method

Prefer the cheapest test that can materially narrow the cause.

Typical progression:

1. define the exact symptom and a short reproducible scenario;
2. preserve the current mod/config baseline;
3. compare against the nearest meaningful control, such as clean game, BepInEx-only, or the same setup with one suspected mod disabled;
4. use grouped/binary isolation when many mods are plausible suspects;
5. inspect source before adding instrumentation when the likely hot path is already visible;
6. if instrumentation is needed, make it narrow, bounded, and removable;
7. measure frequency and duration rather than relying only on subjective severity when practical;
8. remove/disable temporary diagnostics after the question is answered.

Avoid broad per-frame logging, stack dumping on every call, global object enumeration in recurring paths, or diagnostics that can create the very stalls being investigated.

When comparing test runs, change one material variable at a time unless the test is explicitly a coarse isolation step.

## Performance-specific rules

Treat frame-time and loading-time problems separately.

For runtime microfreezes, inspect first for work that can block the main Unity thread, including:

- broad `Resources.FindObjectsOfTypeAll` / `FindObjectsOfType` scans;
- repeated hierarchy searches;
- repeated FlowCanvas/quest graph parsing;
- frequent reflection or enumeration of large collections;
- allocations and LINQ in recurring hot paths;
- synchronous file I/O;
- excessive logging;
- repeated object/component creation/destruction;
- expensive Harmony prefixes/postfixes on high-frequency methods;
- accidentally multiplied subscriptions/timers after scene or save reloads.

These are suspect categories, not assumptions of guilt. Verify them in the actual source/runtime before concluding.

For startup/loading stalls, distinguish one-time prewarm/cache construction from recurring gameplay cost. A large one-time operation is not automatically a microfreeze root cause unless it occurs during the reported gameplay scenario or repeats unexpectedly.

## Cross-repository fix ownership

Once a root cause is assigned to a specific mod:

- switch to that mod's current repository and read its current `AGENTS.md` plus relevant docs/history;
- create the appropriate dev/fix branch according to that repository's workflow;
- implement only the proven/narrow fix there;
- use that mod's version and handoff rules;
- do not merge runtime changes to its stable branch without its required acceptance;
- record the diagnostic link/result back in this repository.

A finding in this repository does not override another repository's project contract.

## Git / version / acceptance workflow

This research repository is evidence-first and normally does not distribute a stable DLL.

- `main` may contain accepted research documentation, diagnostic scripts, and non-production tooling.
- Use `research/<topic>` for substantial experiments or temporary probes when separation improves rollback/review.
- Documentation-only evidence updates may go directly to `main` when no runtime artifact is affected.
- Do not consume production mod version numbers here.
- If a diagnostic DLL/probe must be handed to the user, give it an explicit diagnostic identity, freeze its exact source state, record its purpose and hash in `docs/TEST_LOG.md`, and never present it as a normal mod release.
- Production fixes and numbered releases belong to the owning mod repository.

## CI / resource policy

Hosted CI is normally unnecessary for this repository because most work is research, logs, docs, static inspection, and controlled in-game testing.

Do not add automatic CI or spend hosted runner minutes for routine research/bookkeeping. Use hosted CI only when a concrete diagnostic tool or reproducible build artifact genuinely needs compilation in a hosted environment and no cheaper equivalent proves the required property.

For a production mod fix, follow that mod repository's own CI policy rather than inventing a parallel build pipeline here.

## Proprietary game material

Do not commit copied Graveyard Keeper assemblies, decompiled game source trees, extracted proprietary assets, or other copyrighted game payloads to this repository.

Direct inspection of locally available game binaries/resources may be used as evidence when permitted by the working environment, but preserve only the minimum derived technical facts, identifiers, hashes, notes, or diagnostic output needed for reproducibility.

## Long-lived sources of truth

Consult and maintain:

- `AGENTS.md` — this project-specific contract;
- `docs/PERFORMANCE_EVIDENCE.md` — verified facts, active hypotheses, ruled-out causes, and cross-project conclusions;
- `docs/TEST_LOG.md` — controlled test history and supplied runtime evidence;
- `docs/CHATGPT_PROJECT_INSTRUCTIONS.md` — text for the ChatGPT Project instructions field;
- `docs/CHAT_START_PROMPT.md` — canonical prompt for starting a fresh diagnostic chat;
- the current repositories and local contracts of any mods under investigation.

When chat memory conflicts with accepted repository evidence, investigate the conflict before changing code or asking the user to repeat a test.
