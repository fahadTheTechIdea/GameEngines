# SpatialEngine: research, boundary, criteria and result

Pass 28, 2026-10-28. Written before the code; the result section at the end records what was built and measured.

## 1. What already owns space (evidence, and the decision)

Read in the source on 2026-10-28:

| Owner | What it does with space | Where |
|---|---|---|
| `CombatEngine` | Takes a **distance** (millimetres) and a **terrain id** with each order; terrain gives an accuracy percent and a movement percent; a march closes the distance by speed times the movement percent; weapon range is compared with the distance. It has no map, no positions and no line of sight | `TerrainRule.cs`, `MovementRule.cs`, `CombatResolver` |
| Field game | One constant distance for the whole fight (`--distance`); sides are not placed anywhere; every group is told about every other | `FieldCommander`, `FieldOptions` |
| Skirmish bots | The commander holds one distance for the duel; `settled` says whether a march has fixed it | `SkirmishBot` |
| `AIEngine` sensors | `SkirmishBot.Sense`, `FieldCommander.Describe`, `FieldQuartermaster.Sense` tell the agent about every group, whatever it could see: **truth, not knowledge** | the three sensors |
| `SimEngine` | Tile worlds with `NavGrid`/`PathSearch` (octile A* over a cost **callback** that reads the live world: occupancy, traffic yielding, motion clearance, entity observation scopes), `TileLineGeometry` | `Standard/Navigation.cs`, `Standard/Movement.cs` (the Mover), `Map/*` |

Decision: **one SpatialEngine owns geometry and knowledge of geometry** (grid, cost and sight layers, paths, flow fields, line of sight, field of view, spatial queries, fog of war). It owns *where things are and what can be known about it*; it does not own what a terrain does to a shot or a march (those numbers stay in `CombatEngine` rules, keyed by the terrain ids the map carries), and it does not own the SimEngine world.

Migration plan, with the reason each part is or is not moved:
- **Field game positioning and distance: move.** Groups get positions on a map; the distance the commanders hold is *derived* from the positions of the lines (today it is a free number). The default layout reproduces the old number, so the 41 recorded output hashes must not change.
- **AI sensors for the field game commanders: move** to fog-of-war knowledge: a group is told about a foe when its side can see it. On the default map every group sees every other, so decisions and hashes stay; with a smaller sight range the commanders genuinely do not know about foes they cannot see.
- **CombatEngine terrain rules: keep, and link.** The numbers are combat rules and a spatial map must not duplicate them; the map names terrain ids and the pair is checked at load (every map terrain exists in the rules). I will not copy the percentages into the map.
- **SimEngine Mover path search: evaluate, then decide with measurements** (section 7).

## Boundary with Godot (standing rule: use Godot's features, do not reinvent them)

The rule asks for a **demonstrated gap** before any engine service is replaced. The gap here is narrow and specific: Godot's navigation and pathing services are built for what a *view* needs; the simulation needs a different guarantee. Evidence, from the Godot documentation installed on this machine (`H:\dev\Godot\godot-docs-html-master`, Godot 4.7.2; read 2026-10-28):

| What the simulation needs | What the Godot class documents | Gap |
|---|---|---|
| Identical results on every machine and in every build (hash, checkpoint, replay, lockstep) | `AStarGrid2D`: costs are `float` (`_compute_cost`, `_estimate_cost`, `set_point_weight_scale(id, weight_scale: float)`), the path is a `PackedVector2Array`; `NavigationServer2D.map_get_path(map, origin: Vector2, destination: Vector2, optimize: bool, ...)` returns `PackedVector2Array`. Neither class page states anything about determinism (the only match for "determin" in either page is the word "determining") | Float costs and positions: the cross-machine and cross-build identity the hashes rely on is not promised, and cannot be asserted in a headless test |
| An answer at the moment of the question, inside the tick | "Most NavigationServer2D changes take effect after the next physics frame and not immediately"; baking runs "as an async task running on a background thread"; the avoidance result `safe_velocity` "will be dispatched with a signal ... just before the physics calculations"; a map that has never synchronised is empty (the tutorial tells you not to query it) | Results arrive on the engine's frame and thread schedule, not on the simulation's tick |
| To run with no engine at all (headless tests, a server, the other games' tests) | Both are engine nodes/servers | The simulation libraries must not depend on Godot |
| Knowledge, not truth | No fog of war or per-side knowledge exists in either class | Nothing to reuse |

So the SpatialEngine owns **simulation truth only**: any path or distance that changes simulation state; line of sight and fog of war as *knowledge* an AI agent is given; anything that enters a hash, checkpoint, journal replay or lockstep step; anything a headless test must reproduce.

**Everything view-only stays native Godot**, and the host must not reimplement it: cosmetic movement and smoothing (`Tween`, interpolation), local avoidance (`NavigationAgent2D` / `NavigationServer` avoidance for display-only agents), picking and cameras, `InputMap` and `Input`, `Timer` nodes for view timing, signals for events leaving the layer, `MultiMesh` for mass rendering, `AStarGrid2D` or the navmesh for visual-only agents that never affect the simulation. A view agent may *follow* a simulation snapshot; it never *decides* for it.

Review of the existing Godot host (`src/GameRuntime/GameRuntime.Godot`, read 2026-10-28), against the rule:

| Finding | Verdict | Action |
|---|---|---|
| `GodotEventPort`: a second event bus (queue, handler table, delivery budget, drain) beside the reference `RuntimeEventBus` | A parallel replacement of a game-layer owner (rule 3), though not of a Godot service | Replaced: the Godot node hosts a `RuntimeSession`, which owns the bus |
| `GameFramework` + `ComponentNode`: its own tick counter and its own per-node update scheduler beside `RuntimeSession`'s clock and ordered Update | A second clock and scheduler | Replaced: one node feeds elapsed microseconds to `RuntimeSession.Update`; the session's clock is the one tick |
| `Main._UnhandledInput` maps an `InputMap` action straight to `Port.Emit(MissionCommandRequested)` | `InputMap` use is native and correct; emitting a game event from the view bypasses the polled input model | Moved to `InputState`: the node writes the action edge, the game component polls it |
| `Engine.PhysicsTicksPerSecond`, `Input.ParseInputEvent`, `InputMap.AddAction`, `GD.Print`/`PushError`, signals | Native Godot services used as intended | Kept |

A source guard test (`GodotHostSourceGuardTests`) fails if a file of the host adapter or the Godot project contains pathfinding, its own timers or its own action map where Godot provides them (the patterns are listed in the test).
## 2. Research (searched 2026-10-28)

Primary sources reached are marked P; where only a search page or abstract came back that is said.

- **A\* on grids and hierarchical search.** P (abstract and publication record): Botea, Müller and Schaeffer, "Near Optimal Hierarchical Path-Finding", *Journal of Game Development* 1(1), 2004. HPA\* splits the map into clusters, precomputes the optimal distances of crossing each cluster, plans on the abstract graph, and can nest; the abstract reports up to ten times faster than a highly optimised A\* with paths within 1 percent of optimal. I did not read the full paper.
- **Jump point search.** P (abstract): Harabor and Grastien, "Online Graph Pruning for Pathfinding on Grid Maps", AAAI 2011: online, optimal, no extra memory, expands only jump points and skips the nodes between; reported speed-ups of an order of magnitude on **uniform-cost** grids. A weighted-grid extension exists in later work (named in the search result, not read).
- **Flow fields for many agents.** Secondary only: Emerson, "Crowd Pathfinding and Steering Using Flow Field Tiles", *Game AI Pro* (the search returned the publisher abstract: flow field tiles move large numbers of agents across big maps and avoid rebuilding a path per agent). I did not read the chapter, so the cost/integration/flow field split I use is my own reconstruction of the standard technique (a Dijkstra integration from the goal, a direction per cell from the lowest neighbour), not a quotation.
- **Navmesh versus grid.** P (documentation search result): Recast builds a navmesh by voxelising input geometry, filtering and re-triangulating; Detour answers path queries and agent movement; the docs say faster, slightly less accurate queries are often better than optimal paths. Nothing returned on Detour determinism across platforms.
- **Godot.** P (the installed documentation, `godot-docs-html-master`): `AStarGrid2D` is a variant of `AStar2D` for partial 2D grids, set by `region`, `cell_size` and `update()`, with `DiagonalMode`, `jumping_enabled`, default heuristics and a virtual `_compute_cost`; `NavigationServer` is the engine's service for navigation maps. These are **view-side** services (they use engine floats); the simulation must not depend on them for a decision.
- **Line of sight and field of view.** Secondary: Bergström's "FOV using recursive shadowcasting" (RogueBasin; the search found pointers and ports, not the article): the field is divided into eight octants and swept with start and end slopes. The standard implementations use floating slopes; I write the slopes as integer fractions.
- **Determinism.** P (search result summarising Fiedler, Gaffer on Games): floating-point results differ between compilers, operating systems and instruction sets, and probably between debug and release builds; lockstep needs identical results; fixed point (integers with a fixed scale, no `sin`/`cos`/`sqrt`) is the usual answer. The Age of Empires paper already cited in `AI_ENGINE.md` says the same about identical simulations.
- **Fog of war, spatial hash, quadtree.** The one search for fog-of-war models returned nothing on them. I use a three-state model (unknown, remembered, visible) with last-known sightings from general knowledge of RTS and roguelike games, not from a source, and say so. A uniform spatial hash was chosen over a quadtree because the game's objects are of similar size and the hash has O(1) update; I did not read a source comparing them.

What I could not establish from sources: HPA\* or JPS cost on *this* code (measured instead), flow field details, fog-of-war conventions, Detour determinism.

## 3. Adopt, adapt, avoid

| Source idea | Decision |
|---|---|
| Octile A\* on a grid with an admissible heuristic | **Adopt.** Integer costs (straight 10, diagonal 14 times the cell cost), no corner cutting, ties by f, then h, then cell index, so the path is a pure function of the map |
| Navmesh (Recast/Detour) | **Avoid** in the simulation: build is float geometry; the game's maps are already tile grids. A view layer may use the engine's navmesh for display-only agents |
| Hierarchical pathfinding (HPA\*) | **Not built.** Costs and complexity are not justified until a measurement shows A\* or the shared flow field too slow on the maps in use; the measured numbers are in section 7 |
| Jump point search | **Avoid** here: it is for uniform-cost grids, and the maps carry cost layers |
| Flow fields for squads that share a goal | **Adopt** as the shared-by-destination query: one integration per goal, cached, every agent reads it. Rebuilt when a layer changes (version stamp) |
| Floating-point positions, `Math.Sqrt` | **Avoid.** Millimetre integers; distance by exact integer square root (floor) |
| Bresenham line for line of sight | **Adopt**, symmetric version (both directions agree), blocked by a sight-blocking cell strictly between the end cells |
| Recursive shadowcasting for field of view | **Adopt** with integer fractions for slopes; radius is Euclidean in cells |
| Fog of war as truth with a mask | **Avoid.** Knowledge is separate state per side: each cell unknown, remembered or visible, and each unit sighting kept with the tick it was last seen; AIEngine is told only sightings |
| Uniform spatial hash | **Adopt**: range and nearest queries with deterministic order (distance, then id) |
| Quadtree | **Not built**, same measurement rule |
| Godot `AStarGrid2D` / `NavigationServer` | **Use for view-only concerns only.** No simulation decision reads them |
| Re-running a search for every agent | **Avoid**: path queries are cached by (goal, layer version) |

## 4. Layer boundary

- `SpatialEngine.Core` (pure library): map format and loader, layers, fixed-point metric, path and flow field services, line of sight, field of view, spatial hash, fog of war, checkpoint. No clock, no files, no floats, no Godot, no world.
- `GameRuntime.Spatial` (game layer component): requests as events (place, move, remove, path, flow, sight), answers in `Update` in arrival order, publishes sightings as events (which the AI sensors forward to agents), checkpoint refused while a request waits.
- A host (headless or Godot) only renders from snapshots.

## 5. Map format 1 (summary)

`format`, `id`, `cellMilli` (millimetres per cell), `terrains` (id, movement `cost` 1 to 100 or 0 for impassable, `blocksSight`), `legend` (one character per terrain), `rows` (height strings of width characters). Strict: no unknown field, every character in the legend, equal row lengths, bounded size (at most 1,048,576 cells), terrain ids of the lowercase alphabet. The digest is of the exact text.

## 6. Acceptance criteria (fixed before coding)

| # | Criterion | Check |
|---|---|---|
| S1 | The map format is strict: unknown, missing, repeated, bad id, ragged rows, unknown legend character, size and cost range, each with its own code | One test per code |
| S2 | Fixed point only: no `float`, `double`, `Math.` call in the library; distance is exact floor of the Euclidean distance in millimetres | Source guard; exact-value tests including large coordinates |
| S3 | Paths: optimal for the cost model (checked against an independent brute-force Dijkstra on random maps), deterministic tie-breaks, no corner cutting, budget respected, unreachable and out-of-bounds reported with codes, same result however often asked | Tests including a 300-map property test |
| S4 | Shared by destination: a second query to the same goal costs no search (counted), a layer change invalidates, cache capacity is bounded | Tests with counters |
| S5 | Flow field: following it from any reachable cell reaches the goal with total cost equal to the A\* cost; 1,000 agents sharing a goal cost one integration | Tests, measurement |
| S6 | Line of sight is symmetric and blocked exactly by sight-blocking cells between; field of view agrees with line of sight on open ground and is never larger than the radius | Tests, property test |
| S7 | Spatial hash: range and nearest return exactly what a brute-force scan returns, in distance-then-id order, after any sequence of moves and removals | Property test |
| S8 | Fog of war is knowledge: a unit out of sight is not reported; a unit that was seen is reported as last seen at its old cell and tick, not at its new one; remembered cells stay remembered | Tests |
| S9 | Checkpoint: format, map digest and state digest; any change refused with its own code; a forged digest cannot bring in a state the map could not have made; continuation identical | Tamper tests |
| S10 | Component: requests answered in `Update` in arrival order, events deferred, refusals as events, checkpoint refused while a request waits, journal replays | Tests |
| S11 | Migration keeps behaviour: the 41 recorded output hashes are unchanged after the field game takes its distance and its commanders' knowledge from the SpatialEngine; with a short sight range the commanders do not fire at foes they cannot see | Equivalence tests, new test |
| S12 | Mover decision: path equality with `PathSearch` is measured on random maps and the pass-24 budgets are re-measured before any replacement | Measurements recorded; replacement only if all hold |
| S13 | Cost: A\* on a 256 by 256 map, a flow field integration and a 1,000-agent field walk are measured with repeats | Benchmark |
| S14 | Every new test is mutation-checked | Recorded in the result |
| S15 | The Godot host contains no simulation pathfinding, no timer or input-map reimplementation and no second event bus; view concerns use Godot services | Source guard test over the host adapter and the Godot project; the review table above |

## 7. Result

**Parked (2026-10-28):** pass 28 was stopped by the coordinator after the research and this document. Only a sketch of the core exists (`src/SpatialEngine`, untested, not in the solution). See the twenty-eighth-pass entry of the plan.
