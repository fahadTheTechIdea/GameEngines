# AIEngine: research, boundary, criteria and result

Pass 27, 2026-10-28. Written before the code; the result section at the end records what was built and measured.

## 1. What already decides things (evidence, and the decision)

Read in the source on 2026-10-28:

| Owner | What it decides | Where |
|---|---|---|
| `FieldCommander` | Which standing group fires at which standing foe (the nth own group at the nth foe, round the end), at what distance, cover, terrain and round length; when a round falls due (every 15 ticks, kept until the books are settled) | `GameRuntime.Field.Headless/FieldCommander.cs`, `Fire` and the `_roundDue` logic |
| `FieldQuartermaster` | When a battery is offered rounds (below 50 percent), a tank fuel (below 50), a group people (below 75), how much (no more than missing, no more than the depot still holds after earlier offers) | `FieldQuartermaster.cs` |
| `ISkirmishPolicy` bots | Aggressor (march then fight), skirmisher (close to nine tenths of range, then fight from light cover), defender (heavy cover, wait), melee (every group fires at the weakest foe, distance halves) | `GameRuntime.Combat.Headless/*Policy.cs` |
| `PlayerPolicies` (tycoon) | Greedy and cautious buying bots of the economy sample | `GameRuntime.Headless/PlayerPolicies.cs`; **not** moved in this pass (an economy bot, not the combat or field decision logic the brief names); noted as a later candidate |

Three separate hand-rolled decision implementations, each with its own thresholds, its own "when do I act" gating and no shared notion of what an agent knows. Decision: **one AIEngine owns how an agent decides** (behaviour trees with utility selection, a typed blackboard, a per-agent knowledge store), defined in JSON. The game layer keeps only what is not a decision: raising the field, request and answer bookkeeping, writing what happened into the agent's knowledge, and turning an agent's *intent* into a request event for the owning engine (combat, stock). Thresholds, targets and amounts live in the behaviour documents.

Conditions are not reimplemented: the AI engine reads and evaluates conditions through `EngineCommon.Conditions` (the strict reader of the ScenarioEngine grammar), the same one DialogueEngine uses.

## 2. Research (searched 2026-10-28)

Primary sources I actually read in this pass are marked P with what I took from them; where only a search result or a secondary page came back that is said.

- **Behaviour trees.** P: Kevin Dill, "Behavior Selection Algorithms: An Overview", *Game AI Pro* ch. 4 (text extracted from the published PDF). Trees run from the root each time behaviours are selected, so the running time is generally greater than a finite-state machine; trees are stateless unless memory is added deliberately, and "care must be taken when creating behaviors that appear to apply memory" (his example: a citizen running from a battle who then returns to it because the higher behaviour no longer fires); a child can be chosen by a utility-style selector; extra behaviour (on start / on finish, random selectors) is a common extension. Secondary only: the Colledanchise and Ögren book "Behavior Trees in Robotics and AI" (bibliographic record and abstract: BTs give modular, reactive behaviour where finite-state machines get brittle; I did not reach the control-flow node definitions); Damian Isla's Halo 2 talk (only through a lecture handout and citation lists: impulses, decorators such as loop limit, inverter and "ignorer that always reports success", a prioritised list where behaviours describe themselves).
- **Utility AI / IAUS.** P: Dill, same chapter: utility systems score every option from many inputs and pick the highest (or seed a weighted random pick), adapt well to changing context, are "somewhat unpredictable", can be "challenging to tune", and you must override utility with scripted actions where design needs a specific behaviour at a specific moment. Secondary only: gameai.com's page on Dave Mark's Infinite Axis Utility System (data-driven, behaviours "play together" as they are scored, used in Guild Wars 2: Heart of Thorns; the page does not give the curve or compensation formulas). I did **not** reach Mark's talk or book, so the compensation factor I use for combining considerations is written from memory of that work and is listed as my own adaptation, not a quotation (pass 29: the formula was wrong; see section 2a).
- **GOAP.** P: Jeff Orkin, "Three States and a Plan: The A.I. of F.E.A.R." (GDC 2006, text extracted from the PDF). The character state machine has three states; goals and actions are chosen by planning; actions are STRIPS-like with preconditions and effects; F.E.A.R.'s differences are a cost per action, no add and delete lists (a fixed-size world-state array instead), procedural preconditions and procedural effects; A* is used for plans as well as for paths, guided by the action costs; sensors keep a shared working memory up to date.
- **HTN.** P: Troy Humphreys, "Exploring HTN Planners through Example", *Game AI Pro* ch. 12 (text extracted). Total-order forward decomposition; a domain of compound tasks (with methods and conditions) and primitive tasks (operators with conditions and effects); a world state of properties kept by sensors and by the effects of completed tasks; a re-plan is forced when the plan finishes or fails, when there is no plan, or when a sensor changes the world state; a running task can be aborted for a higher-priority plan; the author notes that re-planning on every world-state change can keep re-planning.
- **Influence maps.** P: Dave Mark, "Modular Tactical Influence Maps", *Game AI Pro 2* ch. 30 (text extracted). Influence is propagated from each agent into a grid through a response curve of distance (linear, polynomial and others); the maps are zeroed and re-stamped at a chosen frequency (they updated once per second; not faster than 0.5 s, not slower than 2 s) and agents query them. Secondary (a GameDev.net tutorial): momentum biases a map toward its history, decay controls spread, propagation needs a second buffer.
- **Godot add-ons.** Partly read: LimboAI's class references and documentation index (search results and one how-to page: tasks return SUCCESS, RUNNING or FAILURE; a blackboard shared by the tasks of a tree with scopes; `elapsed_time` while a task runs; a debugger that shows the blackboard live) and Beehave's README (tree nodes in the scene tree attached to an actor; blackboard per tree, shareable; ActionLeaf and ConditionLeaf; an editor debug view). I did not read either add-on's tick or composite semantics in detail, so I take only the vocabulary (status values, blackboard, running state) and not their exact behaviour.
- **Deterministic AI in lockstep games.** P: "1500 Archers on a 28.8: Network Programming in Age of Empires and Beyond" (Bettner and Terrano, Gamasutra/Game Developer). Every machine runs the exact same simulation from identical commands; the random numbers are seeded and synchronised and the *number of calls* to random must match; randomness outside the simulation must not touch the simulation's generator; any divergence is detected as "out of sync"; AI and pathing were about 30 percent of processing and large paths or strategic planning made a turn fluctuate by as much as 200 ms, which they met by lengthening the turn, not by slicing the work. The article says nothing about floating point.

What this research did not give me: no primary source for IAUS formulas, none for the add-ons' internals, none for Isla beyond secondary notes. Where I rely on those, the table below says "adapted" and the criteria test my own behaviour, not theirs.

## 2a. Research gaps closed and corrections (pass 29, 2026-10-28)

What the first pass could not reach was looked up again. Marked as before: P = text actually read; the others say what they are.

- **IAUS compensation factor. My first version was wrong, and is fixed.** I had written the compensation from memory and applied it to the *product* of the considerations. P (the UIntel utility documentation page "Considerations", which cites Dave Mark's compensation factor and prints a table): scores are clamped to [0,1]; the plain product of n scores of 0.5 is 0.500, 0.250, 0.125, ... and with the compensation factor 0.500, 0.391, 0.296, 0.223, 0.168, 0.126, 0.095, 0.071, 0.053 for n = 1 to 9. My product-level formula gives 0.344 for n = 2, not 0.391. Applying it **to each score** (modification factor 1 - 1/n; make-up value (1 - s) x modification; s becomes s + make-up x s; then multiply) reproduces all nine published values (I computed each by hand: n = 2 gives 0.625 squared = 0.391, n = 3 gives 0.667 cubed = 0.296, n = 9 gives 0.722 to the ninth = 0.053). The code now does that, and a test pins the nine values (within 5 permille, the integer pipeline floors). What I still did not reach: Dave Mark's own slides or book, so the *formula text* is a reconstruction that matches the published *numbers*, not a quotation. I also raised the cap on considerations per option from 8 to 16 because the published table goes to 9.
- **Response curves.** The two pages I could read (the UIntel page and an article on IAUS) name linear, quadratic and logistic shapes in prose and give no equations; Mark''s "Behavior Curves" material was not reached. My curves (linear, quadratic, cubic, smoothstep, step, each invertible) are my own integer shapes; **smoothstep stands where the sources say logistic**, to keep everything in integers. That is a stated deviation, not a transcription.
- **Isla, "Handling Complexity in the Halo 2 AI" (GDC 2005). P** (the Game Developer article, summarised from its text): a hierarchical finite state machine built as a behaviour **DAG**; children compete through a **binary relevancy test** ("We will simplify this scheme considerably by making relevancy a binary test"), with prioritized-list the most common scheme; **impulses** are lightweight triggers that redirect to a behaviour at its native place; **stimulus behaviours** insert temporary subtrees for a second or two so they are checked against the priorities; relevancy is tested **every tick**, which is why cost is discussed; behaviour state is allocated **only for the active path** (about 192K for everything against about 25K); **props** hold perception data that "can diverge from the true world state"; tags and locks switch large parts of the tree off; "dithering" (flipping between actions) is the failure to avoid. What this changes here: (1) my `selector` is the prioritized list and `condition` is the binary relevancy, so those match; (2) `utility` and `momentum` are my addition for graded choices, and momentum is aimed at exactly Isla''s dithering; (3) the props remark supports keeping knowledge separate from truth; (4) my agents hold a slot for **every** node that needs memory, not only the active path; the cost is eight bytes a slot and a cleanup pass, measured in A7, and I did not change it; (5) I have no impulses or stimulus subtrees: a sensor posting a percept plus a condition near the top of a selector does the same job.
- **Beehave. P** (the source of `sequence.gd` and `sequence_reactive.gd` on the godot-4.x branch, read through a summary): `Sequence` keeps `successful_index` and `running_child`, **resumes at the running child**, skips children that already succeeded, and on failure or interruption resets; `SequenceReactive` **re-evaluates from the first child every tick**, and when a different child becomes the running one it **interrupts** the old one (an `interrupt()` call that cleans up). That is the pair my `resume: true` / `resume: false` names. The manual and the other composites (selectors, parallel) were not reached. **A real difference:** Beehave tells a running child it has been interrupted; my engine only clears its slot, so an abandoned wait raises no "cancel" intent. A behaviour that must undo an order has to do it with a condition next think. Recorded as a limit, not built.
- **LimboAI. P** (introduction and tree-creation pages): tasks return SUCCESS, FAILURE or RUNNING; the tree is processed each frame top to bottom; Sequence stops at the first FAILURE, Selector at the first SUCCESS; decorators have one child (AlwaysSucceed, Invert, TimeLimit are the examples); tasks share a Blackboard that has a scope chain; the debugger shows the live tree, each task''s properties and the blackboard, and lets you edit values. Not read: update modes, whether its composites remember the running child, plans. What this changes here: my `succeed`, `invert` and the act timeout match the named decorators; **the debugger is the model for the trace facility** (item 6 of this pass).
- **Dill, *Game AI Pro* ch. 4. P** (already used): utility systems "can be challenging to tune" and need overrides for scripted moments: the tree-over-utility arrangement here is that override.
## 2b. Reconsidered omissions, trace, tool and limits (pass 29)

- **GOAP, HTN, influence maps: still not built, decided on the repo, not on new reading.** I tried to read Orkin's GDC 2006 slides and Humphreys' HTN chapter again; both PDFs came back as compressed binary and my extraction produced no text, so **no new source text was read for them this pass** and the section 3 entries stand on the earlier reading. The decision rests on what is shipped: every behaviour here (field commander, quartermaster, skirmish bots, tycoon bots) is a reactive choice of one next action from a few facts; none has a multi-step goal a planner would search. A planner adds an unbounded search the static work bound cannot cover. **Influence maps:** a map needs a spatial grid; the only distance in the games is one scalar, and the grid belongs to the parked SpatialEngine (pass 28). Built now it would be unused code (rule 6). Reopen both when a shipped behaviour needs them.
- **Explain facility.** `AiRuntime.Trace(agent, on)` and `Explain(agent)` return the last think as a `ThinkTrace`: every node run, in order, with its status and what it decided (utility scores and the chosen option, momentum, values written, counts, the intent raised). At most 4,096 steps and 256 characters a detail, with a `Truncated` flag; the same state gives the same text; tracing changes no decision, cost, save or digest (tests compare 30 ticks and the save bytes). Model: the LimboAI debugger and Isla's call for transparency. Not saved, not a digest input.
- **Authoring tool.** `AIEngine.Cli check <document>` prints `ok` with each behaviour's node count and **maximum cost**, or `refused <CODE>: <where and why>` (exit 1); `explain <document> <scenario>` runs one think and prints the trace and intents; usage and file problems exit 2. It reads the three shipped documents (test).
- **Limits.** A percept kind now declares `ownerCapacity` (an owned kind must; an unowned one must not), enforced when told and when a save is opened, and a query by owner is bounded by it instead of by the whole kind: the field quartermaster's work bound fell from tens of millions to 240,005. The field document holds 2,000 groups (the command line raises at most 1,000 squads a side, one group each; a test pins the equality) and 32 batteries a group (64,000 total, under the engine's 65,536); a fight beyond either is refused before the first tick. The shipped catalogue's widest formation has 4 batteries. Quantities were clamped at 1e9 and now at 1e10: a load times 100 must fit the engine's 1e12 value limit, so 1e10 is the ceiling; the world's stock overflows before it (found by trying 1.2e10). **A defect found on the way:** a component's step budget equal to the largest behaviour bound let only one of the two commander agents think per tick; the budget is now the bound of every agent that can be due, and a test pins it.
## 3. Adopt, adapt, avoid

| Source idea | Decision |
|---|---|
| Behaviour tree statuses success / failure / running, composites sequence and selector, decorators, a blackboard (Dill, Isla, LimboAI, Beehave) | **Adopt.** Tree nodes carry a required id; running state is per node per agent and is part of the checkpoint |
| Trees are stateless and memory is a deliberate extension; the flip-flop trap (Dill) | **Adopt as a switch.** Each sequence / selector says `resume`: `true` remembers the running child, `false` re-evaluates from the first child each think. Never implicit |
| Trees re-run from the root every think and cost grows (Dill) | **Adapt.** Cost is counted in work units and bounded statically per behaviour; a scheduler spends a per-tick budget across agents |
| Utility selection inside a tree (Dill; IAUS) | **Adopt** as a `utility` node. **Adapt** the maths: integer permille only (no floats, so a lockstep run cannot drift between machines), curves linear / quadratic / cubic / smoothstep / step with an invert flag instead of floating logistic, a compensation factor when many considerations multiply (applied to each consideration score, corrected in pass 29; see section 2a) and a momentum bonus for the running option (hysteresis) |
| Weighted random pick from scores (Dill) | **Adopt** as a `random` node over fixed weights, seeded; **avoid** random over utility scores in this pass |
| GOAP planning with action costs and A* (Orkin) | **Not built.** Planning cost per agent is unbounded without a depth limit, plans are hard to predict for designers, and the field and skirmish decisions here are reactive. A planner would be a further node kind with its own budget; recorded as a decision, not as a gap |
| HTN decomposition with sensors forcing re-plans (Humphreys) | **Not built**, same reasons; the sensor idea is adopted as the knowledge store (below). The re-plan storm Humphreys warns about is avoided because a tree re-decides on a fixed think interval |
| Sensors write a shared working memory; the planner reads it (Orkin, Humphreys) | **Adopt**, strictly: an agent decides only from its **blackboard** and its **knowledge** (percepts the host wrote, aged out after a declared memory time). The engine has no reference to the world and no way to read it |
| Influence maps (Mark) | **Not built.** A map is a sensor service of the game layer: it would write percepts. Nothing in this pass needs one; adding it would be unused code |
| Floating-point scores | **Avoid** (determinism across machines is not guaranteed by the sources and the Age of Empires paper says nothing about it) |
| Update every agent every frame | **Avoid.** Each behaviour declares how often it thinks; the scheduler spreads agents with a seeded offset |
| Random seeded per agent from a shared generator | **Avoid.** A roll is a pure function of seed, agent, node and a per-agent roll counter, so asking in another order cannot change the result (the AoE paper's "number of calls to random must match" problem does not arise) |
| A tree that throws when something is wrong | **Avoid.** Documents are checked at load; run-time problems are refusals with codes and leave the state as it was |

## 4. Layer boundary

- `AIEngine.Core` (pure library): format, validation, static cost bound, the agent runtime (tree, utility, blackboard, knowledge), the scheduler with its budget, save and open. No clock, no files, no world, no Godot. The host calls `Perceive` / `Forget` / `SetKey`, calls `Step(tick, budget)` and receives **intents** (a named request with typed arguments). The engine never executes anything.
- `GameRuntime.AI` (game layer component): requests as events (add or remove an agent, percept, key), applied in `Update` in arrival order; then the step; intents leave as events; refusals are events; a checkpoint is refused while a request waits.
- The game components that own a concern (combat, stock) turn intents into their own request events. The field commander, the quartermaster and the skirmish bots become thin adapters: they write what happened into knowledge and keys and translate intents; they hold no thresholds and no target rule.

## 5. Document format 1 (summary)

`keys` (typed blackboard values with initial value and range; `source` says who may write: `host` or `agent`), `percepts` (kinds with typed attributes, a memory time in ticks, optional ownership), `intents` (names with typed arguments), `behaviors` (id, think interval, root node). Node kinds: `sequence`, `selector`, `condition` (a ScenarioEngine condition over `key.*`, `item.*`, `tick`), `let`, `act` (emit an intent; optionally wait for `until`, fail on `failWhen` or `timeout`), `invert`, `succeed`, `cooldown`, `utility`, `random`, `forEach` / `count` / `select` (queries over the knowledge of one percept kind, ordered by id, bounded by `limit`) and `adjust` (change a belief after acting on it). Expressions: literals, `key`, `item`, `known`, `tick`, and `add sub mul div mod min max concat`. Everything is integer or string or bool.

## 6. Acceptance criteria (fixed before coding)

| # | Criterion | Check |
|---|---|---|
| A1 | The format is strict: an unknown, missing or repeated field, a bad id, a number out of range, a wrong type and a wrong expression type are each refused with a distinct code and a message naming the place | One test per refusal code |
| A2 | References resolve and shapes are bounded: every key, percept kind, attribute, intent and argument, node ids unique per behaviour, tree depth and node count bounded, `forEach` / `select` / `count` need a `limit`, the child of a `forEach` can never wait, a host key is never written by the tree and an agent key never by the host | Tests per code |
| A3 | Static cost: every behaviour has a work-unit upper bound computed at load; a think never spends more than it | Property test: random documents and random states, spent ≤ bound |
| A4 | Deterministic: the same document, seed and inputs give the same intents however the agents were added and however often asked; random nodes differ by seed | Replay test, order-of-adding test, two-seed test, roll purity test |
| A5 | No cheating: the engine library references nothing of the game, and a decision depends only on blackboard and knowledge. A change to the world that was not perceived changes no decision; a percept that is stale is still acted on until its memory ends, then forgotten | Reference guard; behaviour tests |
| A6 | Budget: a step never spends more work units than its budget (strict); a budget smaller than the largest behaviour bound is refused; under overload every agent still thinks, oldest-due first, and the longest wait is reported | Tests; measured lateness |
| A7 | Scale: 1,000 agents on a think interval of 10 ticks cost under 2 ms per tick on average and allocate under 64 KB per tick; 5,000 agents are measured too | Benchmark with repeats, honest variance |
| A8 | Utility: scores are integers in permille; the best option wins, ties go to the lower id; the running option gets the momentum bonus so a small oscillating input does not flip it | Tests with a mutation that removes the bonus |
| A9 | Checkpoint: save carries format, document digest and state digest; changing any field, the document or the format is refused with its own code; a forged digest still cannot bring in a state the document could not have made; the continuation (running nodes, utility choice, cooldowns, knowledge, due ticks, roll counter) is identical to an uninterrupted run | Tamper tests; continuation test |
| A10 | Component: requests are answered in `Update` in arrival order, never at the moment of the request; intents are events; a refusal is an event; a checkpoint is refused while a request is open; a journal replays | Tests |
| A11 | One owner: `FieldCommander`, `FieldQuartermaster` and the skirmish policies hold no target rule, threshold or amount; their old behaviour is reproduced by AI documents with **identical output hashes** on every configuration the existing tests use, shown before the old code is deleted | Old versus new hash comparison, then a source guard |
| A12 | Every new test is mutation-checked | Recorded in the result |
| A13 | One headless demo wires a dialogue-driven briefing and quest to AI-driven squads and replays to the same hash | Demo run twice, hashes equal |

## 7. Result

**Built (2026-10-28).** `AIEngine.Core` (format 1; a strict loader with static types, proven ranges and a work-unit bound per behaviour; the runtime with trees, utility, blackboard, per-agent knowledge with memory and an owner index, a budgeted scheduler; save/open), `GameRuntime.AI` (events addressed to a component, the component, checkpoint, flat value encoding), two AI documents (`skirmish-bots.json`, `field-ai.json`) that **replace** the hand-written skirmish policies, the field commander's targeting and cadence and the field quartermaster's thresholds and amounts, and the combined demo.

| # | Result | Evidence |
|---|---|---|
| A1 | Met | `FormatTests`: about 75 document mistakes with their codes (every document code) |
| A2 | Met | references, unique node ids, depth 12 and 512 nodes, loop bodies cannot wait (one guard, not two), host keys never written by trees, agent keys never by the host, integer ranges proven to fit (overflow, divisor, assignment ranges, text length) |
| A3 | Met | `NodeCostTests` pins the bound exactly for count, select, loop, sequence and utility; `SchedulerTests` runs 900 random states and never exceeds it. The housekeeping that forgets old percepts is not counted in work units |
| A4 | Met | same document, seed and inputs give the same intents; the order agents were added in changes nothing (three orders); a random node's roll is independent of other agents; other seeds differ |
| A5 | Met, with a stated limit | the library references only EngineCommon and the ScenarioEngine contracts (guard test); stale knowledge is acted on until its memory ends; an unreported change in the world changes no decision. The engine cannot see the world by construction; what the sensor tells it is the game's responsibility |
| A6 | Met | the budget is strict (refused below the largest bound), oldest due first with ties by id, deferred count and longest wait reported; 100 agents wanting a think every 2 ticks on a budget for 30 thinks a tick all thought 10 to 20 times in 60 ticks, longest wait at most 8 ticks |
| A7 | Met, measured | Release, tiered compilation off, 100 ticks after a 40-tick warm-up, 5 repeats. **1,000 agents on an interval of 10: worker 0.083 to 0.149 ms a tick and 44.1 KB; commander 0.107 to 0.163 ms and 50.8 KB.** 5,000 agents: worker 0.471 to 0.774 ms and 220.4 KB; commander 0.581 to 0.704 ms and 242.3 KB. Target: under 2 ms and 64 KB for 1,000. The commander figure is mostly its cheap path (it orders only on a beat); the busy path is exercised by the field-game runs, not by this benchmark |
| A8 | Met | integer curves (14 cases), compensation checked against the nine published values (500, 391, 296, 223, 168, 126, 95, 71, 53 permille for a score of one half with 1 to 9 considerations; ours agree within 5), weight, momentum (with the run that flips without it), ties to the lower id |
| A9 | Met | continuation identical (running act, cooldown, rolls, knowledge with ticks), damage per field, forged digest caught by state rules (17 cases) |
| A10 | Met | `AiComponentTests` (18): queued until Update, arrival order, requests addressed by component id, refusals as events, strict budget, checkpoint refused inside a tick, resumed session identical, journal replay verified and a changed percept found at its tick |
| A11 | Met | **41 configurations** (20 skirmish bots, 21 field games, 7 of them under scarce supply) print the same `output-sha256` as the hand-written classes did; those classes were then deleted. For the scarce-supply set the hand-written quartermaster was restored temporarily to produce the reference, then removed again. `AiSourceGuardTests` fails if a threshold or an old class reappears. The documents are also pinned directly by `FieldAiBehaviourTests` (20) and `SkirmishBotBehaviourTests` (13) |
| A12 | Met, survivors listed | about 150 mutants of the engine, 28 of the component and game glue, 25 of the two JSON documents, 9 of the demo; survivors below |
| A13 | Met | `GameRuntime.Briefing.Headless` and `BriefingDemoTests` (6) |

**The combined demo.** `dotnet GameRuntime.Briefing.Headless.dll --answer go|wait <field options>`: a briefing conversation (speaker = the catalog formation's character archetype, text from a locale table with a plural). `go` starts the quest `hold` and releases the squads, whose commanders and quartermaster are AI agents; the quest completes at the tick the last foe stops standing. `wait` holds fire for the whole run and the quest never starts. Two runs print the same hash; the `go` fight prints the same `final` line as the field game alone.

**Survivors and limits (honest).** Unkilled mutants: G02 (a commander told "not ready" at the tick the fight ends; unobservable in the 41 configurations), G07 (a checkpoint taken while a beat is owed; no test saves at that moment), G08 (`combat.IsQuiet` in the supply readiness; I could not build a state where it matters and did not prove it redundant), B06 in the demo (asking the quest to look more often is idempotent, so equivalent in output). Not built, by decision: GOAP, HTN, influence maps (section 3). The tycoon economy bots (`PlayerPolicies`) were not moved. The supply document handles at most 2,000 groups and 16,000 batteries (document capacities). A step starts a think only if the behaviour's static bound still fits, so a very large behaviour needs a very large budget (the field supply bound is in the tens of millions of units; it is a bound, not a cost). Percept attribute values above 10^9 are clamped by the field sensor.

## 8. Offline integer decision trees and the draft-to-data pipeline (pass 53, 2026-10-31)

Package A of `docs/ENGINE_REMAINING_WORK_PLAN.md`, item 8 of `docs/ENGINE_ALGORITHM_REVIEW.md`. Nothing here runs at play time: it is an authoring tool (`AIEngine.Cli import-tree`) and a process. **No change to `AIEngine.Core` was needed** (the decision the plan asked to be recorded when a runtime change looked necessary): a tree is expressed with node kinds the engine already has.

### 8.1 Research (searched 2026-10-31; reached / snippet / unreachable)

- **How a trained tree is laid out. Reached** (scikit-learn, "Understanding the decision tree structure", https://scikit-learn.org/stable/auto_examples/tree/plot_unveil_tree_structure.html, fetched): a tree is parallel arrays `children_left`, `children_right`, `feature` and `threshold`; a sample goes to the left child when `X[feature] <= threshold`, otherwise right. The interchange follows this ("left when the test holds") and also allows the other five comparisons, because other trainers use strict `<` or equality on one-hot columns.
- **Tree to behaviour tree. Snippet only** (search-result text of Colledanchise and Ogren, *Behavior Trees in Robotics and AI*, https://arxiv.org/pdf/1709.00084; the book itself was not read): a decision-tree node becomes a Fallback (selector) over a Sequence of the predicate and the "yes" branch, then the "no" branch, with identical behaviour; a caveat that decision trees pass no information out of actions. This is the encoding used here. A patent abstract (US 11534690) converts a *soft* tree into selectors and conditions that only approximate it; the exact mapping needs hard predicates, which integer thresholds are. I found no source joining the two (a quantised tree into a behaviour tree); that the mapping carries over is my reasoning, and the exactness tests below are its check for this engine.
- **Integer-only tree inference. Snippet only** (search results for InTreeger, https://arxiv.org/pdf/2505.15391, and fixed-point tree ensembles on ARM, https://arxiv.org/pdf/2305.08579): integer-only tree code and fixed-point thresholds are established; float comparisons can be made integer ones. No study of how much accuracy a given quantisation loses was found. So the importer **quantises nothing**: scaling and rounding a trained model is done offline by the author, who re-checks accuracy there; the importer accepts only what is already an integer (`TREE_FLOAT`).
- **Draft, review, merge. Snippet only** (trade-press and vendor summaries of Ubisoft's Ghostwriter, e.g. https://www.gamedeveloper.com/marketing/here-are-more-details-on-ubisoft-s-narrative-ai-tools-from-gdc-2023 and https://www.axios.com/2023/03/27/ubisoft-ai-ghostwriter; the GDC talk was not reached): first drafts of short barks, two options offered to a writer who accepts, edits or discards, with edits fed back; reporting says it assists writers, and developers criticised it. Adopted: a draft is only a proposal that a person reviews line by line. Avoided: treating a draft as good because a tool made it. Unreachable: shipped-use outcomes after 2023.

Adopt / adapt / avoid: **adopt** the exact selector-over-sequence mapping; **adapt** it to the engine (a leaf is an `act` carrying the weight; ranges narrow down the tree so a test the declared range decides is left out); **avoid** a new inference node (no runtime change), float comparison, rounding, soft trees, ensembles (one tree per file), and any model call in the repository.

### 8.2 The interchange (format 1)

One strict JSON object. Every field is required unless marked optional; an unknown field, a repeated id and a wrong type are refused.

```
{ "format": 1, "id": "guard", "every": 5,
  "features": [ { "id": "threat", "fact": "key.threat", "min": 0, "max": 100, "initial": 0 } ],
  "actions": [ "attack", "flee" ],
  "root": "n0",
  "nodes": [
    { "id": "n0", "feature": "threat", "op": "lte", "threshold": 20, "left": "n1", "right": "n2" },
    { "id": "n1", "action": "flee", "weight": 90 } ] }
```

- `id` becomes the behaviour id, `every` its think interval (1 to 100,000).
- A **feature** maps the trainer's column to an AI fact: `fact` must be `key.<id>`, a **host** key the game writes. The importer declares it with the range `min`..`max` and the start value `initial`; the host promises to keep it in range. Percept attributes and the tick are not features (matrix below): a sensor writes the value to a host key first.
- A **split** sends a value left when `feature op threshold` holds; `op` is `lt`, `lte`, `gt`, `gte`, `eq` or `ne` (scikit-learn is `lte`). Thresholds and weights are whole numbers within +-10^12. Optional `missing` and `categories` exist only so those unsupported things are refused by name.
- A **leaf** has an `action` (listed in `actions`) and an integer `weight`. The emitted `act` raises the intent of that name with the argument `weight`, an integer ranging from the smallest to the largest weight of the emitted leaves.
- Node ids use `a-z 0-9 - _` (no dot, at most 100 characters); the importer derives `<id>.left` and `<id>.test` from them.
- File limits: 1,024 nodes, 256 features, 64 actions (`TREE_TOO_BIG`).

### 8.3 What is emitted, and why it is exact

A split `n` becomes `selector n` of two children: `sequence n.left` (the `condition n.test`, then the left subtree), then the right subtree. A leaf becomes `act <leaf id>`. Every leaf succeeds (an `act` without `until` does), so the sequence fails only when its test fails, and the selector then takes the right subtree: exactly "left if the test holds, else right". Going down, each feature's range narrows by the splits above; where the range decides a test, the split is replaced by the branch that can be taken (reported as a reduction). A split costs two levels, the root is level 1 and the engine allows 12, so the longest path is 5 splits (32 leaves, at most 125 nodes of the engine's 512); a test pins this to the engine's own constants. Output is deterministic: LF line endings, sorted keys and intents, fixed layout, so one file gives the same bytes and digest.

The **trace map** (`--trace`) lists every emitted node with its source node and role (`select`, `branch`, `test`, `leaf`), and every source node with no emitted node (`pruned`, collapsed or unreachable, with the reason). Each source node is in `nodes` (as a source) or in `pruned`, never both. `explain` prints the emitted ids, so the trace names the source: leaf ids are the source ids, and `n0.test` maps to `n0`.

### 8.4 Capability matrix

| Feature of a trained tree | Level | Code | What happens |
|---|---|---|---|
| Binary split on an integer host-key feature with `lt lte gt gte eq ne` | supported | | selector, sequence, condition |
| Leaf with an action and an integer weight | supported | | one `act`, weight as the intent argument |
| Test the declared range decides (also after narrowing by splits above) | **reduced** | `TREE_BRANCH_UNREACHABLE` | split replaced by the live branch; dropped nodes listed in the map; nothing a host that keeps its range can observe changes |
| Float threshold or weight (`0.5`, `3.0`, `1e3`) | refused | `TREE_FLOAT` | never rounded; quantise offline and re-check accuracy |
| NaN, Infinity (written as strings) | refused | `TREE_NOT_FINITE` | |
| Category-set split (`in`, `notin`, `categories`) | refused | `TREE_CATEGORICAL` | one-hot encode so each split is one integer comparison |
| Default direction for missing values (`missing`) | refused | `TREE_MISSING_ROUTING` | keys always hold a number; impute before the tree reads it |
| Feature that is a percept attribute, the tick or any non-key fact | refused | `TREE_FEATURE_SOURCE` | a sensor writes a host key; name `key.<id>` |
| More than 5 splits on a path (after reduction) | refused | `TREE_TOO_DEEP` | retrain shallower. A flat encoding (one `all` condition per leaf) would go deeper at more cost per think; not built, no tree needs it |
| File over 1,024 nodes, 256 features or 64 actions | refused | `TREE_TOO_BIG` | |
| Shared subtree, cycle, node not reachable from the root | refused | `TREE_NOT_A_TREE` | an unreachable node would be dropped silently |
| Unknown feature, action, child or root | refused | `TREE_REFERENCE` | |
| Not the interchange (format, unknown or missing field, range, repeated id) | refused | `TREE_FORMAT`, `TREE_UNKNOWN_FIELD`, `TREE_MISSING_FIELD`, `TREE_RANGE`, `TREE_DUPLICATE` | first mistake, whole file |
| Forests, boosted ensembles, regression leaves, soft trees | refused | `TREE_UNKNOWN_FIELD` / `TREE_FLOAT` | one tree per file, integer leaves only |

Refusals found by capability are collected and printed together; structural ones stop at the first. Exit 0 when a document was written, 1 when refused (nothing is written), 2 for wrong arguments or a file problem. After writing, the importer loads its own output with the engine's loader, so a document it hands over is one the game accepts.

### 8.5 The pipeline for drafts (never at run time)

1. **Draft.** A person or a tool (a language model, outside this repository) proposes a strict JSON file: a tree in the interchange, an AI document, a dialogue document or a locale table. This repository contains no model call, no training code and no ML library.
2. **Check.** The owning engine's tool: `AIEngine.Cli import-tree` or `check`, `DialogueEngine.Cli check`, and so on. Every refusal has a code and a place. A draft that does not pass goes back; it is not hand-repaired into something that merely loads.
3. **Coverage.** For text, `DialogueEngine.Cli coverage <document> <table...>` lists the keys a language lacks.
4. **Human review.** A named reviewer reads every line and number with the engine's output beside it: for AI, `explain` on scenarios at and either side of each threshold, plus the reduction notes; for the other engines, `simulate`. Generated text gets the content-warning (`cw.*` tags and alternatives) and licensing review the Dialogue engine requires; a draft carries no licence of its own.
5. **Merge as ordinary data.** The reviewed output is committed as a normal document. The runtime cannot tell where it came from.
6. **Provenance note.** Tool, date and reviewer go in the document's evidence (a result section or the sample's entry), never in the runtime or a save.
7. **Numbers are human-set.** Rates, prices, weights and thresholds a draft proposes are inputs to the balance harness (`simulate --seeds`), which a person runs and reads; a model's number is not accepted because it loaded.

### 8.6 Worked example (a hand-written draft; no model produced it)

`src/AIEngine/samples/guard.draft.tree.json` was written by hand to hold four mistakes a trained or drafted tree plausibly has; `guard.tree.json` is the fixed file. Commands run from `src/AIEngine/samples`.

```
$ AIEngine.Cli import-tree guard.draft.tree.json --id guard.ai --out guard.ai.json
refused TREE_FEATURE_SOURCE: feature distance: its fact 'item.distance' is not a host key; ...
refused TREE_FLOAT: node n0: threshold 0.2 is not a whole number; ...
refused TREE_MISSING_ROUTING: node n2: a default direction for a missing value cannot be kept: ...
refused TREE_REFERENCE: node n5: its action 'retreat' is not in the 'actions' list
not written: 4 refused, 0 reduced                                   (exit 1)
```

The fixes are decisions a person makes: a sensor writes `distance` to a host key, so the fact is `key.distance`; the 0.2 threshold is the model quantised offline to 20 on the 0..100 `threat` scale; the missing route is dropped because the game supplies a value; `retreat` is the action `flee`.

```
$ AIEngine.Cli import-tree guard.tree.json --id guard.ai --out guard.ai.json --trace guard.trace.json
ok guard.ai digest 5109F3DB...9C06 splits 4 leaves 5 depth 3 nodes 17 budget a step needs at least 17
reduced TREE_BRANCH_UNREACHABLE: node n8: the test 'distance lte 520' always passes while 'distance' is within 121 to 500; ...
supported leaf 5
supported split-lte 4
$ AIEngine.Cli explain guard.ai.json guard.scenario.json
n0 [selector] success
  n0.left [sequence] failure
    n0.test [condition] failure - does not hold
  n2 [selector] success
    ...
        n7 [act] success - raises attack(weight=80)
intent attack(weight=80) from n7
```

The reduction is for the reviewer to judge: the trained test `distance <= 520` can never fail on a 0..500 key, so leaf `n10` is dead; either that is accepted or the range is wrong. The stored `guard.ai.json` and `guard.trace.json` are pinned by a test. Provenance of this sample: hand-written by the pass 53 agent, 2026-10-31, no generating tool; **not yet reviewed by a human** (merged as a sample, not as game content).

### 8.7 Result

**Built (2026-10-31).** `AIEngine.Cli import-tree <tree.json> --id <document-id> --out <ai-document.json> [--trace <map.json>]` (one owner, `TreeImport`, in partial files `Check`, `Plan`, `Emit`; the strict reader `TreeReader`; the report `TreeReport`; the codes `TreeCodes`; the comparisons `TreeOps`); the sample set in `src/AIEngine/samples/` (`guard.draft.tree.json`, `guard.tree.json`, `guard.scenario.json`, and the generated `guard.ai.json`, `guard.trace.json`). `AIEngine.Core` is unchanged, so no AI demo hash can move.

| Check | Result |
|---|---|
| Exactness | An independent reference evaluator (System.Text.Json only) and the engine running the exported document agree on leaf, action and weight at: the guard sample's dense grid (21 x 21 x 21 = 9,261 points), every boundary combination of its thresholds (value below, on and above each, plus both range ends), and 10,000 seeded points; and on 60 seeded random trees (every comparison, thresholds up to 3 outside the range, 4,000 boundary points and 500 random points each) and balanced trees of depth 1, 3 and 5. The engine's trace lists the same source tests as the tree passes (the reference path minus collapsed splits) at every boundary point and every tenth random one. |
| Refusal matrix | Every code has a test: 21 file-level cases, 21 capability cases, the size limits, the depth limit both sides, all together in one draft, and through the command (exit 1 and no file written, exit 2 for ten wrong command lines and a missing file). |
| Reduction | 12 range cases (every comparison, both verdicts), narrowing down the tree, and the map accounting for every source node exactly once on 40 random trees. |
| Counts | `AIEngine.Tests` 209 to 303 (94 new, all passing). Focused set `-Changed src/AIEngine`: GameContent 412, Calendar 86, Diplomacy 160, Economy 131, Relationship 123, GameRuntime 559, Tutorial 127, WorldEvents 111, all passing. |
| Mutation | 26 mutants of the new logic on a copy outside the tree (SHA-256 of each file checked after every restore): 25 killed by a failing test, 1 (M19, the repeated-node check) detected by a crash, because the cycle case then recurses without end; the shared-subtree case alone is still caught by the orphan check. Two tests were added or tightened because of the first round of reasoning about the mutants: initial below the range minimum (M23) and exact range ends for the not-equal (M06). |

**Limits and not done.** Features are host keys only (percept attributes are refused; a sensor writes the key). Depth is 5 splits; a flat encoding for deeper trees is a design option, not built, because nothing needs it. One tree per file: no ensembles, no regression leaves. Thresholds are never quantised here; accuracy after offline quantisation is the author's to check, and I found no source quantifying it. The sample has not been reviewed by a human. No Linux run.
