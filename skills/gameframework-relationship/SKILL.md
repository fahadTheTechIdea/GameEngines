---
name: gameframework-relationship
description: "Use GameFrameWork's RelationshipEngine for social simulation: actors with decaying needs and derived mood, opinions on axes, memories and moodlets with decay and stacking, ladders and bonds, gifts and tastes, interactions with availability and seeded acceptance, gossip, communities, standing and structural balance, plus actor./target./edge./reverse. facts. Use when a game has villagers, crews, companions or characters whose opinions and moods change."
---

# RelationshipEngine

Reference: `docs/engines/RelationshipEngine.html`; design record `docs/RELATIONSHIP_ENGINE.md`.
Code: `src/RelationshipEngine/RelationshipEngine.Core` (net8.0); component `src/GameRuntime/GameRuntime.Relationship`.

## Boundary

- Owns: needs/decay, mood, moodlets, memories, axes, ladders, bonds, gift taste, interactions, gossip, communities, standing, balance.
- Does not own: dialogue text, animation, pathfinding, factions/diplomacy, or the daily schedule (the host sets activity, usually from CalendarEngine).
- Randomness is seeded; a save is a complete copy of state.

## Load (format 3)

```
var set     = RelationshipLoader.Load(json);       // formats 1–2 refused (REL_FORMAT_OLD / REL_FORMAT_2)
var runtime = RelationshipRuntime.Create(set, startStep);
```

Sections: `seed`, `time.stepsPerDay`, `limits`, `facts`, `axes`, `needs`, `sources`, `mood`, `moodlets`, `memories`, `traits`, `ladders`, `bonds`, `gifts`, `tastes`, `gossip`, `social`, `interactions`.
Start from `samples/village.relationship.json`.

## Needs and mood

- A need has `max`, `initial`, `decayPerDay`, `critical`/`rearm`, a piecewise `urgency` curve and conditional `decayModifiers`.
- `sources` map an activity to a need refill per day; `SetActivity` sets what an actor is doing. The day applies over `social.schedule`: `Atomic` or `Spread` (first five steps).
- `mood = traitBase + Σ needWeights + Σ moodlets`, clamped and mapped to the highest reached state. `Mood`/`MoodStateId` answer.
- Moodlets: magnitude, duration, `decay` (Fixed/Linear/HalfLife), `stack` (Refresh/Stack/Replace/Unique). `Grant(actor, id, scalePermille)`.

## Edges, axes, memories

- An edge is one actor's opinion of another: axis values, memories, cooldowns, bonds.
- `Remember(holder, subject, memory, scalePermille)`; a memory has effects per axis, duration, decay, stack and optional `spread`.
- `Opinion(from, to, axis)` = baseline + decaying memory contributions, clamped. Axes drift to baseline daily; `AdjustAxis` changes explicitly.

## Ladders, bonds, triads

`LadderStateId(from, to, ladder)` maps an axis to a named step with hysteresis. `SetBond`/`HasBond`; bonds may be symmetric, exclusive and broken below an axis threshold. `Triads(holder, subject)` reports structural balance per intermediary.

## Interactions

```
var offers = runtime.Offers(actor, target, hostFacts);   // Available + Why + Desire
var r = runtime.Perform(new InteractionRequest(id, actor, target, witnesses, gift), hostFacts);
// r.Accepted, r.Score, r.Threshold, r.Noise, r.Changes
```

Interactions have tags, a precondition, a cooldown, desire rules and effects on both edges, needs, moodlets or bonds. Acceptance is a seeded roll against a threshold; refusal is explained. `GiftTaste(receiver, gift)` uses tags and tastes. Witnesses remember at a scaled magnitude.

## Gossip

`SpreadDef(memory, permille, chance, hops)` spreads a memory through the graph; `gossip` rules bound trust, tellings and listeners. Seeded and bounded.

## Derived social state

Communities by label propagation, standing by damped trust iteration (`social.standing`), structural balance over top-k triads (`social.balance`), day schedule. All bounded by `limits` and config.

## Facts

`actor.*` (mood, mood_state, activity, community, community_size, standing, need.<id>, trait.<id>), `edge.*` / `reverse.*` (known, same_community, state.<ladder>, bond.<id>, memory.<id>, axis ids). Anything else is a host fact and must be declared.

## Runtime component

Requests: actor add/remove, activity, interact, grant, remember, need, axis, bond, mood, opinion. Results: applied/refused, interaction-performed, mood/opinion answered, stepped, mood-changed, need-critical, state-changed, bond-changed, memory-added, moodlet-added, evicted, community/standing/balance changed. `RelationshipCheckpoint` saves state; bridges feed AI/dialogue.

## CLI

`check`; `simulate <scenario> <days> [--save] [--resume]`; multi-seed `--seeds n [--from-seed] [--days] [--metrics] [--rows]`; `explain mood|opinion|interaction`; `coverage`. Exit 0 done, 1 refused/incomplete, 2 usage/file problem.

Tests: `dotnet run --project src/RelationshipEngine/RelationshipEngine.Tests -c Release`.
