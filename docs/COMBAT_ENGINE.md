# CombatEngine: research, layer boundary and acceptance criteria

Dated 2026-10-27. Part of the twenty-first pass (see `docs/RUNTIME_PIPELINE_INTEGRATION_PLAN.md`). Written before the code; section 6 is updated with results as increments finish.

## 1. Research (searched 2026-10-27)

Facts are what the source states. Inferences are ours and marked. Sources that are forums, wikis or calculators are weaker than papers or standards, and the notes say so.

| Topic | Source | Documented | Our inference |
|---|---|---|---|
| Attrition at scale | [Lanchester's laws](https://en.wikipedia.org/wiki/Lanchester%27s_laws), [NPS notes](https://faculty.nps.edu/mpatkins/docs/lanchester.pdf) | Linear law for one-on-one and unaimed fire (attrition tracks target density); square law for aimed fire (size acts quadratically); simple models assume no wasted fire on dead targets and total annihilation; empirical tests are mixed; modified forms still underlie US Army simulations | Group-against-group resolution should be shot by shot with a finite target pool, so wasted fire and concentration emerge instead of being assumed. Do not claim a law |
| Hit roll | [Fan wiki, XCOM Defense](https://xcom.fandom.com/wiki/Defense), [calculator](https://west-games.com/xcom-hit-chance-calculator/) (community sources) | To-hit is aim minus defense, additive modifiers for cover, range, elevation; area-of-effect and melee ignore defense; cover is a fixed deduction | Hit chance is a clamped percentage built from additive percentage-point modifiers (range band, cover, training, suppression); area effects bypass cover |
| Tabletop resolution | [D&D 5e SRD summaries](https://roll20.net/compendium/dnd5e/Damage%20and%20Healing) (secondary) | Attack total against armor class; damage types have no rules of their own, other rules (resistance, vulnerability) use them; order: flat adjustments, then resistance (halve, round down), then vulnerability (double); multiple resistances count once | Damage types are data keys that rules look up; the order of adjustments is part of the rule and fixed |
| Armor mitigation | [WoW armor](https://warcraft.wiki.gg/wiki/Armor), [LoL armor](https://wiki.leagueoflegends.com/en-us/Armor?oldid=4073057), [Unity guide](https://bugnet.io/blog/how-to-fix-armor-mitigation-formula-making-damage-negative-in-unity) | `reduction = armor / (armor + K)`, diminishing in percent but linear in effective health; K depends on the attacker; a damage floor avoids zero; penetration is best modelled as lowering effective armor before the curve | Adopt the ratio curve with a content constant K and a minimum damage; penetration subtracts from armor first; integer arithmetic in per-mille |
| Piercing and hardness | [HoI4 combat stats (community guides)](https://gameslantern.com/article/all-combat-stats-explained-in-hearts-of-iron-iv) | Soft and hard attack with a hardness split; piercing above armor avoids a 50 % damage cut; organization at zero forces retreat; combat width limits how many attack | Armor beating penetration costs a fixed share of damage; a group that loses its fighting strength routs. Hard/soft target split is adapted as per-damage-type armor factors |
| Wounds | [Dwarf Fortress wiki](https://dwarffortresswiki.org/index.php/Wound) | Wounds per tissue layer, pain and unconsciousness thresholds, hit location random, blunt vs edged effects differ | A tissue-level wound model is out of scope for formations; it is the right model for a duel-scale layer later. Damage type still selects different armor factors |
| Determinism | [Terrano and Bettner, 1500 Archers on a 28.8](https://gafferongames.com/post/what_every_programmer_needs_to_know_about_game_networking/) (via Gaffer), [lock-step paper](https://arxiv.org/pdf/1705.09704) | Lockstep sends commands, not state; every machine runs the same simulation; every random number from a shared seeded generator and the same number of calls; world checksums to find desync | Seed plus attempt counter supplied by the caller, no hidden generator state, a state hash on every result. Whether Age of Empires used fixed-point is not established by these sources |
| Unit sizes | [Britannica](https://www.britannica.com/topic/military-unit), [CFR](https://www.cfr.org/backgrounder/modern-military-force-structures), [Section (military unit)](https://en.wikipedia.org/wiki/Section_(military_unit)) | Squad about 7 to 14, platoon about 20 to 50, company about 100 to 250, battalion about 400 to 1,200; sizes vary by country, APP-6 symbols mean different things per nation | A formation is a count of people with its equipment list; names are tags, sizes are data |
| Ammunition | [US Army identification manual](https://archive.org/download/MManuals/UsArmyIdentifyingAmmunition7.pdf), [Britannica](https://www.britannica.com/print/article/21113) | Small-arms rounds: ball (personnel, unarmored), tracer, armor-piercing (hardened steel core), incendiary; about 20 mm is the small-arm limit; artillery by construction (fixed, semifixed, separate) and projectile (high explosive, armor-piercing, antipersonnel, nuclear, chemical) | Ammunition is its own item class with a damage addition, a penetration, a type and a splash radius. No NATO taxonomy was found, so ours is a minimal one |
| Eras | [Three-age system](https://en.wikipedia.org/wiki/Metal_Age), [Copper Age](https://en.wikipedia.com/wiki/Copper_Age) (sources disagree: Near East roughly 4500 to 3300 BCE, Europe later, Britain disputed), Space Age and Atomic Age pages (weak sources: Trinity July 1945; Sputnik 4 October 1957; the Information Age has no agreed start) | Chalcolithic or Copper Age sits between Stone and Bronze; Atomic Age from 1945; Space Age from 1957 and overlaps the others | Add copper-age, renaissance (about 1400 to 1600; **no source found**, convention), atomic (1945 to 1991), information (from 1991, our convention). The Space Age overlaps, so it is a fictional-world label only |

Not reached: Total War, BG3 and AoE damage tables (no primary sources), the APP-6 standard text, a scholarly source for Renaissance dates.

## 2. Adopt, adapt, avoid

- **Adopt.** Ratio armor curve with a floor; penetration lowers armor first; additive percentage modifiers for range, cover, training; resolution shot by shot against a finite pool; seeded randomness with a caller-owned counter; a checksum of state.
- **Adapt.** HoI4 hard/soft into armor factors per damage type; tabletop resistance order into a fixed rule order; XCOM cover into content levels; Lanchester into an emergent outcome of finite pools.
- **Avoid.** Hidden global random state; a second copy of weapon numbers in combat content; damage that no catalog stat explains; positive feedback that snowballs without a cap (a floor on accuracy, a cap on splash); per-tick polling; floating-point in the rules.

## 3. Layer boundary (decisions)

1. **Catalog owns every item number.** Weapons, armor, ammunition, vehicles, crews and formation equipment are catalog items and units. The stat vocabulary gains `accuracy`, `penetration`, `structure` and `splash-radius`, and an `ammunition` item class. Combat never stores a damage, range or armor value.
2. **CombatEngine.Core is a pure library.** No world, clock, wall time, files or global state. Inputs: rules (strict JSON, schema-validated), group specs, an engagement request with a seed and an attempt counter. Output: a result with an explained shot log (codes) and a state hash. Fixed-point (per-mille) integer arithmetic. It does not read the catalog.
3. **The catalog adapter is the bridge.** `GameContent.Catalog.Combat` reads catalog items and units and builds the specs Core consumes. It is the only code that knows stat ids. Core knows damage-type names only as keys of its rules.
4. **CombatComponent is a thin game-layer component** like `AlchemyComponent`: requests are queued by input or code and resolved only inside `Update`, using only the update context's tick; events `engagement`, `casualty`, `ammo`; state hash for checkpoints and journals.
5. **SimEngine boundary.** SimEngine has vehicle movers, storage and equipment condition, and no combat. Combat does not write SimEngine state. Ammunition and fuel are catalog resources; a game that wants them in SimEngine stock does it through a director effect it authors (the existing director bridge). What is not built: unit entities that fight inside SimEngine, wear from combat.
6. **What combat does not model:** wounds by body part, formations in space (it takes a distance), terrain beyond cover levels, morale beyond suppression and rout, logistics (the caller supplies rounds held).

## 4. Acceptance criteria (fixed before coding)

| # | Criterion | Evidence required |
|---|---|---|
| A1 | Rules are strict JSON with a schema and codes; unknown fields and missing decisions are refused | Loader tests, one per code |
| A2 | Same inputs give the same result and hash; changing the seed or attempt changes rolls | Tests, including a pinned expected value |
| A3 | One damage rule: all damage is computed in one method | Mutation: change it, damage tests fail; grep guard |
| A4 | Armor curve, penetration, damage-type factors, floor | Tests with worked numbers |
| A5 | Range bands, cover, training, suppression, rout | Tests |
| A6 | Rate of fire, ammunition limits, out-of-range | Tests; result codes explain each shot |
| A7 | Splash hits several defenders and ignores cover | Test |
| A8 | Every number comes from the catalog: adapter output equals the catalog stat; Core has no item ids or stat values | Test over all shipped items; mutation in the adapter; source guard |
| A9 | Ammunition items across eras, each with a build recipe and the same era rules | Validator and shipped-pack tests |
| A10 | Units in the standard: equipment items, crew skills, training, upkeep, lineage, build recipe, era rule | Refusal tests per code |
| A11 | Intermediate ages added with dates and the sourced caveats | Validator, pack tests |
| A12 | The generator is in the repository and regeneration equals the committed JSON | Byte-for-byte test |
| A13 | CombatComponent resolves only inside Update | Test; mutation: resolve at request time, test fails |
| A14 | Skirmish headless game: fixed frames, player and AI policies, `--pack`/`--era`, save and load, journal replay | Built executable run; replay verifies every tick |
| A15 | Honest boundary | Capability matrix and unrun list in the plan entry |

## 5. Rules (the model)

For one shot from weapon *w* (with ammunition *a*) at defender group *d*:

1. Range: if distance exceeds the weapon's range, code `COMBAT_OUT_OF_RANGE`. Else the range band gives a percentage.
2. Hit chance in per-mille = accuracy x band x training x suppression x cover (all percentages), clamped to a floor and a ceiling from the rules. Area ammunition skips cover.
3. Roll: one value from the seeded generator (seed, attempt, shot index). Hit when roll below chance.
4. Damage: base = weapon damage + ammunition damage (x training damage percent). Effective armor = armor rating x the type's armor factor, less penetration (weapon + ammunition), not below zero. Damage = base x K / (K + effective armor), at least the floor. If penetration is below the armor rating and the type is `armor-checked`, the damage is cut by the rules' share.
5. Splash: the shot hits up to 1 + radius / spacing members' worth of health; each hit repeats step 4.
6. Casualties follow the health pool. A group below the rout share of its initial strength routs and stops firing.

## 6. Results

Updated as increments finish; see the plan entry for the checklist.

### Results as of 2026-10-27

All of A1 to A14 are met by tests; A15 is the plan entry's capability matrix and unrun list.

- CombatEngine.Core: 45 tests; mutation checks: damage rule changed (8 fail), second damage computation (source guard fails).
- Adapter `GameContent.Catalog.Combat`: 8 tests over 83 fighting items, 21 formations and 15 ammunition items; mutation: +1 in the adapter fails the single-owner test.
- `GameRuntime.Combat`: events Engagement, Casualty, AmmoSpent, Standing, EngagementRefused, ForcesRestored; requests are queued and resolved only in Update; checkpoint holds tick, roll attempt, engagement count, last distance and each group's state, digested together with a digest of the forces; mutation: resolving in the event handler fails the deferred-Update test.
- Headless skirmish (`GameRuntime.Combat.Headless`): ancient, medieval, modern, future and fantasy packs selectable; eras can meet (`--pack-a arms-modern --pack-b arms-ancient`); a saved run resumes to the same final hash as an uninterrupted one (tick 287, state DF8E61BB6B1AD25D); a journal replays all 287 ticks; another seed diverges.
- Found while building it: the commander's round count and last distance were policy state outside the checkpoint, so a loaded run restarted its approach; they now live in the combat component's checkpoint and arrive at the commander through ForcesRestored.

## Second pass audit (2026-10-27, twenty-second pass)

Method: re-read the "not covered" list, read every source file of the engine, the component and the headless game for accepted-then-ignored fields, unraised codes and nondeterminism, then fix in the order the coordinator set.

### Gaps found

| # | Gap | Evidence | Fix |
|---|---|---|---|
| 1 | Groups of one side could fire at each other; `Side` was accepted and ignored by the engine | `Fire` never read `Side` | `COMBAT_SAME_SIDE`, tests, mutation |
| 2 | No bound on shots or time: a huge crew or a long engagement looped for millions of shots | no limit anywhere | `limits` in the rules; `COMBAT_TOO_MANY_SHOTS` |
| 3 | Unchecked arithmetic: large counts wrapped silently | `count * rate * seconds` | project-wide checked arithmetic; `COMBAT_NUMERIC_RANGE` |
| 4 | Only the attacker batteries were validated; negative weapon, armor, health or speed passed | `Validate` | every number of both groups validated |
| 5 | The checkpoint digest, state hash and roll formatted numbers with the current culture | `StringBuilder.Append(long)`; concatenation | invariant culture everywhere; a source guard. No culture changes a non-negative integer in .NET, so the culture tests pass with or without the fix; the guard is what can fail |
| 6 | `COMBAT_TARGET_DESTROYED` had no test naming it | new code guard found it | test added |
| 7 | The deferred-rule test stayed green when requests were resolved in the handler | mutation run in the 21st pass | observer test (see the plan entry) |
| 8 | The checkpoint did not carry the distance or the engagement count the commander depends on (found in the 21st pass) and had no tamper matrix | review | format 2, 14 tamper tests |
| 9 | The adapter assumed the units of the stat vocabulary (metres, per minute) without checking | a pack declaring range in km would be read 1000 times too short | `GearUnits.Check`, `COMBAT_ADAPT_UNIT_MISMATCH`, test with a modified pack |
| 10 | `ForcesRestored` did not carry standing, so the goal could not know a group already routed after a load | review | standing in the event |
| 11 | Rout was strength only | model | morale (below) |
| 12 | The non-piercing cut is binary: a hit that nearly pierces cuts like one that does not | the calibration test had to use full penetration | documented limit, not changed |
| 13 | Every era had a roster except the copper age | coverage test | copper axe band |

### Model additions (all from data)

- **Morale** (`rules.morale`, `training.morale_permille`): loss with the share of health lost and with suppression, recovery when unharmed, rout below a line. One rule owns it (`StandingRule`); a source guard keeps it so.
- **Terrain** (`rules.terrain`): scales hit chance (sight lines) and the going of a march. **Movement** (`MovementRule`): speed from the catalog `speed` stat (slowest platform; structures never move; foot soldiers use `rules.movement.foot_speed`). The caller map holds who is where; the engine answers how far a march gets. Reason: positions are the game layer to own, a second map in the engine would be a second owner of the fact.
- **N-way rounds** (`CombatResolver.Round`): any number of groups on any sides fire at once; damage is summed; standing decided once. `Exchange` is now a round of two orders.
- **Resupply** (`Logistics`): a battery takes in rounds up to its capacity; the caller removes only what was accepted from its own stock.

### Not done, with the reason

- **Crew and subsystem damage:** the people and platform pools and the crew rule were built in pass 25 (below). Subsystem damage (engine, turret, tracks) is still not built: the catalog has no subsystem stats.
- **Fuel:** a march rule was built in pass 25 (below). Fuel is not yet a stock-ledger resource and no game raises a fuelled group on the field with a depot.
- **SimEngine boundary (casualties and ammunition as stock flows).** Not built. What exists: a director effect hook (`IDirectorEffect`) and, in the engine, `ResourceInventoryIntent` with kinds Consumed, Destroyed and Lost over an endpoint entity. A combat effect would need a stock-holding endpoint entity for each group in the world and a test world with the inventory provider. I did not build or run that, so this is a requirement and not a verified path.
- **Godot.** No Godot run was made.

### Calibration (plausible, not historical)

Six tests with wide tolerances: equal forces win about half the time (70 to 130 of 200); a 2:1 force wins at least 95 percent of 60 fights and keeps more than half its strength, and survivors rise with the ratio (the square-law trend); armor never lets more through as it rises and heavy armor stops most of a weak hit; hits fall with range and none land beyond it; elite beats regular beats raw and fortified beats heavy beats light beats open; a beaten force breaks while 20 percent or more still stands (more than 30 of 40). No number has been matched to a real weapon or battle.

### Proof run

Three groups with allies (modern: rifle platoon and rifle company on side 1, tank platoon on side 2), melee bot, fixed frames of 33 ms, 400 m start:

```
forces a,1,3600,900,850,1400,active;b,2,10600,3000,850,15278,active;c,1,10000,300,850,1400,active
t17 engagement e16-0 a>b fired=775 hits=398 damage=3619/3600 ...
t47 engagement e46-0 b>c ... codes=COMBAT_ROUTED
t47 ended mutual
```

### Numbers

109 CombatEngine tests, 351 GameContent tests, 162 GameRuntime tests, all green; solution 0 warnings and 0 errors. Mutations run and caught: resolve in handler (3 runtime tests), attempt not advanced (2), morale out of the checkpoint digest (1), same side allowed (2), morale loss removed (3), terrain ignored in the march (1), invariant culture removed (1 guard), rounds not spent (2), second side of an exchange shortened (2).

## Pass 25 (2026-10-27): platform pools, crew, fuel, reinforcement

Researched briefly first. What I found: tabletop and computer wargames commonly abstract a mixed unit as people plus vehicles with separate losses, and tie a vehicle weapon to its crew; I found no primary source that fixes the numbers, so the rules below are my design from the engine existing rules, not a copied pattern.

### The rules

- **Two pools.** A `Group` has its people (`Health`, `InitialMembers`, `MemberHealth`, unchanged) and an optional platform pool (`PlatformHealth`, `PlatformUnitHealth`, `InitialPlatforms`, `PlatformArmor`). Before this, a mixed formation had one pool and `Members` counted structure points as people (a tank platoon of 16 reported about 100 members); now `Members` is people alone, so a squad and its combat group agree. A group without platforms behaves exactly as before: the existing 111 tests pass unchanged and the state hash of such a group is byte-for-byte the old one.
- **Where a hit lands.** In proportion to the health each pool holds, decided by its own deterministic roll (`battery id + "#pool"`), so it never moves the hit roll. Each pool has its own armor and unit health (splash counts units in the pool that was hit).
- **Crew.** A platform needs `CrewPerPlatform` people, drawn from the people alive. `OperablePlatforms = min(platforms standing, people / crew)`. A battery marked `Platform` fires `ceil(count x operable / initial platforms)` weapons; every other battery follows the people as before. A group with no people left is destroyed whatever its platforms.
- **Standing.** Morale loss counts platform health lost against the whole group (people plus platforms); the rout test is on people only.
- **Fuel.** `MovementRule.March` marches a platform group at its platform speed only while a crewed platform stands and, if it burns fuel, while the fuel lasts (`UsableFuel` = tanks less the share that went with lost platforms; burn scales with operable platforms); the rest of the march is on foot at the rules pace. Fuel burned is rounded up. `Logistics.Refuel` fills the tanks of the standing platforms and no further. New component events: `PlatformsLost`, `FuelBurned`, `RefuelRequested`, `Refuelled`, `ReinforceRequested`, `Reinforced`.
- **Reinforcement.** `Logistics.Reinforce` brings people back up to the strength the group started with, at full health, and nothing else changes; a destroyed or routed group takes none (`COMBAT_GROUP_WITHDRAWN`).
- **Catalog adapter.** `CatalogCombat.Group` builds the pools from the formation: people = strength, platform pool from the structure of platform items, crew from their crew counts, one fuel pool from their fuel (a formation burning two fuels is refused, `COMBAT_ADAPT_MIXED_FUEL`). Fuel on a non-platform item (the exo-frame) is not modelled.

### Formats

The rules file format is unchanged. The combat checkpoint is format 3 here and format 4 after pass 26 (was 2): it carries the platform pool and the fuel, the forces digest covers the platform and fuel shape, and a format-2 checkpoint is refused, never migrated. The field-game checkpoint is format 2 (was 1).

### Tests and mutation checks

CombatEngine: `PlatformTests` (13) and `FuelTests` (10) plus two reinforcement tests; CatalogCombat: four adapter tests (platform shape of every formation of every pack, tank platoon numbers, fuel tanks, mixed fuel refused); runtime: component reinforce, march and refuel, and the checkpoint with platform pool and fuel. Mutations that each failed a test: platform battery following people, no crew limit, hits never or always on platforms, platform loss not in morale, round ignoring the platform pool, platform pool not in the hash, wrong armor for platforms, no pool validation, no burn, unlimited fuel, usable fuel ignoring lost platforms, burn not scaled by crew, refuel room ignoring lost platforms, no march validation, fuel not in the hash, crew not checked on the march, adapter platform flag, crew count, members count, mixed fuel, fuel capacity unit, tanks not full, component not storing a march or refuel, fuel not saved. One mutation was not caught: the load-time bound on a platform pool in a checkpoint with a forged digest (the digest is not a secret, so the check is defence in depth; the resolver refuses such a group on first use).

### Not done

- Subsystem damage (no catalog stats). Movement speed does not fall as platforms are lost (the group keeps the slowest platform speed while any crewed platform stands). Weapons that need crew but are not platforms (the machine gun crew of 2) are not limited by crew. Fuel is not a ledger resource and no depot refuels in the field game. Platform losses are an event (`PlatformsLost`) but nothing in the field game acts on them.

## Pass 26 (2026-10-27): the pass-25 left-undone list

- **Pace of a column that lost platforms.** `Group.MarchSpeedMilli`: the people of a formation ride its platforms evenly (`ceil(initial people / initial platforms)` a platform); a platform that is gone or has no crew leaves its riders to walk, and the column moves at the harmonic mean of the platform pace and the foot pace by share of people (the same distance for everyone, so the times add by share). A structure still never moves; a group with no platforms keeps its own pace. `MovementRule.March` uses it for the ridden part of a march.
- **Crews of weapons that are not platforms.** `Battery.CrewPerUnit` (from the item crew counts, platforms have theirs in `CrewPerPlatform`): the weapons fire `min(share of survivors, people alive / crew)`. Honest limit: the cap can only bind when a formation has more crew than people, and no shipped formation does (a test checks all 22); it is there for what players build.
- **Fuel goes down with the platforms.** After a hit, `StandingRule` clamps the fuel to what the standing platforms can hold (`Group.FuelRoom`), the group state is validated against it, `UsableFuel` is gone (the state is now what is usable), and `PlatformsLost` carries the fuel lost. `Group.FuelType` names the catalog fuel.
- **Checkpoint.** Combat checkpoint format 4 (3 refused): the forces digest covers battery crews and the fuel type. Platform pool and fuel ranges (including fuel against the standing platforms) are checked before the digest, so a forged digest cannot carry an impossible pool in: 4 tests (pool too big, negative, fuel too big, negative).
- **Mutations that failed a test:** pace constant, riders ignoring crew, riders rounded down, march at top speed, no crew cap, no crew validation, no fuel clamp, fuel room ignoring platforms, no fuel/platform validation, no pre-digest pool check, no pre-digest fuel check, adapter crew.

### Subsystem damage: not built, and why

I searched for a standard way to describe it and found only game-specific systems (War Thunder module diaries, World of Tanks module lists, the BattleTech motive-system table, fan discussion): no shared schema or data format. A sensible version needs per-vehicle numbers (engine, tracks, turret health and effects) for about 25 vehicle items across five packs, plus a stat vocabulary entry, the catalog validator, the schema document, the generator data and the converters. There is no source for those numbers, and inventing them would put made-up values into content that the rules treat as authoritative; a stat present on only some items would be accepted and then ignored for the rest. Not built. What would make it reasonable: a data source for the vehicles, or a decision to model one generic "mobility" and "firepower" subsystem per platform class with the existing structure stat, which is a design change for the owner.
