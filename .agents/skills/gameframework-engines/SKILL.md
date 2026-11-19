---
name: gameframework-engines
description: "Route any work that uses, hosts, extends or chooses between GameFrameWork's engines (SimEngine, GameRuntime, ScenarioEngine, Calendar, Economy, AI, Tutorial, WorldEvents, Relationship, Dialogue, Diplomacy, Combat, Alchemy, Spatial, EngineCommon, GameContent, WorldCreator). Use when a game needs simulation, time, money, decisions, onboarding, events, people, talk, factions, fighting, recipes, maps, catalogs or worlds from this repository, or when building a game on GameFrameWork."
---
# GameFrameWork engines

Pick the engine that owns the capability, then read its canonical skill. The owner's engine skills live in `skills/`: the index is `skills/README.md`
(including the rules that apply to every engine skill) and the source-verified HTML references are indexed in `docs/engines/index.html`. The
`gameframework-<engine>` entries under `.agents/skills/` and `.claude/skills/` only route to `skills/gameframework-<engine>/SKILL.md`.

One owner per capability: never build a second clock, ledger, calendar, market, pathfinder or condition reader beside the engine that owns it, and never
copy an engine into a game repository; reference it.

| Need | Canonical engine skill | Project(s) |
| --- | --- | --- |
| Host, the one clock, components, events, Godot hosting | `skills/gameframework-runtime/SKILL.md` | `src/GameRuntime` |
| Deterministic world simulation (entities, production, logistics, construction, companies, research, saves, replay) | `skills/gameframework-simengine/SKILL.md` | `src/SimEngine` (`Beep.SimEngine`) |
| Missions, objectives, endings, scenario saves and replay tapes | `skills/gameframework-scenario/SKILL.md` | `src/ScenarioEngine` |
| Dates, seasons, holidays, daylight, schedules | `skills/gameframework-calendar/SKILL.md` | `src/CalendarEngine` |
| Markets, prices, routes, production chains, loans, shocks | `skills/gameframework-economy/SKILL.md` | `src/EconomyEngine` |
| Agent decisions (behaviour trees, utility, blackboard) | `skills/gameframework-ai/SKILL.md` | `src/AIEngine` |
| First-run journey, tutorial steps, hints, tips | `skills/gameframework-tutorial/SKILL.md` | `src/TutorialEngine` |
| Weather, hazards, disease, storyteller director | `skills/gameframework-worldevents/SKILL.md` | `src/WorldEventsEngine` |
| Needs, moods, opinions, bonds, gossip | `skills/gameframework-relationship/SKILL.md` | `src/RelationshipEngine` |
| Conversations, quests, Yarn and Ink import | `skills/gameframework-dialogue/SKILL.md` | `src/DialogueEngine` |
| Realms, treaties, wars, coalitions, espionage | `skills/gameframework-diplomacy/SKILL.md` | `src/DiplomacyEngine` |
| Engagements between groups, ammunition, fuel | `skills/gameframework-combat/SKILL.md` | `src/CombatEngine` |
| Combination recipes and discovery | `skills/gameframework-alchemy/SKILL.md` | `src/AlchemyEngine` |
| Deciding where maps, paths, sight and fog belong: simulation truth uses SimEngine's map/navigation contracts, presentation uses Godot; SpatialEngine is a parked sketch | `skills/gameframework-spatial/SKILL.md` | `src/SimEngine`; `src/SpatialEngine` (parked sketch) |
| Strict JSON, conditions, digests, seeded rolls, locale, graphs, balance sweeps | `skills/gameframework-common/SKILL.md` | `src/EngineCommon` |
| Item, unit and recipe catalogs | `skills/gameframework-content/SKILL.md` | `src/GameContent` |
| World and city generation, world metadata | `skills/gameframework-worldcreator/SKILL.md` (metadata-only world requests also follow `.agents/skills/world-generator/SKILL.md`, as `AGENTS.md` routes) | `src/WorldCreator` |

## The shape most engines share

The engine's canonical skill names its exact projects, CLI commands and test command; where it differs from this summary, the engine's skill wins.

- **`<Engine>.Core`**: a pure library. No clock, no file access, no Godot, no world, no `float`/`double`, no `System.Random`, no `DateTime` (WorldCreator and
  `EngineCommon.TailTool` are the documented exceptions). Input is a strict JSON document plus requests; output is state, facts and explanations. SimEngine
  is shaped differently (`Beep.SimEngine`, with its own `src/SimEngine/AGENTS.md`).
- **Strict documents**: every field present, unknown/missing/repeated fields refused, each refusal with its own code. A document has a `format` number; old
  formats are refused by their own code and are not migrated. Engines that ship samples keep them in the location their skill names.
- **`GameRuntime.<Engine>`**: the game-layer component, `Init` and `Update` only. Requests are events that only queue; they are answered in the next `Update`
  in arrival order with an applied, answer or refused event. Each engine has its `ChannelRegistry` subclass with a fixed rank; an unserved channel or a
  second owner is refused. Events are deferred and bounded (owed events back-pressure time instead of being dropped). Checkpoints are refused while not quiet.
  Journals are text; replay needs a tick rate that divides 1,000,000.
- **`GameRuntime.<Engine>.Bridges`**: one class per link to another engine (calendar clock and facts, dialogue `ext.*` facts and outcomes, AI percepts,
  SimEngine ledger). **`<Engine>.Cli`** where the engine has one; its commands differ per engine (commonly `check`, `explain`, `coverage`). Some engines
  also have a `GameRuntime.<Engine>.Headless` demo.
- **One clock**: the session's `RuntimeClock` (or Godot's physics loop in the Godot host). Engines take the tick from `UpdateContext`; none keeps its own.

## Build and test

- SDK pinned by `global.json` (10.0.402). Build the solution: `dotnet build GameFrameWork.slnx -c Release`, or one project: `dotnet build <csproj> -c Release`.
- Test projects are xUnit v3 executables. Use the focused test command named in the engine's canonical skill (most are
  `dotnet run --project src/<Engine>/<Engine>.Tests -c Release`; add `-- -method "*Name*"` or `-- -class "Full.Name"` to run one). Run focused tests for
  the change, never the whole suite by default; record real commands and results.
- Godot .NET projects target .NET 8: a Godot game references only `net8.0` or `netstandard2.1` assemblies (WorldCreator's projects target `net10.0` and run
  offline).

## Rules

Read `AGENTS.md` and `CLAUDE.md` before changing anything here, and the rules in `skills/README.md`. The owned design record named in the engine's canonical
skill is the authority for its contract, and SimEngine has its own `src/SimEngine/AGENTS.md`. Do not describe parked or partial work as complete. Godot
features come first for anything view-only (rendering, input maps, tweens, view navigation); an engine replaces a Godot service only where simulation truth
needs determinism (see `skills/gameframework-spatial/SKILL.md`).
