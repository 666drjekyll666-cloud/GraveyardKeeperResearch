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

This is **not yet the final project root cause**, because the upstream owner is still unknown. The unresolved question is what creates the heap/allocation/marking conditions that make Boehm consume roughly 0.7 s on the Unity main thread: base game/save state, one mod, several mods, or an interaction between them.

## Active hypotheses / next isolation question

### Allocation / heap-pressure owner

Possible owner classes remain:

- base game / current save-state managed object graph;
- one mod with sustained or bursty managed allocation;
- several mods whose allocations combine into the threshold condition;
- a mod or game path that retains objects and enlarges the graph Boehm must mark, even if its instantaneous allocation rate is modest.

The next cheapest high-information test is a **near-clean runtime control** using the same save/location and the same 0.4.0 probe, but with ordinary gameplay mods removed and only BepInEx plus the diagnostic probe active. Do not sleep/save during this control. The result separates game/save-state GC behavior from mod-driven pressure before any per-mod instrumentation is added.

- If the same ~0.7 s GC-enabled stall remains at comparable frequency/magnitude, prioritize base-game/save-state heap investigation.
- If it disappears or changes dramatically, perform grouped/binary mod isolation, keeping dependency families together, until the allocation owner is narrowed.

### Witch Hill zone / FlowScript

The Witch Hill boundary remains useful as a short reproduction route, but current A/B evidence weakens the hypothesis that the zone callback itself consumes ~0.7 s. The same zone executes while GC is disabled without producing the characteristic stall. It may still contribute allocations or merely provide a convenient location/timing for reproduction; no ownership is assigned without further evidence.

### Scene/subscene unload cleanup

`SubsceneLoadManager.UnloadLastScene` and `UnloadAllScenes` remain eligible only for stalls that actually coincide with `Resources.UnloadUnusedAssets()`. The directly correlated residual road/coal events do not show that cleanup and belong to the GC-mediated residual class described above.

## Ruled-out / separated explanations

- **Day Wheel Quest Markers as the sole owner of the remaining random microfreezes:** ruled out by control comparison after its rhythmic allocation defect was fixed.
- **Repeated Day Wheel FlowCanvas graph parsing in normal gameplay:** ruled out by the persistent-manifest architecture and runtime evidence.
- **Timed Save Now autosave as the trigger for the ordinary residual class:** ruled out by normal configuration and controlled save calibration.
- **Blocking/waiting/descheduling as the dominant mechanism of the characteristic residual freeze:** ruled out by ~98–100% main-thread CPU share in multiple directly correlated captures.
- **`Resources.UnloadUnusedAssets` / save cleanup as the direct trigger of the characteristic road/coal residual class:** no such cleanup is present around the directly correlated events.
- **Witch Hill `big_R` FlowScript as a deterministic ~0.7 s direct CPU callback:** contradicted by successful `big_R` traversals while GC is disabled and by earlier same-run passes without the characteristic spike. It remains only a possible upstream allocator/trigger candidate.

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

## Source-inspection notes retained

- Current inspected production source of `BetterSaveSoulRebalance`, `SpecializedStorage`, and `KeepersLantern` contains no direct `Resources.UnloadUnusedAssets` caller.
- Save Now's explicit `GC.Collect()` + `Resources.UnloadUnusedAssets()` pair is confined to Exit To Desktop in the inspected upstream source; ordinary saves call the game's save pipeline instead.
- Rest In Patches 0.1.5 `Smooth Player Movement` contains a player-only `GetComponentInChildren<SortingGroup>()` lookup, but current evidence does not assign it ownership of the ~0.7 s class.
- Keeper's Lantern contains bounded fallback scans, but their activation conditions do not match a generic explanation for the residual class and they are not accepted as causal without runtime correlation.

## Measurement notes

When practical, record scenario/save/location, exact mod/config set, the single changed variable, measured wall/main-thread CPU time, GC mode/state, relevant log timestamps, and whether instrumentation itself could perturb the result.

## Evidence hygiene

Do not commit proprietary game binaries, full decompiled source trees, or extracted game assets. Preserve only the minimum derived facts and outputs needed to reproduce the diagnosis.
