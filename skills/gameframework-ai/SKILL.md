---
name: gameframework-ai
description: "Use GameFrameWork's AIEngine for deterministic non-player decisions: behaviour trees, utility and random nodes, percept memory, typed keys and intents, a budgeted scheduler, save/restore and decision traces, plus the GameRuntime.AI component that carries requests and intents as events. Use when an agent must choose what to do without executing it."
---

# AIEngine

Reference: `docs/engines/AIEngine.html` (source-verified overview) and `docs/AI_ENGINE.md` (research and design record).
Code: `src/AIEngine/AIEngine.Core` (net8.0), `src/AIEngine/AIEngine.Cli`, component `src/GameRuntime/GameRuntime.AI`.

## Boundary

- **AIEngine decides, nothing else.** It emits a named intent with typed arguments. Movement, damage, spending and pathing belong to the component that owns the concern.
- **The host supplies all knowledge.** `Perceive`, `Lose`, `SetKey`; the engine never reads a clock or the world. A step is a function of state, tick and budget.
- **The game owns meaning.** Adapters translate intents into their own requests and write what happened back into knowledge; they hold no thresholds or target rules.

## Document format 1

Strict JSON; unknown fields are refused. Sections: `keys` (`source` = `host` or `agent`), `percepts` (typed attrs, unknown values, `memory`, `capacity`, `ownerCapacity`, `owned`), `intents` (typed args), `behaviors` (`every` ticks + `root`).
Node kinds: `sequence`, `selector`, `condition`, `let`, `act`, `invert`, `succeed`, `cooldown`, `utility`, `random`, `forEach`, `count`, `select`, `adjust`.
Start from `src/AIEngine/samples/guard.ai.json`; check it before shipping.

## Runtime contract

```
var set = AiLoader.Load(json);          // validated + digest of accepted text
var ai  = new AiRuntime(set, seed);
ai.Add("guard-1", "guard", tick);
ai.Perceive("guard-1", "enemy", "e1", owner, attrs, tick);
ai.SetKey("guard-1", "threat", FactValue.From(15));
StepReport r = ai.Step(tick, budget);   // oldest due first; intents in r.Intents
```

- Agent ids: 1–128 chars of `a-z 0-9 - . _`.
- `budget` must cover `set.MaxCost` (`AI_BUDGET_TOO_SMALL`); a think starts only when the remaining budget covers that behaviour's static bound.
- A due agent that does not fit stays due, is reported in `Deferred`, and goes first next step.
- Ticks never go backwards (`AI_TICK_ORDER`); cap is `AiRuntime.TickLimit`.
- Randomness is a seeded stateless roll; never add `Random` or wall-clock time.

## Save, restore and explanation

- `AiSave.Capture(runtime)` → `AiSave.Write(image)` → `AiSave.Restore(set, bytes, out runtime)`.
- Restore verifies the document digest (`AI_SAVE_DOCUMENT`) and the state digest (`AI_SAVE_STATE`); never edit an image by hand.
- `Trace(agent, true)` then `Explain(agent)` returns the last `ThinkTrace`. Tracing must not change a decision or a save.

## GameRuntime integration

`AiComponent` queues request events (`ai.agent-add-requested`, `ai.agent-remove-requested`, `ai.percept-posted`, `ai.percept-lost`, `ai.key-posted`) and applies them in `Update` in arrival order, then steps within the configured budget. It emits `ai.intent-raised`, `ai.step-done` and `ai.refused`. Declare `AiEventCatalog` before `Init`. A checkpoint is consistent only when `IsQuiet` is true; `Fingerprint()` identifies the state.

## Authoring loop

1. Write or edit the document; run `AIEngine.Cli check <doc>` and fix every refusal code.
2. Run `AIEngine.Cli explain <doc> <scenario>` and read the path, not just the outcome.
3. For model-produced logic, compile the integer decision tree offline with `import-tree`; never evaluate a draft at run time.
4. Add or update a test in `AIEngine.Tests` for the behaviour you changed.

Refusal codes (`AiCodes`): `AI_FORMAT`, `AI_UNKNOWN_FIELD`, `AI_MISSING_FIELD`, `AI_RANGE`, `AI_DUPLICATE`, `AI_REFERENCE`, `AI_VALUE_TYPE`, `AI_EXPR_TYPE`, `AI_TOO_DEEP`, `AI_TOO_MANY_NODES`, `AI_CANNOT_WAIT`, `AI_WRONG_WRITER`, `AI_OVERFLOW`, `AI_DIVISOR`, `AI_RESERVED_NAME`, agent/percept/key/budget/tick codes, and the save codes.
