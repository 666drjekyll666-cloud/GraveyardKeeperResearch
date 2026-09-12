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
