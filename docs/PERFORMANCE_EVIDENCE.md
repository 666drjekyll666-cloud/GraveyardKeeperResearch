# Performance Evidence

Canonical evidence ledger for the Graveyard Keeper 1.407 freeze/microfreeze investigation.

Keep observed facts, hypotheses, root causes, and accepted results separate. Do not promote a hypothesis merely because it sounds plausible.

## Accepted facts

- Target game version: **Graveyard Keeper 1.407**.
- The investigated environment is a modded PC installation using BepInEx and multiple independently maintained mods.
- The user reports recently appearing intermittent gameplay microfreezes/stalls.
- The symptom is especially noticeable during dialogue, but is not limited to dialogue.
- No single mod, BepInEx, or vanilla game subsystem is accepted as the root cause at project bootstrap.
- Cross-mod diagnosis belongs in this repository; production fixes belong in the repository that owns the proven defect.

## Active hypotheses

None accepted at bootstrap. Add hypotheses only when they are tied to a concrete observation and state explicitly what evidence would confirm or falsify them.

## Ruled-out causes

None recorded yet.

## Proven root causes

None recorded yet.

## Cross-project findings

None recorded yet.

When a cause is assigned to another repository, record:

- owning repository and exact source/version involved;
- symptom/reproduction scenario;
- evidence establishing ownership/root cause;
- fix branch/commit/version when created;
- user's retest result;
- final accepted status.

## Measurement notes

When practical, record:

- scenario and save/location;
- mod/config set;
- what changed relative to the control;
- freeze frequency and approximate duration or measured frame/main-thread time;
- relevant log timestamps;
- whether the run was startup/loading or steady-state gameplay;
- whether diagnostic instrumentation itself could perturb timing.

## Evidence hygiene

Do not commit proprietary game binaries, full decompiled source trees, or extracted game assets. Preserve only the minimum derived facts and outputs needed to reproduce the diagnosis.
