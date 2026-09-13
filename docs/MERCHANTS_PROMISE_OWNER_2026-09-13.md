# The Merchant's Promise — owner isolation evidence (2026-09-13)

Focused evidence record for the characteristic residual ~0.68–0.75 s GC-mediated gameplay freeze in Graveyard Keeper 1.407.

This document records the final owner-isolation stage. The immediate stall mechanism remains the already accepted Unity/Mono Boehm GC execution path. The purpose here is to identify the upstream mod owner, not to claim a source-level root cause before the binary/source is inspected.

## Fixed conditions

- Same developed save and ordinary gameplay route used in prior grouped isolation.
- Diagnostic: GK Frame Spike Probe 0.4.0.
- Unity incremental GC enabled, 3 ms target slice.
- No F6 GC hold.
- No intentional sleep/save during the comparison.
- Target class: ~0.68–0.75 s wall time, overwhelmingly Unity-main-thread CPU, `incremental_pending=True`.

## Preceding narrowing

Earlier controlled splits narrowed the sufficient reproducing set from the full modpack to:

- The Merchant's Promise 1.0.0
- Food & Drink Rebalance 1.2.0
- Better Save Soul Rebalance 1.1.1

That three-mod set reproduced the target almost immediately at **701.34 ms wall / 703.13 ms main-thread CPU / 100% CPU**, managed heap **555.2 MB**.

Food & Drink Rebalance 1.2.0 alone plus the probe was then negative for the target class during a broad ordinary-gameplay interval. The run exercised gathering, ordinary craft/activity events, Witch Hill crossings, NPC schedule changes, bat spawn/pathfinding, craft GUIs, tool work, and combat; the largest ordinary steady-state probe event was about **125.88 ms**.

## Merchant + Better Soul pair — positive

Enabled ordinary plugins:

- The Merchant's Promise 1.0.0
- Better Save Soul Rebalance 1.1.1

Plus GK Frame Spike Probe 0.4.0.

Result: **positive**.

Direct capture:

- `FRAME SPIKE #5`
- wall: **713.43 ms**
- Unity-main-thread CPU: **718.75 ms**
- CPU share: **100%**
- `gc_cycle_delta=0`
- `incremental_pending=True`
- managed heap: **538.5 MB**
- focused, `timeScale=1`

No Merchant trade was being processed at the captured moment; the nearby ordinary mushroom/berry gathering and zone logs are not treated as causal merely from adjacency.

Interpretation: Food & Drink Rebalance is not necessary for this reproduction. The sufficient owner set narrowed to Merchant vs Better Soul (or their interaction).

## Better Save Soul Rebalance alone — negative

Enabled ordinary plugin:

- Better Save Soul Rebalance 1.1.1

Plus GK Frame Spike Probe 0.4.0.

Result: **negative for the characteristic class during the tested interval**.

The run exercised ordinary gathering, Witch Hill traversal, NPC schedules, furnace completion, axe work, extensive bat spawn/pathfinding, craft GUI interaction, and a normal manual craft. No ~0.7 s steady-state event occurred. Post-start events topped out around **114 ms**; a completed GC-counter advance later measured about **109.35 ms**, not the target class.

Interpretation: Better Save Soul Rebalance is not shown sufficient by itself. Its broad `CraftComponent` hooks are therefore not accepted as the single-owner explanation.

## The Merchant's Promise alone — positive

Enabled ordinary plugin:

- The Merchant's Promise 1.0.0

Plus GK Frame Spike Probe 0.4.0.

BepInEx confirmed exactly `2 plugins to load`.

Result: **positive; single-plugin sufficient owner established**.

Direct capture:

- `FRAME SPIKE #1`
- wall: **686.88 ms**
- Unity-main-thread CPU: **656.25 ms**
- non-CPU wall: **30.63 ms**
- CPU share: **95.5%**
- frame: **4420**
- focused: `True`
- `timeScale=1`
- `gc_cycle_delta=0`
- `incremental_pending=True`
- Unity GC slice: **3,000,000 ns**
- managed heap: **549.8 MB**
- no GC hold active

The characteristic event occurred during ordinary movement/zone activity after gathering a mushroom and berries. No Merchant trade transaction was active.

## Accepted owner conclusion

**Observed fact:** The Merchant's Promise 1.0.0 alone, with no other ordinary gameplay mod present, is sufficient to reproduce the characteristic residual ~0.7 s GC-mediated gameplay freeze.

Therefore:

- the grouped mod-owner search is complete;
- Better Save Soul Rebalance, Food & Drink Rebalance, Day Wheel Quest Markers, and the other removed mods are not necessary for this target reproduction;
- The Merchant's Promise is the upstream **owner of the sufficient reproducing condition** for the current investigation.

This does **not yet identify the source-level root cause**. The already proven immediate mechanism remains Unity/Mono Boehm GC execution. The unresolved source question is what The Merchant's Promise 1.0.0 changes or retains such that GC can consume ~0.7 s during unrelated ordinary gameplay.

## Current external/source evidence

The public Nexus page for The Merchant's Promise says:

- the mod was uploaded 2026-07-27 as version 1;
- it adjusts Merchant crate payout according to banked marketing/fame points;
- it also scales the trade report while that window is open;
- it 'hooks one method and identifies it by structure rather than by name';
- it claims the hook only adjusts payout calculation and is safe to add/remove.

The runtime log identifies the installed target as the compiler-generated method:

`FlowCanvas.Nodes.Flow_ProcessMerchantTraiding+<>c__DisplayClass0_0::<RegisterPorts>b__0`

No public GitHub source repository for this mod was found through the connected GitHub search or ordinary web search. Nexus publishes the binary but its page does not expose source code. The Nexus permissions page also states that modification/reupload requires the author's permission, so no production modification of the third-party binary should be attempted without source/permission.

A recurring Unity message `Fallback handler could not load library .../Mono/data-XXXXXXXX.dll` appears immediately around Merchant hook installation in the tested logs. This is recorded only as an observation, **not** as causal evidence: Unity can emit analogous `data-*` fallback messages for dynamically generated/non-platform assemblies, and external Unity reports explicitly describe such messages as potentially harmless.

## Next narrow question

Inspect the exact installed `MerchantsPromise` 1.0.0 assembly to establish:

1. which hook/detour library and API it uses;
2. whether the hook object/delegate is strongly retained for the intended lifetime;
3. what reflection/IL/structural method-discovery objects are retained after startup;
4. whether startup discovery or detour installation creates a large persistent managed graph;
5. whether the trade hook can be replaced by a narrower Harmony patch or other implementation that preserves behavior without the GC regression.

Until the exact DLL/source is inspected, candidates such as RuntimeDetour lifetime, reflection retention, or dynamic-method machinery remain **hypotheses**, not root cause.

## Immediate remediation

Disabling The Merchant's Promise 1.0.0 removes the only mod proven necessary for the single-plugin reproduction and is the current safe workaround while source-level diagnosis continues.
