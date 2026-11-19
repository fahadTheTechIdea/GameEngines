# Remaining game-layer engine work: plan

Written 2026-10-31 after pass 52. Plan only when written: nothing here was built yet, and no code, project or solution entry was added by this document. (Superseded as a status line by pass 53 onward: A, B1, B5, C1 and C6 are built, each marked "Status" below, and the tracker block of each pass says what was run; read the Status marks, not this sentence.) The tracker for what has landed is the "Implementation record" of `docs/RUNTIME_PIPELINE_INTEGRATION_PLAN.md` (passes are recorded there as "fifty-third pass" and onward, one block per pass, numbered "pass 53" from the fifty-third entry). Algorithm decisions and rejected ideas stay in `docs/ENGINE_ALGORITHM_REVIEW.md`.

## 0. Where things stand

Built and verified with focused tests: Calendar, Relationship, Economy, Tutorial, WorldEvents and Diplomacy (the `/goal`), then algorithm passes 40 to 52 (graph library, routing, Hawkes hazards, social state, production chains, balance harness, rare-event splitting, profit margin, tool wear, tail-tool shape checks). Everything is uncommitted on `main`.

This plan covers what the passes left over. Nothing in it blocks a game; each item is optional and has a stated consumer or a stated reason to wait.

## 1. Rules for every pass (unchanged)

- One branch, `main`. No commits, pushes, worktrees or Actions unless asked.
- Research first, with reached / snippet / unreachable stated honestly, and an adopt / adapt / avoid decision recorded in the engine's doc.
- Acceptance criteria written before code. Oracle tests with hand-worked or brute-force answers. A new test must be shown to fail without the logic (mutation round of at most 25 to 30 mutants, outside the tree, real SHA-256 check after each restore, per-mutant timeout, tree clean at the end).
- Focused tests only: `scripts/focused-tests.ps1 -Changed <paths> -List`, then run just those. No full suite, no full solution rebuild. Timing tests (Dialogue `CostTests`, soldiers budget, Relationship `ScaleTests`) are never a gate.
- A change in meaning of a document or save bumps its format number and the old one is refused by name with its own code (`ECON_FORMAT_4`, `REL_FORMAT_2` are the models).
- User's global rules apply: no stubs, every catch reports, no throwing default arms, no `?? throw`, one responsible class per capability, an addition arrives with its consumer.
- Parallel passes own disjoint folders and append their own tracker block (never overwrite). Re-read `GameFrameWork.slnx` right before editing it.

## 2. Work packages

### A. AIEngine: integer-tree export and the LLM-draft pipeline (review item 8)

**Status: built 2026-10-31 (pass 53)** — `import-tree`, interchange, capability matrix, exactness and mutation tests, pipeline and worked example in `docs/AI_ENGINE.md` section 8; no `AIEngine.Core` change; the sample is not yet human-reviewed.

**Why.** The review (sections 4 and 5) allows offline-trained models only as quantised integer data run by the engines that exist. A decision tree is already a behaviour tree plus utility table that AIEngine runs, so no inference engine is added. An LLM may draft content only offline, ending in a human-reviewed file; never at run time (replay, cost and validation reasons in the review).

**Scope.**
1. `AIEngine.Cli` command `import-tree` (name to be settled in the pass): reads a trained tree from a strict, documented JSON interchange (nodes with feature id, integer threshold, left / right, leaf action and integer weight), and writes an AIEngine document (behaviour tree or utility table) plus a trace map from each node to its source node. Thresholds are integers already; a float tree is refused with a named code rather than rounded silently. The interchange schema is defined here; no Python or ML library is added to the repo.
2. Capability report in the style of the Alchemy / Sim / Economy converters: supported / reduced / refused with a code for each feature (categorical splits, missing-value routing, depth over the engine's bound, more leaves than the budget).
3. Exactness test: a reference evaluator over the interchange tree and the AIEngine run of the exported document agree on every point of a generated grid and on 10,000 seeded points; the AIEngine explain trace names the source node.
4. Documented pipeline in `docs/AI_ENGINE.md` (new section): LLM or tool proposes a draft as a strict JSON file or locale table, the owning engine's `check` CLI (schema, bounds, refusal codes), `coverage` for locale gaps, human review with the `explain` / `simulate` output beside it, merge as ordinary data, provenance note (tool, date, reviewer) in the doc's evidence and never in the runtime. Numbers (rates, prices, weights) stay human-set and are checked by the balance harness. Content-warning and licensing review as the Dialogue engine already requires.
5. One worked example end to end on a sample (a hand-written draft file, not model output presented as such), showing a refusal, a fix and a pass.

**Touches.** `src/AIEngine/AIEngine.Cli` (new partial file), `src/AIEngine/AIEngine.Tests` (or a CLI test class there), `docs/AI_ENGINE.md`, one small sample. No runtime change to `AIEngine.Core`; if the pass finds one is needed, stop and record it as a decision.

**Out of scope, stated.** Training code, any model file format other than the one interchange, any call to an LLM from the repo.

**Acceptance.** The criteria above, plus: refusal matrix covers every new code; AI hashes and counts unchanged (`AIEngine.Tests` count may only grow).

**Focused tests.** `-Changed src/AIEngine` (selects AIEngine.Tests and the connected runtime AI bridge tests).

### B. Economy leftovers (each independent; one pass, or two if the decision gates below open)

| # | Item | Decision now | Work |
|---|---|---|---|
| B1 | Catalog `durability.uses` mapping | **Do.** The catalog carries the field; pass 51 left it reduced. | Research how the catalog defines a resource's total life vs a recipe tool's `uses`. Convert an item's total life to a tool stock's lifetime (or report it reduced with a precise code if the two meanings differ). Converter tests, `docs/RESOURCE_RECIPE_CATALOG.md` row. | **Status (pass 54, 2026-10-31): done as the "meanings differ" branch; mutation check complete (14 of 14 mutants run; 12 killed at first, 2 survivors exposed test holes, tests strengthened, then all killed).** `durability.uses` is not read; own code `CATALOG_ECONOMY_DURABILITY_NOT_MAPPED`, tested, no format bump (`ECONOMY_ENGINE.md` 16.7). |
| B2 | Rebalance the isles economy | **Do after B1** (so its numbers use final rules). | Same method as the town: `simulate --seeds`, authored numbers only, before / after csv in `docs/measurements`. The Diplomacy demo hash will change and must be reported; pins in the Diplomacy tests updated. **Status: built 2026-10-31 (pass 57)**: isles numbers rebalanced (fair band 10-11 percent to 69-77 percent, no pair at the ceiling, 0.7 percent short; two exporters still at the floor, an engine property), `docs/ECONOMY_ENGINE.md` 17.3, `docs/measurements/economy-rebalance-isles-pass57-2026-10-31.csv`, new Diplomacy demo hash `AE67CE0F...9846`, guard `IslesEconomyBalanceTests`. |
| B3 | A tool shared across lines | **Do.** Lines check in order and wear by their own runs; two lines needing one hammer can both count the same stock as present. | A deterministic reservation rule within a period (first line in order takes the use, the next sees the remainder), explain term, tests with a 600-case oracle as in pass 51. Likely a format bump (state meaning changes); decide in the pass and refuse the old format by name. **Status: built 2026-10-31 (pass 57)**: the first line that runs holds a required good for the period, wear falls on the wearing line, format 6 (`ECON_FORMAT_5` / `ECON_SAVE_FORMAT_5`), `docs/ECONOMY_ENGINE.md` 17.1-17.2, `SharedToolTests` with a 600-case oracle; the Victoria 3 and Anno sources were not reached. |
| B4 | Tax or subsidy on recipe output | **Defer, no consumer.** Sales tax already reaches a line through demand, and the subsidy is paid on a pair's own production. A new levy is a new engine rule. | Reopen only when a Diplomacy treaty or policy feature names it as a consumer. Record the trigger in the Economy doc. |
| B5 | Self-adjusting wages and fixed costs | **Keep excluded.** Fixed costs are excluded by the shutdown rule (OpenStax 8.2); self-adjusting wages would be a second price rule. | Add one paragraph to `ECONOMY_ENGINE.md` section 16 stating the decision and the reopen condition (a labour market feature). No code. | **Status (pass 54, 2026-10-31): done.** Decision paragraph and reopen condition in `ECONOMY_ENGINE.md` 16.7; B4 deferral recorded there too. |

**Touches.** `src/EconomyEngine/*`, `src/GameContent/GameContent.Catalog.Economy`, Economy / Diplomacy samples, `docs/ECONOMY_ENGINE.md`, `docs/RESOURCE_RECIPE_CATALOG.md`, `docs/measurements`.

**Acceptance.** Per item: criteria in the pass, converter capability levels updated, demo hashes explained and run twice, checkpoint replay equal.

**Focused tests.** `-Changed src/EconomyEngine,src/GameContent/GameContent.Catalog.Economy,src/DiplomacyEngine` (then the connected runtime projects the script selects).

### C. Rare-event and tail tools

| # | Item | Decision now | Work |
|---|---|---|---|
| C1 | Conditional estimator from the ladder's tails | **Do first.** Pass 52 showed the rung is one draw inside a known bound, so the exact ladder conditioning has less variance than splitting. | Estimate a rung's probability by integrating the engine's own onset chance and ladder tails over the observed hazard-onset opportunities; integer-only, same report format as `--rare`; validate on the lab world's exact value and the gambler's ruin analogue; compare variance per engine step with pass 52. **Status: built, pass 55 (2026-10-31)**: `--rare hazard:rung --conditional` (`WORLD_EVENTS_ENGINE.md` section 15). Exact for the engine's own trials (Wald identity for the stopped compensator), supports Hawkes, conditions, exclusions and pacing; lab values agree to 0.002% to 0.07%, the 17-of-20 band standard is met in 3 of 4 lab cases and **not at 20 ppm (7 of 20: the band is invalid for a point-mass estimate)**; 900 to 23,000 times better than pre-draw / plain in variance x steps on the valley. Gambler's ruin is not applicable (the event builds up; a self-exciting chain with an exact dynamic-programming value stands in). |
| C2 | Automatic choice of efforts | **Do after C1** (it needs the estimator's variance as a pilot). | A pilot stage that sets per-stage efforts from the pilot's survival counts, documented rule, deterministic. |
| C3 | Hartigan dip and Silverman tests | **Do**, offline in `EngineCommon.TailTool` only. Pass 52 used a gap test and says so. | Research (primary sources before coding); implement; calibration on simulated unimodal and mixture samples (false-alarm rate, power) next to the gap test; keep the gap test unless the new test is shown better. |
| C4 | Splitting on escalation | **Defer, no consumer.** Escalation is not part of the onset event; splitting needs a random process after the onset. | Reopen if WorldEvents gains stochastic escalation. |
| C5 | Run on a second machine / Linux | **Do when a Linux environment is available**; it cannot be faked. | Run the determinism-sensitive commands (demo hashes, `simulate --seeds`, `--rare`) and compare outputs byte for byte; record in `docs/measurements`. If no Linux host is available, record the item as unverified, not done. |
| C6 | Doc pointer note in `WORLD_EVENTS_ENGINE.md` 13.6 | **Do** (one edit, with C1). | Add a pointer to section 14 and to the conditional estimator. **Status: done, pass 55**: pointer in 13.6, new section 15, review paragraph updated. |

**Touches.** `src/EngineCommon/EngineCommon.Balance` (+ tests), `src/EngineCommon/EngineCommon.TailTool`, `src/WorldEventsEngine/WorldEventsEngine.Cli`, `docs/WORLD_EVENTS_ENGINE.md`, `docs/ENGINE_ALGORITHM_REVIEW.md`.

**Acceptance.** Reported estimates cover the exact lab value in at least 17 of 20 independent estimates (the pass 50 / 52 standard); all earlier tail-tool outputs for valid input unchanged digit for digit.

**Focused tests.** `-Changed src/EngineCommon/EngineCommon.Balance,src/EngineCommon/EngineCommon.TailTool,src/WorldEventsEngine`.

### D. Parked: need an explicit go-ahead and a hook first

| Item | Why it is parked | What must exist before it starts |
|---|---|---|
| WorldEvents to SimEngine incident bridge | SimEngine has only a `RaiseIncidentCommand` hook and no incident content. | A decision on what incidents exist (content), then a bridge that maps world events to that command. SimEngine changes follow its own process (build-id bump, tracker entry). |
| Diplomacy to Combat bridge | Combat has no hook for treaty or war state. | A Combat-side contract (which facts it reads), agreed first. |
| SpatialEngine | Parked by the owner ("Leave this for later"). | Owner go-ahead. |
| Godot hosting | Parked by the owner. | Owner go-ahead; Godot-native rules apply (no custom replacement of engine features). |
| Subsystem damage, Ink real-compiler testing | Earlier parks. | Owner go-ahead; Ink needs the real compiler available. |

## 3. Order and parallelism

1. **C1 + C6** and **B1** can run in parallel (disjoint folders: tail / WorldEvents vs Economy / catalog).
2. **A** can run at the same time as either (only `src/AIEngine` and its doc).
3. Then **C2** (after C1), **B2** (after B1) and **B3**.
4. **C3** any time (TailTool only); **B5** is one paragraph and rides with B1.
5. **C5** when a Linux host exists. **B4, C4 and group D** stay closed until their stated trigger.

Suggested passes: 53 = A; 54 = B1 + B5; 55 = C1 + C6; 56 = C2 + C3; 57 = B2 + B3. Passes 53, 54 and 55 touch disjoint folders and can run together.

## 4. Gates before calling a pass done

- Its checklist block is appended to the runtime plan's Implementation record, in the form of the earlier blocks (built, measured, tests run and not run, mutation result, not done).
- Focused tests for the connected projects pass, run by the orchestrator in the repo under the pinned SDK (`global.json`, 10.0.402 with roll-forward; 10.0.403 is installed).
- New demo hashes (if any) are explained, run twice the same, and checkpoint replay equals the live run.
- No leftover mutants: file hashes of the real tree match the pre-mutation copies.
- Any "not done" is named with its reason or its reopen trigger.

## 5. Risks

| Risk | Mitigation |
|---|---|
| A tree export that quietly rounds or truncates a model | Float trees are refused by code; the exactness test compares reference and engine on a dense grid. |
| Shared-folder collisions between parallel agents | Disjoint folders by package; each appends its own tracker block; `GameFrameWork.slnx` re-read before editing. |
| Machine load makes timing tests fail | Not a gate; measurement tests re-run alone when the machine is quiet. |
| Format bumps ripple into other engines' hash pins (Economy to WorldEvents and Diplomacy demos) | Each pass reports every demo hash it can move and updates the pins it owns. |
| Linux / second-machine claims | Recorded only when actually run on that host; otherwise "unverified". |
| Scope creep into parked items | Group D has explicit start conditions; a pass that finds it needs one stops and records it. |

## 6. Questions the plan settles by decision (no answer needed from the owner)

- Wage / fixed-cost modelling stays out (second price rule). Reopen condition recorded in B5.
- Recipe-output levy deferred until a named consumer (rule: an addition arrives with its consumer).
- Splitting on escalation deferred until escalation is stochastic.
- LLM output is never an input at run time; it is a draft that passes `check` and human review before it becomes ordinary data.
