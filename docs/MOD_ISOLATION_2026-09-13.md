# Mod Isolation Matrix — 2026-09-13

Working record for grouped/binary isolation of the characteristic residual ~0.68–0.75 s GC-mediated gameplay freeze.

This file records only controlled runtime split results. The canonical causal-mechanism conclusions remain in `docs/PERFORMANCE_EVIDENCE.md`; final accepted isolation conclusions should also be folded into `docs/TEST_LOG.md`.

## Fixed diagnostic conditions

- Game: Graveyard Keeper 1.407.
- Same developed save used for comparison runs.
- Diagnostic: GK Frame Spike Probe 0.4.0.
- Do not use F6 GC hold during mod-owner isolation.
- Avoid sleep/save so the known save/`Resources.UnloadUnusedAssets()` hitch class does not contaminate the comparison.
- Characteristic target class: roughly 0.68–0.75 s wall time, almost entirely Unity-main-thread CPU, with Unity incremental GC pending.

## Control — BepInEx + probe only

Result: **negative** for the characteristic class.

- No characteristic ~0.68–0.75 s steady-state freeze during extended ordinary gameplay.
- Post-load spikes remained in the small ~52–132 ms range.
- Same run exercised dialogue, vendors, tools, NPC schedules, spawned mobs/pathfinding, and repeated Witch Hill `big_L` / `big_R` crossings.

Interpretation: base game/current save alone is strongly disfavored as a sufficient owner. Proceed with grouped mod isolation.

## Split A — first half

Enabled ordinary mods/components:

- Longer Days 1.7.0
- GK Longer Days Buff Fix 0.1.1
- Keeper's Lantern 1.0.12
- Keeper's Lantern - Belt Visual 1.0.12
- Keeper's Lantern - Dungeon Practical Lights 1.0.12
- Keeper's Lantern - Dynamic Shadows 1.0.12
- Keeper's Lantern - Save Now Compatibility 1.0.12
- Alchemy Research Redux 0.1.8
- Decomp Delight! 0.1.9
- I Neeed Sticks! 1.6.12
- Rest In Patches 0.1.5
- Queue Everything! 2.2.0
- Save Now! 2.5.14
- Show Me Moar! 0.1.13
- Thoughtful Reminders 2.2.14

Plus GK Frame Spike Probe 0.4.0.

Result: **negative** for the characteristic class.

- Extended ordinary gameplay did not reproduce the ~0.7 s class.
- The run reached managed heap around 532 MB and included completed GC-cycle advances, NPC activity, pathfinding, vendor/dialogue activity, tool work, and Witch Hill crossings.
- Largest steady-state probe event was about 165 ms, not the characteristic class.

Interpretation: this half is not sufficient by itself to reproduce the target freeze under the tested conditions.

## Split B — complementary half

Requested set contained 16 ordinary plugins, but `Max Buttons Redux 1.4.0` did **not** load because its required dependency `p1xel8ted.gyk.restinpatches` was intentionally absent. Therefore the actual reproducing set was 15 ordinary loaded plugins plus the probe.

Actually loaded ordinary plugins:

- Configuration Manager 18.4.1
- HoldToSprint 1.0.3
- The Merchant's Promise 1.0.0
- Food & Drink Rebalance 1.2.0
- Better Save Soul Rebalance 1.1.1
- GYK Quick Stack 1.0.0
- Specialized Storage 1.2.0
- Day Wheel Quest Markers 1.0.30
- Gamepad Tooltip Position Fix 1.3.0
- New Game at Bottom! 2.2.11
- No Intros! 2.2.11
- Move Objects Mod 0.6.0
- GYK Recipe Pin 0.3.0
- GYK Teleport Stone Quick Slot 1.0.0
- Rain & Wind Volume Controls 1.1.0

Plus GK Frame Spike Probe 0.4.0.

Result: **positive**; characteristic freeze reproduced quickly.

Direct capture:

- `FRAME SPIKE #7`
- wall: **702.18 ms**
- Unity-main-thread CPU: **703.13 ms**
- CPU share: **100%**
- `gc_cycle_delta=0`
- `incremental_pending=True`
- managed heap: **555.3 MB**
- focused, `timeScale=1`

The user immediately identified this event as the characteristic freeze. It occurred shortly after normal gameplay began. The immediately preceding house-zone logging is not treated as causal; prior evidence already shows log adjacency is insufficient for trigger ownership.

Interpretation: the target class is reproducible without Split A and without Max Buttons Redux. The upstream owner or sufficient interaction is therefore inside the 15 actually loaded Split-B plugins. Split A and Max Buttons Redux are not necessary for this reproduction.

## Split B1 — eight-plugin subgroup

Enabled ordinary plugins:

- HoldToSprint 1.0.3
- GYK Quick Stack 1.0.0
- Specialized Storage 1.2.0
- Day Wheel Quest Markers 1.0.30
- Gamepad Tooltip Position Fix 1.3.0
- Move Objects Mod 0.6.0
- GYK Recipe Pin 0.3.0
- GYK Teleport Stone Quick Slot 1.0.0

Result: **subjectively negative but not instrumented**.

The user played a substantial ordinary interval and reported a few smaller hitches but no characteristic ~0.7 s freeze. The run included Witch Hill crossings, NPC schedules, spawned bats/pathfinding, tool work, combat, crafting, vendor interaction, and teleports. However GK Frame Spike Probe 0.4.0 was accidentally absent: BepInEx loaded exactly these eight ordinary plugins and no probe. Therefore this run is useful negative user-observation evidence but cannot quantitatively classify its smaller hitches or provide a probe-confirmed upper bound.

Interpretation: do not repeat immediately; test complementary B2 first. If B2 is positive, the sufficient owner set is narrowed there and B1 need not be repeated for the current binary search. If B2 is also negative, repeat B1 with the probe before inferring cross-group interaction.

## Split B2 — complementary seven-plugin subgroup

Enabled ordinary plugins:

- Configuration Manager 18.4.1
- The Merchant's Promise 1.0.0
- Food & Drink Rebalance 1.2.0
- Better Save Soul Rebalance 1.1.1
- New Game at Bottom! 2.2.11
- No Intros! 2.2.11
- Rain & Wind Volume Controls 1.1.0

Plus GK Frame Spike Probe 0.4.0.

Result: **positive**; characteristic freeze reproduced almost immediately.

Direct capture:

- `FRAME SPIKE #1`
- wall: **687.15 ms**
- Unity-main-thread CPU: **671.88 ms**
- CPU share: **97.8%**
- `gc_cycle_delta=0`
- `incremental_pending=True`
- managed heap: **565.1 MB**
- focused, `timeScale=1`

The probe was correctly loaded, and the user immediately identified the event as the characteristic freeze. It occurred only seconds into steady-state gameplay. The nearby ordinary FlowCanvas/NPC schedule activity is not assigned ownership from log adjacency.

Interpretation: the target class is reproducible using B2 alone. B1 is not necessary for this reproduction. The upstream owner or sufficient interaction is now narrowed from 15 ordinary plugins to these seven B2 plugins.

## Split C1 — three gameplay-data/logic mods

Enabled ordinary plugins:

- The Merchant's Promise 1.0.0
- Food & Drink Rebalance 1.2.0
- Better Save Soul Rebalance 1.1.1

Plus GK Frame Spike Probe 0.4.0.

Result: **positive**; characteristic freeze again reproduced almost immediately.

Direct capture:

- `FRAME SPIKE #6`
- wall: **701.34 ms**
- Unity-main-thread CPU: **703.13 ms**
- CPU share: **100%**
- `gc_cycle_delta=0`
- `incremental_pending=True`
- managed heap: **555.2 MB**
- focused, `timeScale=1`

The user immediately identified this event as the characteristic freeze. It occurred immediately after ordinary mushroom-gathering activity (`Craft mushroom_spawn` / drop collection). Log adjacency alone is not treated as proof of which plugin executed the causal allocation path.

Interpretation: the sufficient owner set is narrowed from seven plugins to these three. Configuration Manager, New Game at Bottom, No Intros, and Rain & Wind Volume Controls are not necessary for reproduction.

### Source-guided suspect inside C1

`Food & Drink Rebalance 1.2.0` was a concrete hypothesis because its exact accepted `baseline/1.2.0-accepted` source patches `CraftComponent.DoAction`, a recurring action/crafting path. The prefix performs reflective member access, LINQ/string work, creates a container-name array, calls `GetMethods`, and may invoke reflected methods with a new argument array while checking the Well Fed resource. This was a plausible recurring allocation source, especially because the captured event followed a mushroom craft/activity, but required a single-plugin A/B before any causal conclusion.

## Split C1a — Food & Drink Rebalance alone

Enabled ordinary plugin:

- Food & Drink Rebalance 1.2.0

Plus GK Frame Spike Probe 0.4.0.

Result: **negative for the characteristic class during the tested interval**.

The probe was correctly loaded (`2 plugins to load`). After `OnGameStartedPlaying`, the run exercised ordinary gathering/craft events, repeated house teleports, Witch Hill crossings, NPC schedules, a large bat spawn/pathfinding period, berry gathering, furnace completion, craft GUI at the cooking table and oven, axe work, and combat. No ~0.68–0.75 s steady-state event occurred. Post-start probe events were small-to-moderate; the largest ordinary steady-state event was about 125.88 ms, with later events around 50–107 ms. The large ~2.07 s event occurred during load before `OnGameStartedPlaying` and is not the target gameplay class.

Interpretation: Food & Drink Rebalance 1.2.0 is **not shown sufficient by itself** to reproduce the target class, and the specific `CraftComponent.DoAction` single-owner hypothesis is substantially weakened because the run exercised action/craft paths without the characteristic stall. This is still a finite stochastic negative test, not proof that the mod can never contribute.

The next cheapest discriminating test is the complementary pair:

- The Merchant's Promise 1.0.0
- Better Save Soul Rebalance 1.1.1

plus GK Frame Spike Probe 0.4.0.

If that pair reproduces, Food & Drink Rebalance is not necessary for the target reproduction and the search narrows to Merchant vs Better Save Soul (or their interaction). If the pair is negative, test Food + one of the two remaining plugins to identify which interaction is sufficient.
