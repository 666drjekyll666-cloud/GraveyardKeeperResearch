# Diagnostic Test Log

Chronological record of controlled runtime tests, supplied logs, probes, and accepted conclusions for the Graveyard Keeper performance investigation.

## Recording rule

For each meaningful test, record:

- date;
- question being tested;
- exact comparison/control;
- relevant game/mod/config state;
- evidence supplied or generated;
- observed result;
- interpretation;
- status: `inconclusive`, `supports hypothesis`, `rules out`, `root cause confirmed`, or `accepted after retest`;
- next step only when one remains.

Do not record routine repository inspection as an in-game test. Do not rewrite an old result after later evidence changes the interpretation; append the correction so the diagnostic history remains auditable.

## Tests

### 2026-09-12 — Day Wheel Quest Markers 1.0.25 fresh-game structural rebuild

- Question: can newly discovered weekday NPCs be handled without reparsing the six weekday FlowCanvas graphs during gameplay?
- Comparison/control: 1.0.25 first-NPC rebind candidate against the earlier unified-cache behavior.
- Evidence: owning-repository runtime log and source audit.
- Observed result: first Bishop/weekday-NPC introduction still triggered a **302.22 ms** synchronous structural rebuild; later NPC discoveries used cheap rebinding only.
- Interpretation: the rebind model was valid, but the loading prewarm window was missed because `game_starting` polarity was wrong.
- Status: `root cause confirmed` for the first-weekday-NPC hitch.
- Next step taken: 1.0.26 persistent-manifest candidate moved structural discovery to the verified loading window.

### 2026-09-12 — Day Wheel Quest Markers 1.0.26 persistent manifest

- Question: does removing gameplay FlowCanvas parsing eliminate the measured first-NPC hitch?
- Comparison/control: 1.0.26 versus 1.0.25 on fresh/current saves.
- Evidence: owning-repository runtime logs and test record.
- Observed result: the first Bishop introduction no longer produced the previous ~302 ms structural rebuild. A subsequent launch loaded the manifest behind loading in **5.95 ms** with canonical counts; later NPC discoveries used rebind only. User reported a substantial drop in noticeable freezes, but intermittent hitches remained.
- Interpretation: synchronous gameplay structural parsing was a real contributor but not the sole source of all microfreezes.
- Status: `supports hypothesis` / partial performance fix; not final acceptance.
- Next step taken: preserve manifest architecture and inspect remaining recurring steady-state work.

### 2026-09-12 — Day Wheel Quest Markers 1.0.27 recurring hitch audit

- Question: why does an approximately 0.5 s hitch still appear roughly every 30–60 seconds after graph parsing is removed from gameplay?
- Comparison/control: source/runtime inspection of the 1.0.27 steady-state path.
- Evidence: source audit plus supplied short-run log.
- Observed result: no runtime graph rebuild in the captured interval; source still performed avoidable recurring allocations: per-second dictionary construction for known-NPC count, 30-second list/sort/array/string signature construction, and per-second reflective invocation with argument-array allocation.
- Interpretation: Day Wheel still had a plausible rhythmic allocation/GC-pressure defect independent of FlowCanvas parsing.
- Status: `supports hypothesis` pending A/B runtime confirmation.
- Next step taken: 1.0.28 replaced those paths with allocation-free checks.

### 2026-09-12 — Day Wheel Quest Markers 1.0.28 allocation-free A/B

- Question: are the recurring Day Wheel allocations responsible for the rhythmic roughly-30-second freezes, and does a residual hitch class remain without Day Wheel?
- Comparison/control: 1.0.28 allocation-free candidate versus the preceding 1.0.27 behavior, plus a control run with Day Wheel removed over a similar interval.
- Evidence: owning-repository runtime log and user observation.
- Observed result: the previous rhythmic roughly-30-second freezes **disappeared**. The 1.0.28 log loaded the persistent manifest in **6.16 ms**, skipped FlowCanvas graph parsing, showed no runtime structural rebuild, and later NPC discoveries used cheap rebinds. Roughly two or three random short hitches still occurred over several minutes; the Day-Wheel-removed control produced a comparable two or three random hitches over a similar interval.
- Interpretation: recurring Day Wheel allocation pressure was causal for the rhythmic component. The remaining sporadic hitch class exists independently at the tested baseline and is not attributable to Day Wheel from this evidence.
- Status: `root cause confirmed` for the rhythmic Day Wheel component; `rules out` Day Wheel as the sole owner of the residual random hitches.
- Next step: investigate the remaining random hitch class cross-mod/game-wide rather than continuing to optimize Day Wheel without new evidence.

### 2026-09-13 — Save Now one-minute autosave calibration

- Question: can the game's save path produce a visible stall of the same broad magnitude as the reported freezes, and does Save Now's timed autosave explain the ordinary residual symptom?
- Comparison/control: temporarily enabled Save Now debug logging and changed the autosave interval from 10 minutes to 1 minute for a short controlled run; ordinary profile state before the diagnostic change had `Auto Save = False`, `Save On New Day = False`, and `Backup Saves On Save = False`.
- Evidence: supplied runtime log plus user observation.
- Observed result: each forced autosave visibly froze. Three consecutive one-minute autosaves were followed by Unity unused-asset cleanups of **772.9579 ms**, **759.0385 ms**, and **769.1783 ms**, with roughly 949k–955k loaded objects and ~695–710 ms spent in `MarkObjects`. A normal sleep/save path in the same log produced another **792.6656 ms** cleanup with ~949k loaded objects.
- Interpretation: the save path is a confirmed, highly reproducible source of ~0.75–0.8 s stalls in this installation. However the timed Save Now autosave cannot explain the original ordinary steady-state symptom because autosave was disabled in the normal configuration. The user also reports that residual freezes occur outside autosave events. Treat save-related stalls as a separate known hitch class, not the root cause of the remaining random microfreezes.
- Status: `root cause confirmed` for save-triggered stalls; `rules out` Save Now timed autosave as the ordinary recurring trigger under the user's baseline configuration.
- Next step: inspect recurring/high-frequency runtime paths in the remaining mod set and correlate only new candidates with the residual non-save hitch class.

### 2026-09-13 — GK Frame Spike Probe 0.1.0 handoff

- Question: do the remaining non-save gameplay frame spikes coincide with Mono GC collections, or are they long frames with no collection event?
- Rationale: static source audit of the current owned mods and the accessible p1xel8ted runtime mods did not reveal another generic half-second dialogue/gameplay hot path; the previous unused-assets probe observed runtime cleanup at save/return-to-menu events rather than ordinary dialogue/walking.
- Diagnostic behavior: normal-frame work is restricted to one `Stopwatch.GetTimestamp()` and `GC.CollectionCount(0..2)` sample per `Update`; no Unity object scans, stack traces, hierarchy enumeration, synchronous I/O, or per-frame logging. It ignores the first 30 realtime seconds and logs only intervals >= 50 ms. On a spike only, it additionally records Unity unscaled delta, frame number, focus/timeScale, GC generation deltas, and managed heap size.
- Source branch: `research/frame-spike-probe`.
- Frozen diagnostic source: `diagnostic/frame-spike-probe-0.1.0` at `7cf1aa6a62f3503fb4d70ba81cb7ee46480f3794`.
- Build evidence: GitHub Actions run `34721589397` on `ubuntu-latest`; restore, Release build, hash step, and artifact upload all succeeded. The first run `34721538967` failed during restore before compilation because `nuget.config`/the BepInEx feed was absent; this was an infrastructure configuration failure, not a source compile failure.
- Artifact: `GKFrameSpikeProbe.dll`.
- SHA-256: `705acfc354c19878267ab5ec2bc73ac11a9c99420adb7c1e61e509d06a8b11fc`.
- Status: `inconclusive` until runtime capture.
- Next step: run the ordinary problematic gameplay scenario with Save Now returned to its normal autosave-off configuration and this probe added; submit the resulting BepInEx log plus whether/when visible non-save hitches were noticed.

### 2026-09-13 — GK Frame Spike Probe 0.1.0 runtime capture

- Question: do user-perceived residual non-save freezes require a managed Mono GC collection?
- Scenario: ordinary gameplay with Save Now autosave disabled; user reported two characteristic freezes, one in/around the house and a second while mining coal, and supplied the log immediately after the second event.
- Evidence: `GK Frame Spike Probe (Diagnostic) 0.1.0` runtime log.
- Observed result: two large steady-state gameplay spikes stand out after load. `FRAME SPIKE #16` measured **711.93 ms**, `focused=True`, `timeScale=1`, managed heap **528.1 MB**, with `gc0=+1 gc1=+1 gc2=+1`. It occurred in the house-area portion of the run and is the strongest temporal match to the user's first reported freeze, although the user did not timestamp that first event exactly. `FRAME SPIKE #17` measured **695.26 ms**, `focused=True`, `timeScale=1`, managed heap **613.9 MB**, with **`gc0=+0 gc1=+0 gc2=+0`** while the player was repeatedly mining coal. The log was submitted immediately after this second perceived freeze, so #17 is the direct correlation target.
- Additional negative evidence: no `Resources.UnloadUnusedAssets` / `Unloading ... unused Assets` runtime cleanup, save event, scene load, or focus transition occurs around #17. The spike is logged before the subsequent coal depletion/replacement/drop lines, so the coal object's completion itself is not established as the trigger.
- Interpretation at the time: managed GC was treated as not necessary because `GC.CollectionCount` did not advance on #17.
- Status at the time: `rules out` "all residual freezes are Mono GC".
- Later correction: this interpretation is superseded by the Boehm-counter semantics entry below. The raw measurement remains valid, but `gc0/gc1/gc2=+0` does not prove absence of incremental GC work.

### 2026-09-13 — GK Frame Spike Probe 0.2.0 handoff

- Question: during a residual non-GC ~0.7 s stall, is the Unity main thread actively consuming CPU or spending most of the wall interval blocked/waiting/descheduled?
- Diagnostic delta from 0.1.0: preserves the same wall-clock and GC sampling and adds Windows `GetThreadTimes` for the current Unity main thread. Spike-only output adds `main_thread_cpu_ms`, `non_cpu_wall_ms`, and `cpu_share_pct`. No object scans, hierarchy enumeration, stack traces, synchronous file I/O, or per-frame log writes were added.
- Development branch: `research/frame-spike-probe-v0.2`.
- Frozen diagnostic source: `diagnostic/frame-spike-probe-0.2.0` at `e67d2240eae00ad03a64d43481d1a5f517ab809a`.
- Build evidence: GitHub Actions run `34722251988` on `ubuntu-latest`; restore, Release build, hash, and artifact upload succeeded.
- Artifact: `GKFrameSpikeProbe.dll`.
- SHA-256: `3cb6a6efcf3c2aa16be9679b06e78fc9616ef9c3c774b9979bec7ec78f7c3151`.
- Status: `inconclusive` until runtime capture.
- Next step: replace 0.1.0 with 0.2.0, leave the normal mod/config baseline unchanged, and capture another characteristic freeze. A high `cpu_share_pct` will prioritize CPU hot-path instrumentation; a low share will prioritize blocking/I/O/scheduler/native-wait investigation.

### 2026-09-13 — GK Frame Spike Probe 0.2.0 runtime capture

- Question: is the directly correlated residual ~0.7 s freeze CPU-bound on the Unity main thread or mostly blocked/waiting/descheduled?
- Scenario: ordinary gameplay with normal Save Now autosave-off configuration; user reported three characteristic freezes, including a severe third freeze while walking on the road near Witch Hill immediately before sending the log.
- Evidence: `GK Frame Spike Probe (Diagnostic) 0.2.0` runtime log plus immediate user correlation.
- Observed result: the directly correlated third freeze was `FRAME SPIKE #13`: **692.09 ms wall**, **687.50 ms main-thread CPU**, **4.59 ms non-CPU wall**, **99.3% CPU share**, `focused=True`, `timeScale=1`, `gc0/gc1/gc2=+0`, managed heap **590.3 MB**. Immediately preceding logs include `witch_hill_down_zone_big_R` entry/FlowScript activity. Separate sleep/save spikes were also captured and matched the already-proven `Resources.UnloadUnusedAssets` save class. A sleep-time control interval measured **257.97 ms wall / 31.25 ms CPU / 12.1% CPU share**, demonstrating that the CPU-time classifier can distinguish waiting/descheduling from active main-thread CPU work.
- Interpretation: the characteristic road freeze is decisively **CPU-bound on the Unity main thread**, not primarily synchronous I/O wait, scheduler descheduling, or a blocked native wait. The adjacent Witch Hill zone is a trigger candidate but not yet causal because the probe samples between `Update` calls rather than attributing CPU time to the last log line.
- Status: `rules out` blocked/waiting/descheduled as the dominant mechanism for this directly correlated event; `supports hypothesis` of a main-thread CPU hot path.
- Next step taken: re-evaluate GC counter semantics before instrumenting a game/zone CPU path.

### 2026-09-13 — Boehm GC counter semantics correction

- Question: does `GC.CollectionCount(0..2)=+0` prove that no managed GC work occurred during a spike in Unity 2020/Mono?
- Evidence: Mono Boehm backend implementation and Boehm collection-counter semantics; durable note `docs/GC_COUNTER_NOTE.md`.
- Observed result: Mono's Boehm backend implements `mono_gc_collection_count(int generation)` with the single `GC_get_gc_no()` counter; the generation argument is ignored. Boehm increments that counter once per completed collection. Incremental marking work can therefore occur before the counter advances.
- Interpretation: `gc0/gc1/gc2=+1` confirms a completed-collection-counter advance; `+0` proves only no such counter advance during the interval and **does not prove zero GC work**. The earlier 0.1 interpretation that #17 was a proven non-GC stall is too strong and is superseded.
- Status: `accepted fact`.
- Next step taken: probe 0.3.0 adds Unity incremental-GC state and a zero-budget `CollectIncremental(0)` query on spike-only paths.

### 2026-09-13 — GK Frame Spike Probe 0.3.0 handoff

- Question: when a characteristic ~0.7 s CPU spike has no Boehm cycle-count advance, is Unity incremental GC enabled and is incremental collection work still pending immediately afterward?
- Diagnostic delta from 0.2.0: startup/spike-only logging adds `GarbageCollector.isIncremental`, `GarbageCollector.GCMode`, `incrementalTimeSliceNanoseconds`, `gc_cycle_delta`, and `CollectIncremental(0)` result as `incremental_pending`. No positive GC time budget is requested by the probe.
- Development source commit: `a7e01dd74a83e7106490617c9a6bfe91259815ce`.
- Frozen diagnostic source: `diagnostic/frame-spike-probe-0.3.0` at `750ddc0cb1760233d2b632fb712380bc999cbc70`.
- Build evidence: GitHub Actions run `34722920364` on `ubuntu-latest`; restore, Release build, hash, and artifact upload succeeded.
- Artifact ID: `10306378806`.
- Artifact ZIP SHA-256: `39f37ba29b484464e4abac990e43b9ca86a83a2515693bc6f93053546d56684f`.
- DLL SHA-256: `eac9a0f2bed63d7b955c7bab7fdd880c7f0032c8aaf663b7f4a3c3e9799516ef`.
- Status: `inconclusive` until runtime capture.
- Next step: reproduce a characteristic freeze, preferably around the repeatable Witch Hill route, and submit the log immediately.

### 2026-09-13 — GK Frame Spike Probe 0.3.0 runtime capture

- Question: does a directly correlated characteristic ~0.7 s CPU freeze occur while Unity incremental GC is active and still has work pending?
- Scenario: ordinary gameplay; user installed 0.3.0, walked toward/through the Witch Hill lower-road route, perceived a characteristic freeze, and immediately supplied the log.
- Evidence: supplied 0.3.0 runtime log.
- Observed result: probe startup reports `unity_gc_incremental=True`, `unity_gc_mode=Enabled`, `unity_gc_slice_ns=3000000`. The directly correlated `FRAME SPIKE #2` measured **714.88 ms wall**, **703.13 ms main-thread CPU**, **11.76 ms non-CPU wall**, **98.4% CPU share**, `gc_cycle_delta=0`, managed heap **560.6 MB**, and `incremental_pending=True`. The spike appears immediately after `witch_hill_down_zone_big_R` enter/FlowScript logging. Earlier in the same run a **51.76 ms** spike also had `incremental_pending=True`, so pending state is not unique to the severe event.
- Interpretation: a live Unity incremental GC cycle definitely spans the characteristic severe freeze, and zero Boehm cycle delta does not exclude GC. However `incremental_pending=True` alone does **not** prove that GC consumed the full ~703 ms because pending work can coexist with unrelated CPU work. Witch Hill remains a strong temporal trigger candidate but not a proven owner.
- Status: `supports hypothesis` that GC is the common mechanism behind the ~0.69–0.75 s residual freezes; not yet `root cause`.
- Next step: perform a short controlled A/B with GC completely disabled while traversing the same route.

### 2026-09-13 — GK Frame Spike Probe 0.4.0 handoff

- Question: does the characteristic ~0.7 s Witch-Hill-area freeze still occur when Unity garbage collection is completely disabled for a very short controlled window?
- Diagnostic delta from 0.3.0: pressing **F6** after the probe arms starts a bounded **15-second** `GarbageCollector.GCMode=Disabled` window. The previous GC mode is captured and automatically restored on timeout, a second F6 press, focus loss, or plugin destruction. The probe does **not** call `GC.Collect()` when restoring. Spike logs add `gc_hold_active` and remaining hold time; hold start/end logs include managed-memory growth and collection-counter delta.
- Safety rationale: Unity documents `GCMode.Disabled` as completely disabling the collector and warns that memory grows while it is disabled. The 15-second automatic limit and focus-loss restoration constrain that risk; this is diagnostic-only behavior, not a proposed production fix.
- Development branch: `research/frame-spike-probe-v0.4`.
- Frozen diagnostic source: `diagnostic/frame-spike-probe-0.4.0` at `8e9b988e50a6b66825b9bbfd37287e905b5fce55`.
- Build evidence: GitHub Actions run `34723457626` on `ubuntu-latest`; restore succeeded, Release build succeeded with **0 warnings / 0 errors**, hash step succeeded, artifact upload succeeded.
- Artifact ID: `10306519308`.
- Artifact ZIP SHA-256: `cc791c55238e3d3eb35dbdfa47cae6ed2dd5c05d2323381791ebca15321e5594`.
- DLL SHA-256: `a906a045adb41a3fa85946f44c4a6d1f89cc68aaeac434f7d11b79845712c420`.
- Status: `inconclusive` until controlled runtime A/B.
- Next step: replace 0.3.0 with 0.4.0, keep all other mods/config unchanged, reach the Witch Hill lower-road reproduction area, press F6, immediately traverse the same `big_L`/`big_R` boundary repeatedly during the 15-second GC-disabled window, then after `[GC HOLD END]` traverse it again with GC restored. Submit the log immediately after a characteristic freeze or after the enabled/disabled/enabled comparison is complete.

### 2026-09-13 — GK Frame Spike Probe 0.4.0 first GC-disabled A/B

- Question: is the characteristic residual ~0.7 s stall suppressed while Unity GC is disabled and restored when GC resumes?
- Scenario: normal 32-plugin baseline, same Witch Hill lower-road route, one 15-second F6 hold.
- Evidence: supplied 0.4.0 runtime log plus immediate user report of the characteristic freeze on the return walk.
- Observed result: the hold started with `incremental_pending_before=True`, `gc_cycle=137`, managed heap **474.7 MB**. The player crossed `skull_back_zone`, `witch_hill_down_zone_big_L`, and `witch_hill_down_zone_big_R` while GC was disabled; no characteristic ~0.7 s spike was logged during the hold. The hold timed out with `gc_cycle_delta=0`, managed heap **492.9 MB**, growth **18.2 MB**, and restored `GCMode=Enabled`. Immediately after restore, `FRAME SPIKE #1` measured **686.95 ms wall / 687.50 ms main-thread CPU / 100% CPU share**, `gc_cycle_delta=0`, `incremental_pending=True`, managed heap **560.2 MB**.
- Instrumentation check: 0.4.0 calls `ResetSample()` after restoring the GC mode, so the 686.95 ms interval cannot include time accumulated inside the disabled window or the mode-switch call itself.
- Interpretation: the result strongly supports GC execution as the causal mechanism. It also weakens `big_R` as a deterministic direct ~0.7 s CPU callback because the same route executes during `GCMode=Disabled` without the characteristic stall.
- Status: `supports hypothesis`; replication requested because the underlying event is intermittent.
- Next step taken: repeat the same F6 A/B without changing mods/config.

### 2026-09-13 — GK Frame Spike Probe 0.4.0 replicated GC-disabled A/B

- Question: does the GC-disabled suppression / GC-enabled recurrence pattern reproduce, including under unrelated gameplay CPU activity?
- Scenario: unchanged 32-plugin baseline and same route; two independent 15-second F6 holds in one run.
- Evidence: supplied 0.4.0 runtime log.
- Observed result, first hold: started with `incremental_pending_before=True`, `gc_cycle=138`, managed heap **453.6 MB**. The player crossed `big_L` and `big_R` with no characteristic stall. The hold timed out with only **2.2 MB** managed growth and no cycle-count advance. After restore, `FRAME SPIKE #2` measured **685.61 ms wall / 687.50 ms CPU / 100% CPU share**, `GCMode=Enabled`, `incremental_pending=True`, managed heap **535.1 MB**.
- Observed result, second hold: started with `incremental_pending_before=True`, `gc_cycle=140`, managed heap **465.0 MB**. During `GCMode=Disabled`, the game performed NPC despawns/transitions, bat spawning, pathfinding, and another `witch_hill_down_zone_big_R` traversal. Logged spikes were only **73.00 ms** (85.6% CPU) and **75.97 ms** (100% CPU), both explicitly with `gc_hold_active=True` / `unity_gc_mode=Disabled`. The hold timed out after **18.6 MB** managed growth and no cycle-count advance. After restore, `FRAME SPIKE #5` measured **681.91 ms wall / 671.88 ms CPU / 98.5% CPU share**, `GCMode=Enabled`, `incremental_pending=True`, managed heap **546.1 MB**.
- Interpretation: across three controlled GC-off windows in two runs, the characteristic ~0.68–0.75 s stall is suppressed while GC is disabled and returns after GC is restored, while the same route and other gameplay CPU work continue. This is sufficient to accept Unity/Mono GC execution as the **immediate causal mechanism** for the residual characteristic freeze class. The first replicated hold accumulated only 2.2 MB, so a large artificial 15-second allocation backlog is not required for recurrence after restore.
- Scope: this does **not** identify the upstream allocation/retention owner and therefore does not close the overall project root cause. The owner may be the base game/current save-state heap, one mod, several mods, or an interaction.
- Status: `accepted after retest` for GC as the immediate causal mechanism; upstream owner/root cause remains open.
- Next step: run a near-clean control with the same save/location and probe, keeping only BepInEx plus the diagnostic probe active and avoiding sleep/save. If the same GC-mediated ~0.7 s class remains, investigate base-game/save-state heap behavior; if it disappears, begin grouped/binary mod isolation.
