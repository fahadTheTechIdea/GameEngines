---
name: gameframework-combat
description: "Use GameFrameWork's CombatEngine to resolve deterministic group engagements: batteries and rates of fire, range bands, accuracy, cover, armor and penetration, splash, suppression and rout, personnel and platform pools, crew, fuel, march and resupply, with per-battery reports, an explained shot log and a state hash. Use when a game has units or squads that fight, with numbers from the item catalog."
---

# CombatEngine

Reference: `docs/engines/CombatEngine.html`; model rationale `docs/COMBAT_ENGINE.md`, stock flow `docs/COMBAT_STOCK_FLOW.md`.
Code: `src/CombatEngine/CombatEngine.Core` (net8.0); component `src/GameRuntime/GameRuntime.Combat`; rules sample `src/CombatEngine/Content/combat-rules.json`.

## Boundary

- Engine owns: rules validation, shot accounting, hit chance, damage, splash, suppression/rout, pool accounting, march/fuel, resupply caps, hashing.
- Engine does not own: item catalog, formations, pathfinding, AI target choice, animation, victory conditions.
- Pure function: no clock, no files, no mutable state. All numbers come from the request; only curves live in the rules document.

## Rules document (format 2)

```
var rules = CombatRulesLoader.Load(json);
var resolver = new CombatResolver(rules);
```

Fields: `armor_constant`, `minimum_damage`, `non_piercing_cut_percent`, `hit_chance.floor/ceiling_permille`, `personnel_health`, `damage_types`, `range_bands` (ascending `up_to_percent`), `cover`, `training`, `suppression`, `rout`, `splash`, `limits`, `morale`, `terrain`, `movement.foot_speed_milli_per_second`.
Unknown/missing fields, bad ranges, duplicate ids and non-ascending bands are refused (`COMBAT_RULES_*`).

## Groups and batteries

- `Group` is a value: personnel pool (`Health`, `MemberHealth`, `InitialMembers`) and platform pool (`PlatformHealth`, `PlatformUnitHealth`, `InitialPlatforms`, `PlatformArmor`, `CrewPerPlatform`), plus `ArmorRating`, `Training`, `Side`, `Morale`, `SpeedMilli`, `Fuel*`, batteries.
- Derived: `Members`, `Platforms`, `OperablePlatforms`, `Destroyed`, `Standing`, `MarchSpeedMilli(footSpeed)`, `FuelRoom`.
- `Battery`: weapon + optional ammo, `Rounds`, `CarryMilli`, `Capacity`, crew. A platform fires only while crewed.

## Resolving

```
var result = resolver.Fire(new EngagementRequest(group, enemy, distanceMilli, seconds,
                                                cover, terrain, seed, attempt, loggedShots));
```

- Shots = `(carry + units × ratePerMinuteMilli × seconds / 60) / 1000`, capped by `max_shots_per_battery`.
- Outcomes per shot: `COMBAT_HIT`, `COMBAT_MISS`, `COMBAT_OUT_OF_RANGE`, `COMBAT_NO_AMMO`, `COMBAT_TARGET_DESTROYED`.
- `Round(groups, orders, seconds, seed, attempt, loggedShots)` for simultaneous ordered fire; `Exchange(...)` for both directions.
- Each earned shot lands in exactly one outcome; `BatteryReport` totals must reconcile.

## Hit chance and damage

```
chance = accuracyPermille × band% × training% × terrain% / 100^3
         (suppressed ? × suppressedAccuracy% / 100)
         (not area ? × cover% / 100)
         clamp(floor, ceiling)

armor  = armorRating × armorFactor% / 100
eff    = max(0, armor - penetration)
damage = scaled × K / (K + eff)
         if armorChecked && penetration < armor: × (100 - nonPiercingCut%) / 100
         max(minimumDamage, damage)
```

Area weapons ignore cover. Splash hits extra targets by spacing, capped by `max_targets` and scaled by `splash_damage_percent`.

## Movement and logistics

- `MovementRule.March(rules, group, distanceMilli, seconds, terrain)` → distance closed, changed group, fuel burned, motor seconds.
- `Logistics.Reinforce/Refuel/Resupply` accept only up to capacity/starting strength; caller removes only what was accepted. Destroyed/routed groups take nothing (`COMBAT_GROUP_WITHDRAWN`).
- `StandingRule` applies suppression, recovery and rout thresholds from `MoraleRules`.

## Determinism

`CombatRoll.Permille(seed, attempt, group, battery, index)` is SHA-256, invariant culture, no generator state. `StateHash.Of(groups)` for journals/replay. Checked arithmetic: overflow → `COMBAT_NUMERIC_RANGE`, never wrap.

## Runtime component

`CombatComponent(resolver, initialGroups, seed, loggedShots)` queues engagement, round, move, resupply, reinforce and refuel requests; applies them in `Update`. Emits engagement, casualty, ammo-spent, platforms-lost, moved, resupplied, reinforced, refuelled, fuel-burned, standing, refused and forces-restored events. Declare `CombatEventCatalog` before `Init`; `CombatCheckpoint` saves tick, attempt, engagement count, distance and groups.

Tests: `dotnet run --project src/CombatEngine/CombatEngine.Tests -c Release`.
