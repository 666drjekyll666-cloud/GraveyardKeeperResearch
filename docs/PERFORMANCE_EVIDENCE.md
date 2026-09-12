# Performance Evidence

Canonical evidence ledger for the Graveyard Keeper 1.407 freeze/microfreeze investigation.

Keep observed facts, hypotheses, root causes, and accepted results separate. Do not promote a hypothesis merely because it sounds plausible.

## Accepted facts

- Target game version: **Graveyard Keeper 1.407**.
- The investigated environment is a modded PC installation using BepInEx and multiple independently maintained mods.
- The user reports recently appearing intermittent gameplay microfreezes/stalls.
- The symptom is especially noticeable during dialogue, but is not limited to dialogue.
- Cross-mod diagnosis belongs in this repository; production fixes belong in the repository that owns the proven defect.
- The investigation has already separated at least one Day Wheel Quest Markers performance defect from the remaining sporadic baseline hitches; therefore no single mod is accepted as the cause of all reported freezes.
- A previously supplied runtime probe found six references/caller paths capable of reaching `Resources.UnloadUnusedAssets()`, including `PlatformSpecific.SaveGameDataToSlot`, `SubsceneLoadManager.UnloadLastScene`, `SubsceneLoadManager.UnloadAllScenes`, `SleepGUI`, `WaitingGUI`, and Save Now-related code.
- One previously supplied runtime cleanup sample reported `Loaded Objects now: 860406` and a **611.2889 ms** total cleanup, including **558.5640 ms** in `MarkObjects`. This proves that Unity unused-asset cleanup can be large enough in this installation to produce a visible main-thread-class stall. It does not by itself prove which trigger owns every observed microfreeze.
- In the user's ordinary Save Now configuration, `Auto Save = False`, `Save On New Day = False`, and `Backup Saves On Save = False`. Therefore timed Save Now autosaves are not part of the ordinary steady-state baseline in which the residual random microfreezes were reported.
- A deliberate one-minute Save Now calibration reproduced three near-identical save-triggered stalls: **772.9579 ms**, **759.0385 ms**, and **769.1783 ms** Unity unused-asset cleanup, with roughly 949k–955k loaded objects and ~695–710 ms spent in `MarkObjects`. A normal sleep/save in the same run produced **792.6656 ms**. Save-triggered stalls are therefore a confirmed separate hitch class.

## Active hypotheses

### Remaining sporadic gameplay hitches

Day Wheel Quest Markers 1.0.28 removed the previously rhythmic roughly-30-second freezes, but the user still observed roughly two or three random short hitches over several minutes. A control run with Day Wheel removed produced a comparable two or three random hitches over a similar interval.

The one-minute Save Now calibration later proved that saves can generate ~0.75–0.8 s stalls, but this does **not** explain the ordinary residual symptom because timed autosave is disabled in the normal profile. The user also reports freezes outside autosave events.

Therefore the remaining sporadic hitch class is still open. Day Wheel and timed Save Now autosave are not attributed as its owner without new evidence.

The next useful question is which recurring/high-frequency runtime path remains active in ordinary gameplay and can generate isolated main-thread spikes without an explicit save. Priority should be given to periodic scans, high-frequency Harmony patches, repeated hierarchy searches/reflection/enumeration, scene/subscene cleanup triggers, or mod-owned synchronous I/O that are actually reachable in the user's baseline configuration.

### Scene/subscene unload cleanup

The previous IL/runtime probe found `SubsceneLoadManager.UnloadLastScene` and `SubsceneLoadManager.UnloadAllScenes` among direct `Resources.UnloadUnusedAssets()` callers. Because the save-path explanation is now separated from the ordinary baseline, these scene/subscene cleanup paths remain eligible suspects for non-save stalls if they can be shown to run during ordinary movement/dialogue/location transitions.

No root-cause status is assigned yet: a caller reference alone is insufficient. The next source/runtime step is to identify when these methods are invoked in normal gameplay and whether their invocation coincides with the residual hitch class.

### Keeper's Lantern bounded global-scan fallbacks

Direct source inspection of accepted `KeepersLantern` 1.0.12 found no `Resources.UnloadUnusedAssets` caller. It does contain bounded fallback/global lookups that deserve context-sensitive scrutiny if a future hitch occurs while those fallbacks are active:

- world-camera binding can call `Resources.FindObjectsOfTypeAll<Camera>()` on a one-second retry cadence until the gameplay camera is resolved and cached;
- time-object resolution can call `Resources.FindObjectsOfTypeAll(_timeType)` while its cached object is absent;
- dungeon practical-light compatibility fallback calls `Resources.FindObjectsOfTypeAll<Light>()` at most every five seconds, but only while in the procedural dungeon and only when the native `DynamicLights` lists cannot be resolved.

These are **not** accepted as causes of the general dialogue/gameplay symptom. The dungeon fallback is structurally irrelevant outside the dungeon, and the other scans are expected to stop after successful binding. They become suspects only if runtime evidence shows repeated rebinding/fallback activation at a freeze timestamp.

## Ruled-out causes

- **Day Wheel Quest Markers as the sole cause of the remaining random microfreezes:** ruled out by the 1.0.28 control comparison. With the recurring Day Wheel defect removed, a similar low count of random short hitches remained both with Day Wheel present and with Day Wheel removed.
- **Repeated FlowCanvas graph parsing as a normal gameplay requirement for Day Wheel after the persistent-manifest line:** ruled out by 1.0.26+ architecture and runtime evidence. 1.0.28 loaded the manifest behind loading in 6.16 ms, skipped FlowCanvas parsing, performed no runtime structural rebuild, and used cheap rebinds for later NPC discoveries.
- **Timed Save Now autosave as the trigger for the ordinary baseline random hitches:** ruled out by configuration state. Autosave is disabled in the user's ordinary profile; the one-minute autosaves were introduced only for diagnostic calibration.
- **Direct `Resources.UnloadUnusedAssets` call in the inspected current source of `BetterSaveSoulRebalance`, `SpecializedStorage`, or `KeepersLantern`:** no such caller is present in the inspected production source. This does not rule out the game itself, Unity internals, or third-party mods as the owner of any observed cleanup pass.
- **Save Now's explicit `GC.Collect()` + `Resources.UnloadUnusedAssets()` exit code as the ordinary gameplay trigger:** current upstream source confines that explicit pair to the Exit To Desktop path. Normal Save Now auto/manual/new-day saves instead call `PlatformSpecific.SaveGame`, so any cleanup during those saves must be attributed to the downstream game save path unless separate evidence proves otherwise.

## Proven root causes

### Day Wheel Quest Markers: first-weekday-NPC synchronous structural build

Owner: `666drjekyll666-cloud/DayWheelQuestMarkers`.

Fresh-game testing of 1.0.25 measured a **302.22 ms** runtime structural rebuild at the first Bishop/weekday-NPC introduction. Source review identified the intended loading prewarm gate using the wrong `game_starting` polarity, so the verified graph-ready loading window was skipped on fresh saves.

1.0.26 moved static structural discovery behind loading into a persistent manifest. Player testing confirmed the first Bishop introduction no longer produced the ~302 ms runtime structural rebuild; subsequent launches loaded the manifest in about 5.95 ms and later NPC discoveries used rebind only.

Status: root cause confirmed for this specific hitch class; superseded implementation line, not accepted as a standalone stable release.

### Day Wheel Quest Markers: recurring steady-state allocation pressure

Owner: `666drjekyll666-cloud/DayWheelQuestMarkers`.

1.0.27 still produced an approximately 0.5 s hitch roughly every 30–60 seconds. Source audit found recurring avoidable allocation in the steady-state validation paths:

- once-per-second known-NPC count check constructed a dictionary;
- 30-second known-NPC signature check allocated a `List<string>`, sorted it, converted it to an array, and joined a new string;
- runtime validity used reflective invocation with a new argument array every second.

1.0.28 replaced these with non-allocating count/fingerprint/live-player checks while preserving reminder semantics and manifest format. Player A/B result: the previous rhythmic roughly-30-second freezes disappeared. The remaining random short hitches occurred at a comparable rate in the Day-Wheel-removed control.

Status: root cause confirmed and performance objective confirmed for the rhythmic Day Wheel component. Stable public baseline in the owning repository remains 1.0.24 until that repository's runtime/functional acceptance line is completed.

### Save-triggered Unity unused-asset cleanup

Owner/trigger split:

- trigger can be any path that invokes the game's save pipeline, including manual save, sleep/new-day save, or deliberately enabled Save Now autosave;
- the heavy cleanup itself is in the game/Unity `PlatformSpecific.SaveGameDataToSlot -> Resources.UnloadUnusedAssets()` path, not Save Now's ordinary timed-save code;
- Save Now's role during the calibration was only to provide a repeatable timer trigger.

Three consecutive one-minute autosaves produced **772.9579 ms**, **759.0385 ms**, and **769.1783 ms** cleanup; a sleep/save produced **792.6656 ms**. The dominant component was `MarkObjects`, around 695–732 ms with roughly 949k–955k loaded objects.

Status: root cause confirmed for save-triggered stalls. This result is **not** the root cause of the ordinary residual random hitch class because the ordinary configuration has timed autosave disabled.

## Cross-project findings

### Day Wheel Quest Markers

Owning repository: `666drjekyll666-cloud/DayWheelQuestMarkers`.

Relevant source/test line:

- 1.0.25: first Bishop still triggered a 302.22 ms structural rebuild; wrong loading-gate polarity diagnosed.
- 1.0.26: persistent manifest removed gameplay FlowCanvas parsing; first-Bishop hitch eliminated, but intermittent hitches remained.
- 1.0.27: manifest moved to `BepInEx/cache/DayWheelQuestMarkers/`; rhythmic 30–60 s hitch still observed; recurring allocation sources identified in source.
- 1.0.28: allocation-free steady-state checks; rhythmic freezes disappeared; random residual hitches matched the control with Day Wheel removed.
- 1.0.29/1.0.30 continue functional nested-dialogue reachability work and are not evidence that Day Wheel owns the remaining random hitch class.

The Day Wheel performance work demonstrates that the original user symptom was composite: at least one reproducible rhythmic component belonged to Day Wheel, while another sporadic component remains after that defect is removed.

### Current owned-mod source inspection

Inspected accepted production source of:

- `666drjekyll666-cloud/BetterSaveSoulRebalance`;
- `666drjekyll666-cloud/SpecializedStorage`;
- `666drjekyll666-cloud/KeepersLantern`.

No direct `Resources.UnloadUnusedAssets` caller was found in these production sources. `SpecializedStorage` is event/UI-bound rather than a recurring global-resource cleanup owner; its recipe suitability cache is built lazily and retained. `BetterSaveSoulRebalance` applies deterministic data patches / narrow craft hooks and does not contain Unity resource cleanup calls. `KeepersLantern` has the bounded global-scan fallbacks recorded above, so it remains eligible for a targeted runtime check only when their activation conditions match a freeze.

This source inspection narrows ownership but does not identify the owner of a Unity cleanup pass that might be initiated by the base game or a third-party mod whose source is outside the owned repositories.

### p1xel8ted save-path source inspection

Current upstream `p1xel8ted/Graveyard-Keeper-Mods` source was inspected for Save Now and Alchemy Research Redux.

Save Now:

- timed auto-save is configurable;
- auto-save, manual save, and new-day save all call `PlatformSpecific.SaveGame`;
- the source includes debug messages around auto-save start/completion that were used for the calibration;
- its explicit `GC.Collect()` + `Resources.UnloadUnusedAssets()` is in the Exit To Desktop branch, not the normal timed save branch.

Alchemy Research Redux:

- patches `PlatformSpecific.SaveGame` with a postfix;
- every save calls `AlchemyRecipe.SaveRecipesToFile()` and `LastMix.SaveToFile()`;
- both use synchronous JSON serialization/file writes; `LastMix.SaveToFile()` directly uses `File.WriteAllText`, and the recipe persistence path does the same.

The calibration shows that the dominant observed stall is the downstream Unity cleanup, not enough evidence to blame the small additional Alchemy JSON writes. No production fix in Save Now or Alchemy Research Redux is justified for this save-stall class from current evidence.

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
