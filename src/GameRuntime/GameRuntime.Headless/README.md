# GameRuntime.Headless

A small tycoon that runs on the game layer and nothing else: one `RuntimeSession`, one clock (`RuntimeClock`), and components whose `Update` runs once per clock tick, like a game-engine main loop. SimEngine runs the company's books, ScenarioEngine runs the mission, and a player plays.

## How a tick works

The session owns the clock. Each pass of the host's main loop reads how much time went by and calls `runtime.Update(elapsed)`. The session turns that into whole ticks and, for each tick, updates the components in order:

| order | component | job |
|---|---|---|
| 10 | `InputStateBridge` | puts input edges that arrive as events (a journal replay) into the input state |
| 100 | `SimComponent` | one `SimGame.Update(tick)`; publishes what the world announced |
| 150 | `TycoonObservations` | puts `sim.event.*` into the mission layer's `gf_tycoon.*` vocabulary |
| 200 | `MissionComponent` | one ScenarioEngine tick; publishes mission events and world requests |
| 250 | `PlayerComponent` | polls the input state, learns from events, raises commands |
| 300 | `SimEffectDispatcher` | turns mission requests (`sim.credit`) into director commands for the next sim tick |
| 900 | `JournalComponent` | records the state hash after the tick (only when journaling) |

`UpdateContext` carries the tick (elapsed ticks), the fixed delta per tick and the total game time (`TotalMicroseconds`, the XNA `GameTime` equivalent). Components use these and never read a wall clock.

## Nothing happens outside Update

Raising an event or writing input only queues it. `Submit` queues an event for the start of the next tick; `Emit` queues for delivery after the emitting Update returns; `InputState` just records edges and held state. The game changes only inside an Update, in the tick that delivers or polls the input. The tests in `DeferredDeliveryTests` prove it.

## The player

The player is a component that behaves like an XNA `Update` polling `Keyboard.GetState` or a Godot node polling `Input`:

1. The outside world (the host's script, or a bot) writes the `InputState` between Updates: `Press`, `Release`. Each press is a just-pressed edge, read once per poll, plus held state.
2. `PlayerComponent.Update` polls it (edges once, held state every tick) and raises events through the event port (`MissionCommandRequested`, `ClockRequested`).
3. A policy (`--player greedy|cautious`) decides from what the player learned from earlier events (founded, cash-changed, objective-changed, mission events, command resolved, session ended). It does not act directly: it presses keys on the same `InputState`, which the next tick polls.

So everything the player does is an input edge. The journal records each edge with the tick whose Update first polls it, and a replay submits the edges again at those ticks; no policy runs in a replay.

- `greedy` speeds the game up and sells a big lot every tick without waiting for answers. It runs into `NO_STOCK`, halves its lot after each refusal and tries again.
- `cautious` sells a small lot, waits for the mission's answer and a cooldown, and never changes the speed.

The lot size of each policy comes from `--seed` (SplitMix64); nothing else is random.

## Host main loop

Real-time mode (default) reads `Stopwatch` and feeds the elapsed time to `runtime.Update`. `--frame-ms N` feeds fixed frames of virtual time instead, which makes a run reproducible (used by the tests and by replays). Script times (`at <seconds> ...`) are host seconds in either mode.

```
GameRuntime.Headless --frame-ms 100 --seconds 30 --player greedy
GameRuntime.Headless --frame-ms 100 --script play.txt --journal run.journal
GameRuntime.Headless --replay run.journal
```

Script lines: `at 2.5 sell 3` (a tap), `at 3 autosell 2` / `at 5 autosell off` (hold the key down), `at 7 speed 2/1`, `at 8 pause`, `at 9 resume` (pause and resume work the host's own clock control).

Exit codes: 0 ended or time reached | 1 runtime fault | 2 usage/content/file refusal | 3 checkpoint refused | 4 journal refused or replay diverged.

## Godot

`InputState` and `PlayerComponent` have no engine dependency, so a Godot `ComponentNode` can host the same player: its `_UnhandledInput` (or an `InputMap` action handler) calls `Press`/`Release` with the action names in `PlayerActions`, and `_PhysicsProcess` updates the component once per physics tick. The existing Godot demo (`Main.cs`) still maps an `InputMap` action straight to a `MissionCommandRequested` through `Port.Emit`; moving it onto `InputState` has not been done.

## Limits

- `--load` cannot be combined with `--player`: a policy keeps its own state, which a checkpoint does not contain. `--save` waits (up to 4 ticks) for open director tickets and pending input to settle.
- `--journal` cannot be combined with `--load`; a replay needs the same `--seed` as the recording.
- `pause`/`resume` and the speed a player asks for are pacing; they are not journaled and do not change what a tick computes.
- Real-time runs are not reproducible; use `--frame-ms` for that.
