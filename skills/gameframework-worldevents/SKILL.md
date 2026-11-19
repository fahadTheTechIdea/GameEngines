---
name: gameframework-worldevents
description: "Use GameFrameWork's WorldEventsEngine for regional weather (Markov regimes, temperature, precipitation, wind), hazards with severity ladders and cooldowns, disease spread (SIR/SEIR), and a storyteller director that draws event cards by stress, with effects (signals and shocks) for other engines. Use when a game needs weather, disasters, outbreaks or paced random events."
---

# WorldEventsEngine

Reference: `docs/engines/WorldEventsEngine.html`; research and balance history `docs/WORLD_EVENTS_ENGINE.md`.
Code: `src/WorldEventsEngine/WorldEventsEngine.Core` (net8.0), CLI; component `src/GameRuntime/GameRuntime.WorldEvents`.

## Boundary

- Owns: seasons, climate regimes, weather, hydrology, hazard lifecycles, outbreaks, cards and chains, director pacing, difficulty/storyteller, modifiers, facts.
- Does not own: production, stock, combat, population agents or presentation. The game maps effects to its systems.
- Per-mille integer maths, seeded rolls, explicit step counter.

## Load (format 2)

```
var set   = WorldLoader.Load(json);   // format 1 is refused (superseded), never migrated
var world = WorldRuntime.Create(set);
WorldChanges c = world.Advance(toStep, seasonAt, hostFacts);
```

Sections: `format`, `id`, `seed`, `time`, `limits`, `hydrology`, `seasons`, `weathers`, `modifiers`, `zones`, `regimes`, `regions`, `links`, `hazards`, `diseases`, `interventions`, `storytellers`, `difficulties`, `cards`, `facts`, `start`.
Start from `samples/valley.world.json` or `tiny.world.json`.

## Weather and hydrology

- Zones choose a climate regime per season; temperature follows persistence and noise; `spill` tilts neighbours so regions are correlated.
- Weather applies temperature/precipitation/wind/wetness and modifier effects.
- Hydrology tracks soil moisture from rain, evaporation, melt, drainage and heavy rain; `SoilPermille(region)` answers. Flood hazards read it.

## Hazards

- Start from base chance, seasonal weight, conditions, cooldown and exclusions. `excite` adds self-excitation: recent activity raises the chance, then decays.
- Severity rungs: duration, onset/recovery steps, stress, escalation chance, effects. Phases Onset/Peak/Recovery; changes Started/Escalated/Receding/Ended.
- `BeginHazard(region, hazard, severity)` is for scripting/tests; `Intensity(instance)`, `ExcitationAt(hazard, region)`, `IHazardWatcher`.

## Disease

- SIR or SEIR per disease: beta, incubation, infectious, waning, fatality, seasonal weights, seeding, regional flow (`links`).
- Region states None/Introduced/Outbreak; `SeedOutbreak(region, disease, cases)`; `Intervene(region, intervention, on)` for beta/flow/vaccinate/treat/fatality with an optional auto threshold. `Living(disease)`.

## Cards and director

Cards: tier Bad/Neutral/Good, weight, condition, cooldown, group, duration, intensity, effects, chained follow-ups (delay + probability). `PlayCard(card, region)` draws deliberately; `CardReason` is Drawn/Chained/Forced.
Director phases Build/Peak/Relax from a storyteller profile and current stress, respecting rest minimum, gap range and tier shares.

## Difficulty and storyteller

`UseDifficulty(id)` (hazard/bad-card/disease/stress multipliers) and `UseStoryteller(id)` (decay, peak/relax thresholds, hold, rest, gap, shares) are explicit controls, not hidden defaults.

## Effects and facts

Effects: `Signal`, `Shock`, `Modifier`, nested `Hazard`/`Outbreak`. Modifiers are bounded counters read with `Modifier(region, index)`.
Facts: `world.<name>` and `world.<region>.<name>` (season, weather, temperature, wetness, soil, hazard phase/rung/intensity, disease state/cases, director phase, stress, modifier values), plus reserved `hazard.`, `disease.`, `mod.` ids. `Facts(prefix, region)`.

## Determinism and save

`WorldImage`/`WorldSave` verify document and state digests (`WORLD_SAVE_DOCUMENT`, `WORLD_SAVE_STATE`). `StateDigest()` is stable. `Branch(reseededSet)` and `BranchForcingOnset` create independent copies for what-if and balance work; never mutate the original.

## CLI

`check`; `simulate <scenario> <steps> [--save]`; multi-seed `--seeds n [--from-seed] [--days] [--metrics] [--rows] [--onsets]`; rare onsets `--rare hazard:rung [--predraw|--conditional] [--effort|--efforts] [--replications]`; `explain hazard|card|disease|weather`; `coverage`. Exit 0 done, 1 refused/incomplete, 2 usage/file problem. Rare modes use `EngineCommon.Balance` splitting.

## Runtime component

`WorldEventsComponent` advances on the session clock and publishes season/weather/hazard/outbreak/card/director/effect changes; `WorldEvents.Bridges` maps effects into economy, diplomacy, calendar and AI requests. `WorldCheckpoint` saves state; a checkpoint waits for no pending request.

Tests: `dotnet run --project src/WorldEventsEngine/WorldEventsEngine.Tests -c Release`.
