---
name: gameframework-common
description: "Use EngineCommon instead of writing shared plumbing again: strict JSON reading with coded refusals, the one condition grammar over facts, stateless seeded rolls, SHA-256 digests, localisation tables with CLDR plurals/RTL and coverage checks, graph algorithms (CSR, Dijkstra/Bellman-Ford, flow, topological order, communities, trust), balance seed sweeps and rare-event splitting, the offline tail tool, and Roslyn source guards for tests. Use when an engine or game needs any of these."
---

# EngineCommon

Reference: `docs/engines/EngineCommon.html`; engine dependency map `docs/ENGINE_GRAPH.md`.
Code: `src/EngineCommon/EngineCommon.Core` (net8.0), `.Graph`, `.Balance`, `.TailTool`, `.TestSupport`.

## Rule

If two engines need the same rule, it lives here — never copied. EngineCommon references no engine; facts come from `ScenarioEngine.Contracts`.

## Strict documents

```
var json = new StrictJson(MyCodes.ForDocument);   // RefusalCodes: format/unknown/missing/range/duplicate/reference
var root = json.Object(element, "market", ["id", "currency"], "policies");
long n    = json.Long(root, "treasury", "market", 0, MaxMoney);
string id = json.Id(root, "id", "market");
```

Every required field present, no unknown field, no duplicate, whole numbers range-checked, ids on a fixed alphabet. Nothing is repaired or defaulted: throw `EngineRefusedException(code, message)` with the first failing code.

## Conditions

`Conditions.Read(element, where, facts)` validates the shared grammar (`all`, `any`, `not`, `{fact, op, value}`) against the engine's declared facts; `Conditions.FactsOf` lists what to gather; `Conditions.Evaluate` runs it. `ConditionExplanation.Describe` renders why.

## Rolls and digests

`DeterministicRoll.Next(seed, stream, index)`, `Below(seed, stream, index, bound)`, `Permille(...)`: SplitMix64 over FNV-1a, pure, order-independent. Never use `Random` or a clock.
`Digest.Of(text|bytes)` = upper-case SHA-256 hex, the digest every document and save carries.

## Locale

- `LocaleTable.Load(text)` — strict format 1 (`locale`, `messages`), text or form object with `select`/`other`/CLDR categories.
- `LocaleRenderer.Render(table, key, args)` — CLDR cardinal/ordinal by number, or value selection by text; missing key/placeholder refused.
- `PluralRules.Categories(locale)`, `TextDirection.IsRightToLeft(locale)`, `LocaleChain` fallback.
- `CoverageRules.Report(keys, table, notGiven)` then `Check` — every used key present, no unknown key, no placeholder the key does not give.

## Graph

CSR `CsrGraph` + reusable `GraphScratch`; limits in `GraphLimits` (200k nodes, 2M edges).
`GraphBuilder.Create/Add/AddUndirected/Build`; `Dijkstra.Run` / `BellmanFord.Run` → `ShortestPaths.PathTo`; `Bfs.Run`, `Components.Run`; `TopologicalSort.Run`; `MinCostFlow.Run` + `Decompose`; `Degrees.Run` + `DegreeTable.Top`; `LabelPropagation.Run`; `TrustIteration.Run`. Every result has `Explain()` and a refusal code.

## Balance

`SeedSweep.Run(options, perSeed)` / `RunWithEvents` + `Render`; `SeedDocument.WithSeed`; `FixedEffortSplitting` + `DerivedSeed`; `AutoEfforts.Plan`; `ConditionalEstimator.Run`; `SplitSummary.Of`; `SampleStats`; `IntegerRoot`. Codes in `BalanceCodes` (`MaxSeeds` 100k, `MaxEffort` 5k, replications 6..1000).

## Tail tool

`EngineCommon.TailTool` measures tail shape offline: `PeaksOverThreshold`, `DipTest`, `SilvermanTest`, `ModalityCheck`, `BoundCheck`, driven by `TailCli`.

## Test guards

`EngineCommon.TestSupport/SyntaxGuards` — Roslyn checks for no wall clock, no coalescing that hides a refusal, no duplicated rules, repository rules. A source guard is a real test only when it can fail.

Tests: `dotnet run --project src/EngineCommon/EngineCommon.Tests -c Release` (also `.Graph.Tests`, `.Balance.Tests`).
