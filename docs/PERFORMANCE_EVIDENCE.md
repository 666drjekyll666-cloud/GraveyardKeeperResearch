# Performance Evidence

Canonical evidence ledger for the Graveyard Keeper 1.407 freeze/microfreeze investigation.

Keep observed facts, hypotheses, root causes, and accepted results separate. Do not promote a hypothesis merely because it sounds plausible.

## Accepted facts

- Target game version: **Graveyard Keeper 1.407**.
- The investigated environment is a modded PC installation using BepInEx and multiple independently maintained mods.
- The user reports recently appearing intermittent gameplay microfreezes/stalls, especially noticeable during dialogue but not limited to dialogue.
- Cross-mod diagnosis belongs in this repository; production fixes belong in the repository that owns the proven defect.
- The original symptom is composite: Day Wheel Quest Markers had two independently proven hitch classes that were removed, while a separate sporadic residual class remained in control runs.
- Save-triggered stalls are a separate proven class. The game save path can invoke `Resources.UnloadUnusedAssets()` and produce roughly **0.75–0.8 s** stalls dominated by `MarkObjects`; normal Save Now timed autosave is disabled in the ordinary profile and does not explain the residual steady-state class.
- `GC.CollectionCount(0..2)` on this Unity/Mono Boehm backend is a single completed-collection counter repeated for all three generation arguments. A zero delta does **not** prove that no incremental GC work occurred. See `docs/GC_COUNTER_NOTE.md`.
- `GK Frame Spike Probe 0.2.0` directly classified a characteristic Witch-Hill-road freeze as **692.09 ms wall / 687.50 ms Unity-main-thread CPU / 99.3% CPU share**. Blocking, synchronous I/O wait, scheduler descheduling, or a native wait is therefore not the dominant mechanism for that captured event.
- `GK Frame Spike Probe 0.3.0` confirmed that Unity incremental GC is enabled (`isIncremental=True`, `GCMode=Enabled`, target slice **3 ms**) and that a directly correlated **714.88 ms wall / 703.13 ms main-thread CPU / 98.4% CPU** freeze occurred while an incremental GC cycle was still pending afterward (`incremental_pending=True`).
- `witch_hill_down_zone_big_R` is a repeatable temporal correlate, but repeated passes through the same zone can complete without a characteristic ~0.7 s freeze. The zone/FlowScript is therefore not accepted as a deterministic direct CPU root cause.
- A near-clean control using the same developed save with **only BepInEx plus GK Frame Spike Probe 0.4.0** loaded produced no characteristic ~0.68–0.75 s steady-state freeze during an extended ordinary gameplay run. After `OnGameStartedPlaying`, the probe logged 12 spikes from **52.40 ms to 131.68 ms**; the large 1.3–2.5 s events were confined to save loading. The run included repeated `witch_hill_down_zone_big_L` / `big_R` crossings, dialogue, vendor UI, tool work, NPC schedule activity, bat/slime spawning and pathfinding. This strongly disfavors the base game/current save alone as a sufficient explanation.
- Grouped binary isolation narrowed the residual class to **The Merchant's Promise 1.0.0**. The mod reproduced the target class by itself with only the diagnostic probe present: **686.88 ms wall / 656.25 ms Unity-main-thread CPU / 95.5% CPU**, `incremental_pending=True`, managed heap **549.8 MB**. No Merchant trade was active at the captured moment.
- Food & Drink Rebalance 1.2.0 alone did not reproduce the characteristic class during the tested interval, and Better Save Soul Rebalance 1.1.1 alone likewise did not reproduce it despite broad ordinary gameplay coverage.

## Confirmed causal mechanism for the residual ~0.7 s class

`GK Frame Spike Probe 0.4.0` introduced a bounded 15-second A/B window in which `GarbageCollector.GCMode` is set to `Disabled`, then automatically restored to its prior mode. The probe resets its wall/CPU sampling baseline after restore, so a spike logged after `[GC HOLD END]` cannot contain time accumulated inside the disabled window or the mode-switch call itself.

Across **three controlled GC-disabled windows in two runtime runs**, the same pattern repeated:

1. A live incremental cycle was already pending before the hold (`incremental_pending_before=True`).
2. While GC was `Disabled`, the player traversed the same Witch Hill `big_L` / `big_R` route without the characteristic ~0.68–0.75 s freeze.
3. In the second replication, the disabled window also contained active NPC transitions, bat spawning/pathfinding, and a `big_R` crossing. The largest logged spikes were only **73.00 ms** and **75.97 ms**, both explicitly with `unity_gc_mode=Disabled`.
4. After automatic restore to `Enabled`, the characteristic CPU stall returned repeatedly:
   - **686.95 ms wall / 687.50 ms CPU / 100% CPU**, `incremental_pending=True`;
   - **685.61 ms wall / 687.50 ms CPU / 100% CPU**, `incremental_pending=True`;
   - **681.91 ms wall / 671.88 ms CPU / 98.5% CPU**, `incremental_pending=True`.
5. The first window of the replication accumulated only **2.2 MB** managed-memory growth while GC was disabled, yet the ~685 ms stall still appeared after restore; therefore a large artificial 15-second allocation backlog is not required to reproduce the event.

**Accepted interpretation:** Unity/Mono garbage-collector execution is a **confirmed immediate causal mechanism** for the characteristic residual ~0.68–0.75 s freeze class in this installation. This is stronger than temporal correlation: disabling GC suppresses the characteristic event during the controlled window and restoring GC repeatedly allows the same main-thread CPU stall to recur.

The upstream sufficient mod owner is now isolated to **The Merchant's Promise 1.0.0**. The source-level mechanism inside that third-party mod remains unknown and is intentionally not pursued further because the user will simply stop using the mod.

## Owner isolation — closed

The grouped isolation sequence established:

- near-clean control: negative;
- Split A: negative;
- Split B: positive;
- B2: positive;
- C1 (`Merchant + Food + Better Soul`): positive;
- Food alone: negative during the tested interval;
- `Merchant + Better Soul`: positive at **713.43 ms wall / 718.75 ms main-thread CPU / 100% CPU**;
- Better Soul alone: negative during a broad tested interval, with post-start events topping out around **114 ms**;
- Merchant alone: positive at **686.88 ms wall / 656.25 ms main-thread CPU / 95.5% CPU**.

**Accepted owner conclusion:** The Merchant's Promise 1.0.0 is sufficient by itself to create the conditions under which the characteristic residual GC freeze reproduces. This closes the mod-owner search for this hitch class.

**Practical remediation:** disable/remove The Merchant's Promise 1.0.0. No fork, binary modification, replacement implementation, or further source-level investigation is planned unless the user explicitly reopens the issue.

If the same characteristic ~0.7 s freeze later reproduces with The Merchant's Promise absent, treat that as new contradictory runtime evidence and reopen isolation from the then-current mod baseline rather than assuming every future hitch belongs to this owner.

### Witch Hill zone / FlowScript

The Witch Hill boundary remains useful as a short reproduction route, but current A/B evidence weakens the hypothesis that the zone callback itself consumes ~0.7 s. The same zone executes while GC is disabled and in the near-clean mod-free control without producing the characteristic stall. It may still merely provide convenient timing for reproduction; no direct ownership is assigned.

### Scene/subscene unload cleanup

`SubsceneLoadManager.UnloadLastScene` and `UnloadAllScenes` remain eligible only for stalls that actually coincide with `Resources.UnloadUnusedAssets()`. The directly correlated residual road/coal events do not show that cleanup and belong to the GC-mediated residual class described above.

## Ruled-out / separated explanations

- **Day Wheel Quest Markers as the sole owner of the remaining random microfreezes:** ruled out by control comparison after its rhythmic allocation defect was fixed and by positive reproduction without Day Wheel.
- **Day Wheel 1.0.30 navigation-evaluator allocation cleanup as the missing fix:** a speculative 1.0.31 candidate removed recurring reflection argument-array/boxing and phrase-enumeration churn without changing reminder semantics, yet the normal 32-plugin run still produced characteristic **726.22 ms / 99.0% CPU** and **710.76 ms / 98.9% CPU** steady-state spikes. Do not promote 1.0.31 as a performance fix for the residual class.
- **Repeated Day Wheel FlowCanvas graph parsing in normal gameplay:** ruled out by the persistent-manifest architecture and runtime evidence.
- **Food & Drink Rebalance 1.2.0 as a required owner of the target class:** ruled out as necessary because `Merchant + Better Soul` reproduced without it; Food alone also did not reproduce during the tested interval.
- **Better Save Soul Rebalance 1.1.1 as a required owner of the target class:** ruled out as necessary because Merchant alone reproduced; Better Soul alone did not reproduce during the tested interval.
- **Timed Save Now autosave as the trigger for the ordinary residual class:** ruled out by normal configuration and controlled save calibration.
- **Blocking/waiting/descheduling as the dominant mechanism of the characteristic residual freeze:** ruled out by ~95–100% main-thread CPU share in multiple directly correlated captures.
- **`Resources.UnloadUnusedAssets` / save cleanup as the direct trigger of the characteristic road/coal residual class:** no such cleanup is present around the directly correlated events.
- **Witch Hill `big_R` FlowScript as a deterministic ~0.7 s direct CPU callback:** contradicted by successful `big_R` traversals while GC is disabled, by earlier same-run passes without the characteristic spike, and by repeated near-clean `big_L`/`big_R` traversals without the characteristic class.

## Proven root causes already closed

### Day Wheel Quest Markers: first-weekday-NPC synchronous structural build

Owner: `666drjekyll666-cloud/DayWheelQuestMarkers`.

Fresh-game testing of 1.0.25 measured a **302.22 ms** runtime structural rebuild at the first Bishop/weekday-NPC introduction. Source review found the loading prewarm gate using the wrong `game_starting` polarity. 1.0.26 moved static structural discovery behind loading into a persistent manifest and eliminated that hitch.

Status: root cause confirmed for this specific hitch class.

### Day Wheel Quest Markers: recurring steady-state allocation pressure

Owner: `666drjekyll666-cloud/DayWheelQuestMarkers`.

1.0.27 retained recurring avoidable allocations in once-per-second / 30-second validation paths. 1.0.28 replaced them with allocation-free checks. Player A/B testing confirmed the rhythmic roughly-30-second freezes disappeared while a separate sporadic baseline remained.

Status: root cause confirmed for the rhythmic Day Wheel component.

### Save-triggered Unity unused-asset cleanup

The game save pipeline can reach `Resources.UnloadUnusedAssets()`. Controlled one-minute Save Now triggering produced **772.9579 ms**, **759.0385 ms**, and **769.1783 ms** cleanup stalls; a normal sleep/save produced **792.6656 ms**. Dominant time was `MarkObjects` with roughly 949k–955k loaded Unity objects.

Status: root cause confirmed for save-triggered stalls; separate from the ordinary residual GC-mediated class.

### The Merchant's Promise: residual ~0.7 s GC freeze owner

Owner: third-party mod **The Merchant's Promise 1.0.0**.

Controlled single-plugin isolation reproduced the characteristic event with only The Merchant's Promise and the diagnostic probe loaded: **686.88 ms wall / 656.25 ms main-thread CPU / 95.5% CPU**, `incremental_pending=True`, managed heap **549.8 MB**. Better Save Soul alone and Food & Drink Rebalance alone did not reproduce the target during their tested intervals.

Status: **owner confirmed / practical issue closed by disabling the mod**. Source-level internal root cause inside the third-party mod was not established and is intentionally left uninvestigated.

## Source-inspection notes retained

- Current inspected production source of `BetterSaveSoulRebalance`, `SpecializedStorage`, and `KeepersLantern` contains no direct `Resources.UnloadUnusedAssets` caller.
- Save Now's explicit `GC.Collect()` + `Resources.UnloadUnusedAssets()` pair is confined to Exit To Desktop in the inspected upstream source; ordinary saves call the game's save pipeline instead.
- Rest In Patches 0.1.5 `Smooth Player Movement` contains a player-only `GetComponentInChildren<SortingGroup>()` lookup, but current evidence does not assign it ownership of the ~0.7 s class.
- Keeper's Lantern contains bounded fallback scans, but their activation conditions do not match a generic explanation for the residual class and they are not accepted as causal without runtime correlation.

## Measurement notes

When practical, record scenario/save/location, exact mod/config set, the single changed variable, measured wall/main-thread CPU time, GC mode/state, relevant log timestamps, and whether instrumentation itself could perturb the result.

## Evidence hygiene

Do not commit proprietary game binaries, full decompiled source trees, or extracted game assets. Preserve only the minimum derived facts and outputs needed to reproduce the diagnosis.
