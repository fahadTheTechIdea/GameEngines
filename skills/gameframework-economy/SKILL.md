---
name: gameframework-economy
description: "Use GameFrameWork's EconomyEngine for markets: goods with elasticities, bands and seeded noise, production recipes and lines with inputs, requirements, tool wear and margin throttling, trade with taxes and subsidies, routes and landed cost, currencies, loans, wages and settlement, shocks and modifiers, facts and per-number explanations. Use when a game has market prices, trading, supply contracts or an economy that reacts to events."
---

# EconomyEngine

Reference: `docs/engines/EconomyEngine.html`; model and balance history `docs/ECONOMY_ENGINE.md`.
Code: `src/EconomyEngine/EconomyEngine.Core` (net8.0); component `src/GameRuntime/GameRuntime.Economy` (+ `.Bridges`).

## Boundary

- Owns: prices, stock, supply/demand, production lines, trade arithmetic, routes/landed cost, currencies, loans, shocks, modifiers, wear, conservation.
- Does not own: agents/pathfinding, market UI, quest rewards, strategic simulation.
- Whole-number per-mille arithmetic; only the document seed for noise. No floats, no hidden random state.

## Load (format 6)

```
var set     = EconomyLoader.Load(json);          // formats 1–5 are refused
var runtime = EconomyRuntime.Create(set, startStep);
EconomyChanges changes = runtime.Advance(toStep, hostFacts);   // closes ≤ 64 periods
```

Sections: `seed`, `time` (`stepsPerDay`, `periodSteps`), `limits`, `price`, `routing` (`direct`|`landed`), `currencies`, `goods`, `bands`, `recipes`, `schedules`, `policies`, `modifiers`, `shocks`, `markets` (pairs, policies, budget, lines), `routes`, `loans`, `facts`.
Start from `samples/workshop.economy.json` or `town.economy.json`.

## Markets and prices

- A pair is (good, production, consumption, stock, cover, capacity). Price moves toward a reference price by elasticity, band, speed, inertia, decay, bounded step and optional seeded noise.
- `Quote(market, good, quantity)` prices without executing; `Trade` executes and returns unit price, value, tax, total.
- `PriceHistory`, `Ratio`, `Reach(market, pair)` for charts and routing hints.

## Production

- A line runs a recipe up to capacity; the first blocker decides the outcome: `Locked`, `Capacity`, `Input`, `Room`, `Held`, `Margin`, `Worn`.
- `PlanLines(market, hostFacts)`, `MarginOfLine(market, recipe, hostFacts)` → `InputCost`, `OutputValue`, `Ratio`, `Fraction`, `Wage`, `Overhead`.
- `RequirePairs` are per-run requirements; a requirement with `Wear` degrades a shared tool and blocks the line when spent.
- `ThrottleDef` stops below `Stop` margin and runs fully at `Full`; `UnlockRecipe`/`IsUnlocked` gate progression.

## Trade, budgets and settlement

Policies: `Sales` tax, `Import`/`Export` tariff, `Subsidy`. Budgets: `Wage`, `Upkeep` (schedule, amount, per-head, indexed).
`Settle(scheduleId)` pays taxes, wages, upkeep, subsidies and loans. Arrears are penalised (`price.arrearsPenalty`); `InArrears(market)` answers.

## Routes

With `routing.mode = landed`, price reflects the cheapest source: delay, capacity, cost per weight, margin, share.
`Reach`, `PlanRoute(route)` → `RouteLine` (good, rank, gain, ratio, landed, via, surplus, wanted, shipped, outcome), `CapacityNow(route)`. Shipments are capped and arrive after their delay.

## Money

`ReferencePrice(currency, good)`, `SetGrowth(currencyId, ppm)`, `Fund(market, delta)`.
`Lend(id, lender, borrower, principal, ratePpm, term, kind, schedule, defaultAfter)`; kinds Annuity/Bullet; outcomes Missed/Closed/Defaulted.
`Conservation()` must always hold: treasuries + world account = starting money + minted.

## Shocks and modifiers

`ApplyShock(shockId, marketId, scalePermille)` with effects Supply/Demand/Price/Rate/Growth/Route. Modifiers are conditional supply/demand changes over host facts. Active shocks are readable.

## Facts and explanation

`EconomyFacts.Read(runtime, marketId, prefix)` → `market.*`, `currency.*` (prices, stock, ratio, band, arrears, shortage, level, rate). Reserved prefixes guarded by `EconomyFacts.IsReserved`.
`EconomyExplain` returns `Explanation(Subject, Terms, Value, State)`; use it whenever a price or outcome is shown to a player.

## Determinism and save

`EconomyImage` + `EconomySave` verify document digest (`ECON_SAVE_DOCUMENT`) and state digest (`ECON_SAVE_STATE`). `StateDigest()` is stable for equal states.

## Runtime component

`EconomyChannels.Install(session)` before `Init`. Requests: trade, shock, settle, growth, fund, lend, quote, price, recipe-unlock. Results: applied/refused, trade-executed, quoted, price-answered, stepped, rate-changed, shock-changed, shipment-arrived, band-changed, shortage-changed, shipment-launched, route-blocked, settled, loan-changed, production-limited, recipe-unlocked. `EconomyClocks` advances on the session clock; journal/transcript/replay and `EconomyCheckpoint` support verification.

## CLI

`check`; `simulate <scenario> <periods> [--save]`; multi-seed sweep `--seeds n [--from-seed] [--days] [--metrics] [--rows]`; `explain price|loan|currency|route|reach|production`; `coverage`. Exit 0 done, 1 refused/incomplete, 2 usage/file problem.

Tests: `dotnet run --project src/EconomyEngine/EconomyEngine.Tests -c Release`.
