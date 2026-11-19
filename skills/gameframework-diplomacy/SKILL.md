---
name: gameframework-diplomacy
description: "Use GameFrameWork's DiplomacyEngine for factions and nations: realms with regard, acceptance scoring, proposals and treaties with clauses and obligations, wars with score and exhaustion, coalitions and balance of power, espionage missions and intel levels, internal politics and factions, plus facts other engines read. Use when a game has nations, guilds, rival houses or companies that negotiate, ally or fight."
---

# DiplomacyEngine

Reference: `docs/engines/DiplomacyEngine.html`; research and limits `docs/DIPLOMACY_ENGINE.md`.
Code: `src/DiplomacyEngine/DiplomacyEngine.Core` (net8.0); component `src/GameRuntime/GameRuntime.Diplomacy` (+ `.Bridges`).

## Boundary

- Owns: regard, acceptance, proposals, treaties/obligations, wars, coalitions, agents/missions/intel, factions/stability, incident log, facts, save.
- Does not own: armies/battles (report CombatEngine outcomes via `Score`), market, UI, or executing effects.
- Integer per-mille math only; no floating point; no random generator state.

## Load (format 1)

```
var set   = DipLoader.Load(json);
var world = DipRuntime.Create(set);
var changes = world.Advance(toStep, seasonAt, hostFacts);   // ≤ 4000 steps
```

Sections: `seed`, `time`, `limits`, `seasons`, `ideologies`, `traits`, `deeds`, `rules` (`regard`, `standing`, `war`, `acceptance`, `balance`, `politics`, `espionage`), `casusBelli`, `missions`, `types` (treaty types + clauses), `realms`.
Start from `samples/isles.diplomacy.json`.

## Regard and acceptance

`RegardOf(holder, subject)` ∈ −1000..1000, the sum of ideology, leader, trust, honour, coalition and deed terms.

```
score = type.Bias + traitBias + Σ raw[factor] × weight[factor] / 1000 + modifiers
```

`Evaluate(from, to, type, termDays, amount)` returns `Verdict(Score, Chance, Lines)` — every line explained. Below `rejectAt` = no, at/above `acceptAt` = yes, in between a proportional seeded chance.

## Treaties

- `Propose` → `Respond` / `Withdraw` / expiry; all reported as `ProposalChange`.
- Treaty types: `consent` (Mutual/Imposed), clauses (`NonAggression`, `Trade`, `Tribute`, `Defend`, `Vassal`, `Truce`, `Bond`, `Embargo`), term bounds, regard, exclusions, war/succession interaction, cancel rule (Free/Breach/Never).
- `Fulfil(treatyId, paid)` answers an obligation; mismatches are `Due`/`Paid`/`Missed`/`Called`/`Honoured`/`Ignored` changes.
- Breach applies trust/honour loss, deed and casus belli.

## Wars

- `DeclareWar(attacker, target, casusBelli)`; a CB is required unless `allowNoCb`, which costs infamy/honour.
- Allies may be called; joining is a seeded chance from `callBase`, fear and standing; a refusal is an incident.
- `Score(warId, favoured, amount, source)` with source Battle/Occupation/Blockade/Event; exhaustion grows per season.
- `JoinWar`/`LeaveWar`; `GrantCasusBelli`/`Holds` for time-limited justifications.

## Coalitions

Checked on `balance.interval`: threat above `threatLine`, dissolution below `relaxLine`, `minMembers`, `joinRegard`, `bandwagonFear`. Reports Formed/Joined/Left/Dissolved. Members gain a balance casus belli.

## Espionage

Agents (`Recruit`, `Release`, state Idle/Away/Captured) run `Intel`, `Sabotage`, `Incite`, `Counter` missions. `OddsOf` returns success/exposure + reason. Intel levels None/Rumour/Report/Detailed/Complete via `IntelOf`/`Estimate`; intel and counter-intel decay. Exposure creates incidents and may grant `spied`.

## Politics

Realms have stability, legitimacy, factions (share + satisfaction) and band Stable/Unrest/Revolt. `Happen(realm, politicalEvent, scale)` applies faction likes. Succession resets legitimacy and may start a contested succession with a claim CB. A revolt refuses some calls (`DIP_REVOLT`).

## Facts

`Facts(prefix, realm, other)` / `DipFacts.Offered` expose `diplomacy.<realm>.{stability,legitimacy,trust,fear,honour,infamy,might,wars,treaties,band}` and pair facts `regard`, `reverse`, `ratio`, `at_war`, `treaties`, `coalition`, `intel`, `first.*`, `second.*`, `treaty.<type>`, `cb.<id>`. These feed dialogue, AI, events and UI.

## Determinism and save

Counter-based `DeterministicRoll(seed, stream)` everywhere. `DipSave` verifies document digest (`DIP_SAVE_DOCUMENT`) and state digest (`DIP_SAVE_STATE`); `StateDigest()` is stable.

## Runtime component

`DiplomacyChannels.Install(session)` before `Init`. Request: `diplomacy.requested`. Answers: applied, refused, evaluated, standing-answered, estimated. Changes: season, proposal, treaty, obligation, war, coalition, mission, agent, intel, politics, grant, incident, effect. `DiplomacyClocks` advances on calendar seasons; `DiplomacyJournal`/`Transcript`/`Replay` and `DiplomacyCheckpoint` support replay.

## CLI

`check`, `simulate <scenario> <steps> [--save]`, `explain proposal|regard|mission|coalition|realm`, `coverage`. Exit 0 done, 1 refused/incomplete, 2 usage/file problem.

Tests: `dotnet run --project src/DiplomacyEngine/DiplomacyEngine.Tests -c Release`.
