---
name: gameframework-runtime
description: "Host GameFrameWork engines with GameRuntime: one clock, an ordered component list, queued events, deterministic ticks and fail-stop, request channels that always answer, per-engine components and bridges, checkpoints, journals and replay, and console/headless/Godot hosts. Use when composing a game session, writing an IGameComponent, wiring engines together, feeding input, or hosting the engines inside Godot."
---

# GameRuntime

Reference: `docs/engines/GameRuntime.html`; pipeline plan `docs/RUNTIME_PIPELINE_INTEGRATION_PLAN.md`; dependency map `docs/ENGINE_GRAPH.md`.
Code: `src/GameRuntime/GameRuntime.Abstractions`, `.Core`, `.Channels`, `.Services`, one project per engine component, `.Godot`, `.Headless`.

## Boundary

- Runtime owns: tick order, clock, event bus, request channels, lifecycle, fault reporting, checkpoints, journal/replay, hosts.
- An engine owns its rules. A component/bridge translates events to engine calls and back; it adds no thresholds or target rules.

## Component contract

```csharp
public interface IGameComponent
{
    string Id { get; }
    void Init(in ComponentContext context);   // connect/declare only
    void Update(in UpdateContext context);    // one tick
}
```

`UpdateContext`: `Tick` (first 1), `TickMicroseconds`, `Alpha` (interpolation only), `TotalMicroseconds`. Never keep your own counter.
Connect events in `Init` only; a component reads its own state and the events delivered to it.

## Session and tick order

```
var clock   = new RuntimeClock(ticksPerSecond: 60, catchUpCap: 8);
var session = new RuntimeSession(clock, reporter);
CalendarChannels.Install(session);      // declares events + adds the registry last
session.AddComponent(calendar, 10);
session.AddComponent(economy, 20);
session.Init();
int ran = session.Update(elapsedMicroseconds);
```

Each due tick: deliver input → for each component in `order`: `Update`, then drain its emitted events → `TickSettled` → stop if an ending was requested. Orders and ids are unique. A fault stops the session, reports once (`IRuntimeReporter`) and raises `SessionFaulted`; nothing continues.

## Clock

`RuntimeClock`: exact integer accumulator, no drift; `SetSpeed(n, d)` (1..64 each), `Pause`/`Resume`, `Earn` bounded by `CatchUpCap`, `IsRunningSlowly`, `Alpha`, `PendingTicks`.
`EngineMainLoop.Run(host, totalMicroseconds, fixedFrameMicroseconds, writeInputs)` — fixed frame = reproducible run; null = real clock.

## Events and input

`IEventPort.Emit<T>` queues; delivered after the emitting `Update`, publication order. Host `Subscribe<T>` receives after the tick drain. `Submit<T>` queues input for the next tick. `InputState` + `InputStateBridge` turn press/release edges into pollable actions. Exceeding the per-drain event budget is a fault, not a drop.
Standard events: observation-posted, mission-command-requested, mission-event, effect-requested, command-resolved, tick-settled, session-faulted, session-ended, input-edge-raised.

## Channels

`ChannelRegistry`: the only way to declare an engine's events is its `Install(session)`, which adds the registry last.
- Every request is answered in the same tick: by its serving component, or by the registry as the engine's unknown-channel refusal.
- Duplicate channel at `Init` → refused; component whose registry is not running → fault on first `Update`.
- `IsQuiet` false while a refusal waits, so no checkpoint between request and answer.

## Engine components

Calendar (`CalendarChannels`), Economy (`EconomyChannels`), Combat, Dialogue (`DialogueChannels`), Diplomacy (`DiplomacyChannels`), AI (`AiEventCatalog`), Relationship, Tutorial, WorldEvents, Alchemy (`AlchemyEventCatalog`), Mission (ScenarioEngine), Sim (`SimComponent`).
Bridges (`.Economy.Bridges`, `.Diplomacy.Bridges`, `.Relationship.Bridges`, `.Tutorial.Bridges`, `.WorldEvents.Bridges`, `.Calendar.Ai`) translate one engine's output into another's requests.

## Journal, checkpoint, replay

- `SessionJournal` records inputs + per-tick state hashes; `Serialize`/`Deserialize`; `JournalComponent` writes the hash.
- `JournalingRuntime` records transparently; `JournalReplay` returns `ReplayResult(Matches, DivergedAtTick, Code)`.
- `CompositeCheckpointService` combines mission + engine checkpoint parts; refuses when a component is not quiet.
- Each component exposes `Fingerprint()` and a checkpoint helper.

## Hosts

- Console/server: `EngineMainLoop` + `OutputLog`.
- Headless: `GameRuntime.Headless` and per-game proofs (`Economy.Headless`, `AlchemySim.Headless`, `Field.Headless`, `Briefing.Headless`, `CharacterBench`) — full sessions with scripted policies.
- Godot: `GameRuntime.Godot/GameFramework.cs` drives `RuntimeSession` from the physics loop; `GodotEventPort`, `ComponentNode`, `GameProfile`, `GodotLogger`; reference `project.godot`/`Main.tscn`.

Tests: `dotnet run --project src/GameRuntime/GameRuntime.Tests -c Release`; each component has its own test project.
