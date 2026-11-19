---
name: gameframework-scenario
description: "Use GameFrameWork's ScenarioEngine for missions: validated JSON content, typed facts and conditions, objective graphs, exhaustive endings, replaceable game layers, transactional command admission and world frames, complete session snapshots, checksummed saves with previous-generation recovery, portable replay and diagnostics. Use when a game needs objectives, win/lose endings, campaign missions or scenario-driven world requests."
---

# ScenarioEngine

Reference: `docs/engines/ScenarioEngine.html`; design authority `src/ScenarioEngine/docs/sdd/` (index, engine, content, authoring).
Code: `src/ScenarioEngine/src` — `ScenarioEngine.Contracts`, `.Core`, `.Content`, `.Persistence`, `.Replay`, `.Diagnostics`; sample `samples/Counter.GameLayer`; host `tools/ScenarioSimulator`.

## Boundary

- Kernel owns: content validation, conditions, objective graph, endings, command admission, tick transaction, snapshots, save/recovery, replay, diagnostics.
- A game layer owns domain rules: operations, effects, observations, projected facts. It is pure — previous snapshot in, candidate state/events out.
- A failed tick rolls back; a faulted session does not continue.

## Content

Normalized JSON pack ≤ 1 MiB via `IScenarioPackSource` (`JsonFilePackSource`). `IScenarioContentService.LoadAsync` validates schema (`IContractValidator`) then semantics before registration; cancellation is honoured. `RemoveFromCatalog` affects new selection only.
Identity: pack id/version, content digest, scenario id, layer id/version.

## Contracts

```
Condition = All | Any | Not | Compare(fact, op, value)
ObjectiveDefinition: dependencies (accepted statuses), ActivateWhen, CompleteWhen, FailWhen, success/failure effects
EndingDefinition: priority, condition, outcome tags
ZoneDefinition: anchors + exits (target zone, anchor, condition, duration)
```

Facts are boolean/integer/text and declared by the layer. Validation refuses unreachable/ambiguous endings and dangling zone/objective references.

## Game layer

```
public interface IGameLayer { string Id; string Version; LayerCapabilities Capabilities; ValidationReport Validate(JsonElement); ILayerRuntime Create(LayerStartContext); }
public interface ILayerRuntime
{
    LayerSnapshot InitialState { get; }
    LayerStepResult Resolve(LayerSnapshot previous, LayerTickContext context);
    LayerEffectResult ApplyEffects(LayerSnapshot candidate, ImmutableArray<EffectEnvelope> effects, LayerTickContext context);
    ImmutableDictionary<string, FactValue> ProjectFacts(LayerSnapshot state);
    ValidationReport ValidateState(LayerSnapshot state);
}
```

`LayerTickContext` gives tick, fixed step, world frame, admitted commands and seeded `IRandomStreams`. `Resolve` returns state, outcomes, events, world requests. Optional `IIdleTickRuntime.CanSkipIdleTick` skips a provably idle tick.
Register in `GameLayerRegistry` at the composition root; no DLL scanning.

## Simulation

```
using var session = factory.Create(validated, startOptions, initialWorld);
session.Enqueue(new CommandEnvelope(commandId, controllerId, targetTick, worldGeneration, operation, payload));
session.CancelQueued(controllerId, commandId);
StepResult step = session.Update(new TickInput(tick, worldFrame));  // Committed, Code, Tick, Events, WorldRequests, Snapshot, RuleStateHash
```

Limits: command payload ≤ 64 KiB UTF-8, depth 32, duplicate JSON properties refused; frame ≤ 256 observations within 1 MiB. Duplicate command ids are not admitted. `ScenarioEngine.StateHash(snapshot)` is the state digest.

## Snapshot, save, recovery

`SessionSnapshot` holds identity, ids, seed, controllers, phase, tick, generations, sequence counters, objectives, applied effects, ending/outcome, layer state, pending commands, receipts and random streams.
`SessionSnapshotRules` validates all of it before restore; `ScenarioSessionFactory.Restore` revalidates scenario + layer.
`SaveDocument` + `IScenarioSaveCodec` + `ISaveStore`; optional `IRecoverableSaveStore` + `LoadPreviousAsync` reads a backup only when selected, never repairs or promotes. Checksums detect modification, not authentication.

## Replay

`ReplayService`: `Record(session, world)` → enqueue/step → `CaptureReplay()` → `codec.Encode` → `replay.Verify(scenario, tape)`. The tape holds the initial snapshot/world, commands, cancellations, lifecycle calls and expected hashes for state, events, requests and receipts; verification names the first divergent entry. Bounded to 10,000 entries / 16 MiB; the recorder owns the session while recording.

## Diagnostics

`IEngineReporter` / `ApplicationDiagnostics`, `EngineNotice` + `INotificationSink`, `AuditRecord(operation, target, result, session)`. Validation findings are `Diagnostic(Code, Message, File, Pointer, Severity)`.

## Build and run

```
dotnet build ScenarioEngine.slnx -c Release
dotnet run --project src/ScenarioEngine/tools/ScenarioSimulator/ScenarioSimulator.csproj -c Release
dotnet run --project src/ScenarioEngine/tests/ScenarioEngine.Tests/ScenarioEngine.Tests.csproj -c Release -- -noColor
```

`MissionComponent` in `src/GameRuntime/GameRuntime.Scenario` adapts the mission to GameRuntime.
