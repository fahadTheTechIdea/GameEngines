---
name: gameframework-simengine
description: "Build deterministic world simulations on Beep.SimEngine: game modules and systems, entities and components, authored JSON content, work activity graphs, resource transactions, inventory, logistics, transformation, construction, maps/navigation, incidents, knowledge, objectives, one game clock, Sync save/hash, replay and lockstep. Use when a game needs a simulated world (tycoon, logistics, colony, oil and gas, factory) or when porting a game's own simulation kernel onto SimEngine."
---

# SimEngine

Reference: `docs/engines/SimEngine.html`; owned authority `src/SimEngine/README.md`, `src/SimEngine/AGENTS.md`, `src/SimEngine/plans/enhancements/MASTER_TRACKER.md`.
Code: `src/SimEngine/src/Beep.SimEngine` (netstandard2.1, C# 9); quickstart `src/SimEngine/help/quickstart`.

## Non-negotiable rules

- **One owner per concern.** Extend existing registries, job/reservation stores, codecs and views. Never add a second scheduler, clock, stock ledger, state store or compatibility facade.
- **One game clock.** `GameClock` owns current time and its embedded Sync. Every duration, deadline and elapsed work derives from it and the authored session quantum.
- **One `Sync(IStateSync)` per stateful owner**, owning save/load/hash.
- **Fixed point, stable IDs, stable order, one seeded random source.** No wall clock, float gameplay or platform timing.
- **Hosts are thin.** They submit commands, read immutable views/snapshots and drive `Initialize`/`Update`; they never touch mutable `World`.
- **Authored intent is JSON through the master manifest.** Executable behavior implements registered contracts. Missing/invalid definitions fail; no invented defaults.
- **Focused verification only.** Build to check compilation; run the affected suite for a completed slice. Do not run broad/full suites unasked.

## Shape of a game

```csharp
public sealed class MyModule : GameModule
{
    public override string Id => "my";
    public override ModuleVersion Version => new ModuleVersion(1, 0, 0);
    public override IReadOnlyList<ModuleRequirement> Requires => new[] { new ModuleRequirement(WorldSetupModule.ModuleId, new ModuleVersion(1,0,0)) };
    public override void DeclareContent(ContentTypes types) { /* sections + component keys */ }
    public override void Compose(WorldComposer composer) { /* systems, services, layers, stages */ }
}

var definition = new GameDefinition("my-game", new GameModule[] { new WorldSetupModule(), new MyModule() });
var factory = new GameFactory(definition, ContentSet.Load(new DiskFileSource("Content"), definition.ContentTypes));
var game = factory.NewGame(new NewGameRequest { Scenario = "main", Seed = 42 });
game.Initialize();
game.Update();
ulong hash = game.Hash();
byte[] save = game.Save();
```

`PlayerSeat`: `Submit`, `Preview`, `ReadResults`, `ReadNotifications`, `Snapshot`, `View`.

## Modules

Core: `ContentAccess`, `WorldSetup`, `Work`, `Inventory`, `Logistics`, `Motion`, `Infrastructure`, `Transformation`, `Construction`, `Technology`, `KnowledgeVisibility`, `CompanyKnowledge`, `Exploration`, `Incident*`, `ScenarioPressure`, `Objective`, `PlayerControllers`, `Automation`, `CompanyAlert`, `MachineFleets`, `PlanHistory`, `GameConditions`, `EnvironmentalMetrics`, `ResourceFlows`, `SpatialResources`, `TileNavigation`/`TileConditions`/`TileConstructionRules`/`TileTerrainWork`, `Weather`, `EquipmentCondition`, `Character`, `UtilitySignals`, `RemoteSignals`, `EventDispatchPolicy`, `LockstepPolicy`, `Operations`, and the optional `Tycoon` economy.

## Model

- `EntityDef` → `Entity`; `ComponentDef<T>` → `Component<TDef>`. One responsibility and one Sync per component.
- `World.Spawn(def, owner, configure)` / `CheckDespawn` + `Despawn`; removal guards protect stock/promises/claims.
- `GameSystem` with `Schedule.EveryTick`, `Schedule.Every(ticks, phase)` or `Schedule.Spread(n)`.
- Content: `manifest.json` → docs → `ResolvedContent`, validated and frozen before `Initialize`. Scenario JSON owns time quantum, game mode, modules, startup and initial entities.

## Simulation

- Work: activity graph nodes with executors, roles, durations and requirements; `WorkBoard` owns evaluation/jobs/spending/progress/history, `WorkSystem` owns matching/travel/execution.
- Resources: `ResourceInventoryState` + owned receipt promises; `ResourceTransactions` is the sole claim/transfer/conversion authority. Never copy stock.
- Logistics: demands, stock bands, routes, carriers, loading/unloading, recovery. It is not a second scheduler.
- Production: `Transformer`/`TransformerManager`, explicit batch or continuous, WIP/tools/time, whole-result commit.
- Construction/removal: real site, work graph, materials, timed completion, recovery/disposition.
- Tycoon: optional money, values, accounts, income, monetary costs, suppliers. Unpriced physical work runs without it.

## Spatial

Sparse typed `MapLayer`/`MapLayer<T>` (byte/int/fixed) owns values, codec and invalidation; `TileMap` is native tile space; point-space providers implement the same contracts. `NavGrid`/`PathSearch` route, `Mover` moves and pays real exposure/fuel. Regions, areas, docks, placement and visibility are provider-owned.

## Determinism and persistence

`World.Hash()` / `HashBreakdown()`; `SaveGame` carries build/scenario/seed and refuses incompatible data; `SimGame.RecordReplay(limits)` verifies state and results; lockstep compares peers. A faulted or mid-commit state cannot checkpoint.

## Where to look

`Kernel/` (World, modules, systems, entities, save), `Content/` (ContentSet), `Core/` (time, resources, work progress, capacity), `Standard/` (gameplay systems), `Map/` (layers, navigation), `Hosting/` (SimGame, factory, replay, lockstep), `Views/`.

Build: `dotnet build src/SimEngine/Beep.SimEngine.slnx -c Release`.
Quickstart: `dotnet run --project src/SimEngine/help/quickstart/Host/QuickGame.Host.csproj -c Release`.
