# Character factory: research, measurement, decision, acceptance criteria

Dated 2026-10-27. Written before any factory code. Sections 1 to 5 are research and the plan; sections 6 and 7 hold the measurements and the decision and are filled in after the benchmark runs. Nothing here is a claim about code that does not exist yet.

The request (coordinator, approved by the user): a **CharacterFactory** in SimEngine as the single owner of creating all characters (NPCs, workers, soldiers) from archetype definitions. It is not a resource factory (resources are `ResourceId` plus `Fix` amounts and lots, and the content registry already owns their definitions), and there is no pooling inside the deterministic simulation core by default. It must work for a game with **1,000 soldiers on the field**, and plan for 5,000 to 10,000.

## 1. Research

Facts are what a source says. Inferences are mine and are marked as such. Where a source was secondary (forum, community wiki) it is labelled so; I found no official developer documentation for the game-design claims in 1.4.

### 1.1 .NET allocation and garbage collection (Microsoft Learn, fetched 2026-10-27)

| Fact | Source |
|---|---|
| The GC has generations 0, 1 and 2. New objects start in generation 0, most die there, survivors are promoted. A generation 2 collection is a full collection. | [Fundamentals of garbage collection](https://learn.microsoft.com/en-us/dotnet/standard/garbage-collection/fundamentals) |
| Allocating on the managed heap is "almost as fast as allocating memory from the stack" (a pointer bump). | same |
| "The intrusiveness (frequency and duration) of garbage collections is the result of the volume of allocations and the amount of survived memory on the managed heap." "The fewer objects allocated on the heap, the less work the garbage collector has to do." | same |
| Objects of 85,000 bytes or more go on the large object heap (LOH), which is collected only with generation 2 and is not compacted by default. | [Large object heap](https://learn.microsoft.com/en-us/dotnet/standard/garbage-collection/large-object-heap) |
| The cost of a large allocation is dominated by clearing memory (the page gives 16 MB at about 16 ms on a 2 GHz machine). Temporary large objects cause repeated generation 2 collections. The page recommends "a pool of large objects that you reuse instead of allocating temporary ones". | same |
| An array of references makes the GC walk every element; an array of structs without references is not walked. The page recommends storing indexes instead of references. | same |
| Workstation GC is the default for standalone apps; concurrent (background) GC lets threads continue during a collection. Server GC uses a heap and a dedicated thread per logical CPU and is for throughput. | [Workstation vs. server GC](https://learn.microsoft.com/en-us/dotnet/standard/garbage-collection/workstation-server-gc) |
| `GC.TryStartNoGCRegion(totalSize)` disallows collection while a critical path runs, if the memory is available; the size must be large enough for every allocation in the path, including the runtime's; it cannot nest; exit with `EndNoGCRegion`. | [GC.TryStartNoGCRegion](https://learn.microsoft.com/en-us/dotnet/api/system.gc.trystartnogcregion) |
| `ArrayPool<T>` rent/return "can improve performance in situations where arrays are created and destroyed frequently, resulting in significant memory pressure on the garbage collector". | [ArrayPool&lt;T&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.buffers.arraypool-1) |
| `ObjectPool<T>` is for objects that are expensive to allocate or initialize, limited, or used predictably and frequently. "Unless the initialization cost of an object is high, it's usually slower to get the object from the pool." "Use object pooling only after collecting performance data using realistic scenarios." | [Object reuse with ObjectPool](https://learn.microsoft.com/en-us/aspnet/core/performance/objectpool) |

Inferences: (a) Per-character objects are small (far below 85,000 bytes) and long-lived, so they do not go to the LOH and they are promoted to generation 2 once; the GC cost of 10,000 live characters is the cost of *tracing* them in generation 1 and 2 collections, which depends on how many references each carries, not on the count alone. (b) Per-tick allocation, not live-object count, is what causes frequent generation 0 and 1 collections; a tick loop that allocates per entity per tick is the hazard. (c) Pooling character objects would trade allocation for stale-state risk in a deterministic core; the Microsoft guidance itself says to measure first. (d) `TryStartNoGCRegion` is a latency tool for a bounded critical region, not a general answer; I will use it only in the benchmark to separate allocation cost from collection cost, if at all.

### 1.2 Entity storage in data-oriented engines

| Fact | Source |
|---|---|
| Unity Entities groups entities by *archetype* (the unique combination of component types). Each chunk is 16 KiB with one packed array per component type plus an array of entity ids; removing an entity moves the last entity into the gap. An archetype is an identifier, not a container. | [Unity Entities, archetypes](https://docs.unity3d.com/Packages/com.unity.entities@1.3/manual/concepts-archetypes.html) |
| Flecs stores entities by archetype in structure-of-arrays tables; adding or removing a component moves the entity between tables. (Secondary sources only; I did not reach the official manual page.) | [Flecs mirror](https://www.sourceforge.net/mirror/flecs), [GitHub issue](https://github.com/sandermertens/flecs/issues/3) |
| EnTT is "a sparse set based model"; since 3.9.0 the sparse arrays hold the *versions* of identifiers so a stale identifier is detected. | [EnTT crash course](https://github.com/skypjack/entt/wiki/Crash-Course:-entity-component-system), [3.9.0 notes](https://newreleases.io/project/github/skypjack/entt/release/v3.9.0) |
| Reused identifiers carry a version (generation) so that a handle to a destroyed entity does not silently address its successor. (General ECS practice; Flecs-specific detail is a secondary claim.) | [Geary note on Flecs](https://docs.mineinabyss.com/geary/backend/core/changing-data) |

Inferences: SimEngine already has the archetype idea in a different form: an `EntityDef` lists its components, every entity of a def has the same component shape, and `EntityStore` keeps typed `ComponentList<T>` indexes in entity-id order. What it does not have is packed per-component arrays: each `Entity` owns an array of `Component` objects, and each `Component` is a separate heap object. That is an array-of-objects layout, so the number of heap objects per character is (1 entity + 1 component array + k components + their own fields). I record the exact count in the benchmark.

### 1.3 Identity: monotonic ids versus generational ids

SimEngine's `EntityId` is a monotonic `int`; `EntityStore` never reuses one (`_nextId` only grows, `CreationIdentityExhausted` at `int.MaxValue`), `CheckCreation` refuses a stale creation whose identity is not the next one, and saves record `_nextId`. **Decision: this stays.** Reasons: (1) determinism, replay and every existing reference (plans, work orders, reservations, journals) rely on an id never meaning two things; (2) 2^31 identities is far beyond the target: creating 10,000 characters per game-minute for a week still uses about 100 million; (3) the generational scheme solves a problem monotonic ids do not have, at the price of a second identity concept. The factory must not reuse ids, and a test will pin this.

### 1.4 Mass units in games (design precedent)

Community and secondary sources only; no developer documentation found. Treat as design inspiration, not as measured fact.

| Observation | Source |
|---|---|
| In Rome and Medieval Total War the number of soldiers drawn per unit is a *display* setting ("Unit Size"); modders report a practical cap near 250 soldiers per unit and siege pathfinding problems when units are large. | [Feral support page](https://support.feralinteractive.com/en/mac-linux-games/rometw/faqs/android_unit_size), [TWCenter thread](https://www.twcenter.net/threads/unit-sizes.352425/) |
| Armies in Total War are capped around 20 units; a division in Hearts of Iron IV is an aggregate whose hit points are the sum of its manpower and equipment, and the front has a combat width that limits how many divisions fight. | [TWCenter](https://www.twcenter.net/threads/the-20-units-cap.366308/), [HOI4 combat stats guide](https://gameslantern.com/article/all-combat-stats-explained-in-hearts-of-iron-iv) |
| RimWorld has no hard pawn limit; players report slowdowns near 100 pawns and below 30 fps at 500; version 1.5 moved pawn drawing to a separate thread. Dwarf Fortress players report sub-1 fps at 200 dwarves on a heavily excavated map. | Steam community threads (anecdotes), e.g. [RimWorld discussion](https://steamcommunity.com/app/294100/discussions/0/591762405949768995) |
| A research paper on large agent simulations states the general trade-off: level of detail per agent against the number of agents; it proposes treating level of detail as a parameter adapted during the simulation. | [Large-scale agent-based urban simulation (AAMAS 2011)](https://cs.huji.ac.il/%7Ejeff/aamas11/papers/C5_B67.pdf) |

Inferences: three representations recur: (1) every individual is an entity (RimWorld, Dwarf Fortress; the cost is paid per individual and the games keep populations small or accept low frame rates); (2) the unit is the entity and soldiers are presentation (Total War display setting, HOI4 division); (3) a hybrid where an aggregate carries headcount and individuals are materialized when something needs them. The user's requirement, 1,000 soldiers on the field with a plan for 10,000, sits between (1) and (2): it is in the range where (1) may be fine and (3) is the safe fallback. Which one is right depends on the numbers in section 6, not on precedent.

### 1.5 Rendering many sprites in Godot (godotengine.org documentation, fetched 2026-10-27)

| Fact | Source |
|---|---|
| A MultiMesh is "a single draw primitive that can draw up to millions of objects in one go"; the page recommends servers for instances in the thousands that need ongoing processing and MultiMesh for hundreds of thousands to millions. There is no per-instance culling: the whole MultiMesh is drawn or skipped. Allocate the maximum instance count and adjust `visible_instance_count`. | [Optimization using MultiMeshes](https://docs.godotengine.org/en/stable/tutorials/performance/using_multimesh.html) |
| `MultiMeshInstance2D` renders a MultiMesh in 2D with a texture, and can render faster than many `Sprite2D` nodes with large transparent areas. | [MultiMeshInstance2D](https://docs.godotengine.org/en/latest/classes/class_multimeshinstance2d.html) |
| The servers (`RenderingServer`) are the low-level API the scene system is built on; using them avoids node overhead, but "dealing with tens of thousands of instances for something that needs to be processed every frame can be a bottleneck"; do not read values back from the servers every frame. | [Optimization using servers](https://docs.godotengine.org/en/stable/tutorials/performance/using_servers.html) |

Inferences: the *view* layer is where pooling and batching belong. A Godot host should draw soldiers from a snapshot of the simulation (positions, facing, animation frame) through a MultiMesh or server canvas items and never create a node per soldier at 5,000 to 10,000; node pooling is a fallback for the few individually inspectable characters. This repository has no Godot character renderer yet (the Alchemy and Combat components are not wired into the Godot addon), so I will specify the view contract and not claim a Godot run.

### 1.6 Determinism for save, replay and hash

| Fact | Source |
|---|---|
| Lockstep and replay need bit-identical state; a per-frame checksum of the whole state is the check. Floating-point determinism across machines is conditional ("yes, if..."). | [Deterministic lockstep](https://gafferongames.com/post/deterministic_lockstep/), [Floating point determinism](https://gafferongames.com/post/floating_point_determinism) |

Inferences: SimEngine already uses `Fix` (fixed-point) and a seeded `World.Random`, a state hasher, save and replay. The factory must draw all names, traits and stats from a caller-supplied seed through the engine's deterministic random (or a hash of seed and index as the CombatEngine does), never from the clock, a culture-dependent formatter or dictionary iteration order, and everything it makes must be inside the saved state.

## 2. What exists in SimEngine today (read from source, 2026-10-27)

- A character-like thing is an `EntityDef` with components such as `tile_presence`, `mover`, `worker`, `storage`, `work_policy` and an entity `kind` of structure, vehicle or marker (`EntityKind`, `Content/Defs.cs`). There is no person kind and no creation concept for people.
- Creation path: `World.PrepareCreation` (admission, returns a refusal `Code` or an `IEntityCreation`) then `World.CommitCreation` (calls `Spawn`, which calls `EntityStore.Create`). Startup creation (`WorldSetupModule`, `entity_setups` in JSON) uses the same path. The creation intent is "ephemeral current-state intent, never a live entity or a saved creation queue" and goes stale if the tick or next identity changes: **one creation is admitted at a time**, so a bulk "all or nothing" needs its own pre-check over the whole batch.
- Commands: a module registers command handlers (`CommandHandler<T>` with `Check` and `Apply`); `DirectorBridgeModule` carries a game-layer request into the world as a ticketed command on the next tick, which is the deferred-Update rule the game layer needs.
- Storage: `EntityStore` keeps a list, a dictionary by id, a pending list flushed between phases, and typed component indexes. `Flush` after removals does `RemoveAll` over the list and each index (cost proportional to the live count).
- Per-tick entity cost: `Entity.Update` runs per entity per game update and, for each state source, captures an observation and verifies it (`EntityObservation.CaptureProvider`), which I expect allocates; this is one of the things to measure.
- Persistence: `EntityStore.Sync` writes each entity's id, def id and component state; the state hasher covers it.

## 3. Adopt, adapt, avoid

| Idea | Decision |
|---|---|
| Archetype = fixed component shape per definition (Unity, Flecs) | **Adopt** as it already exists: a character archetype *is* an `EntityDef` plus a character definition; the factory does not invent a second shape system. |
| Packed per-component arrays (SoA chunks) | **Avoid for now.** It would be a rewrite of the kernel's storage; only worth it if section 6 shows the object layout is the bottleneck. |
| Generational ids | **Avoid.** Monotonic ids stay (1.3). |
| Pools for characters inside the sim core | **Avoid** by default (user instruction, and Microsoft's own guidance to measure first). Allowed only for scratch buffers and view-layer nodes, and only with before and after numbers. |
| Aggregate unit with headcount (HOI4 division, Total War unit) | **Adapt** as the fallback: a *squad entity* that carries a roster (counts per archetype), with individuals materialized lazily. Built only if the numbers require it. |
| Display-only soldier count (Total War) | **Adapt** for the view layer: a host may draw fewer figures than the roster states, never more, and the headcount shown is the simulation's. |
| MultiMesh / RenderingServer drawing from a snapshot (Godot) | **Adopt** as the view contract; implementation is the host's and is not claimed. |
| Seeded generation of names and traits | **Adopt**: each character's seed is a hash of (caller seed, factory command id, index), as `CombatRoll` does; no clock, no culture formatting, no iteration-order dependence. |

## 4. Layer boundary (decisions made before code)

1. **Where the code lives:** a new standard module in SimEngine (`Beep.SimEngine/Standard`, id `sim.characters`) that depends only on public kernel contracts. Archetype definitions are JSON content registered through the existing content registry, with strict schema, refusal codes and no defaults for decisions. The factory does not fork the creation path: it calls `PrepareCreation` and `CommitCreation`.
2. **Character data is components, not a parallel table:** stats, skills, traits and name are state of a `character` component on the entity, saved and hashed by its `Sync`. Equipment links name catalog items by id as data; the factory neither knows nor copies item stats (the CombatEngine rule: stats have one owner, the catalog).
3. **One owner of roster facts:** a formation's roster (strength, equipment, training, role) is the catalog's `units` standard. The factory reads a roster through a small adapter owned by `GameContent.Catalog.*` (as `CatalogCombat` does for combat), and CombatEngine groups are built from the same roster. The factory does not define its own roster format.
4. **Bulk creation:** one `SpawnFormation` command carries a formation or archetype counts and a seed; admission checks the *whole batch* (definition exists, owner seated, placement for every character valid and mutually non-conflicting, identities available, a limit from the content) and creates all or none; a refusal leaves the state hash unchanged.
5. **Deferred rule:** the game layer asks through the Director bridge; the world applies it on its next tick; the answer is an event, never a synchronous result.
6. **Pooling:** none in the core. Anything else waits for section 6.

## 5. Acceptance criteria (fixed before coding)

| # | Criterion | Evidence required |
|---|---|---|
| F1 | Every character in a game is created by the factory from an archetype definition; no other code creates a character entity | Source guard test: character defs may only be spawned through the factory's command; a second path fails the test |
| F2 | Archetype definitions are strict JSON: unknown or missing fields, duplicate ids, unknown component, skill, trait or item references and out-of-range numbers are refused with a code each | One test per code; the code guard from the combat engine extended to the factory |
| F3 | Names, traits and stats are a pure function of (caller seed, command id, index, archetype); same inputs, same characters; different seed, different characters | Determinism tests, including under another culture; pinned values |
| F4 | Bulk creation of N is all-or-nothing: any refusal leaves the world hash unchanged and creates no entity | Tests with a refusal at the first, a middle and the last character; hash before equals hash after |
| F5 | Uses `PrepareCreation` and `CommitCreation`; no second creation path; monotonic ids, never reused, including after removals and after load | Source guard and a test over create, remove, create, save, load, create |
| F6 | Requests from the game layer are applied on the next tick, by the world, and answered with events | Test with an observer component that runs before the world; mutation: apply at request time fails it |
| F7 | Save, load and replay of a world with spawned characters give equal hashes at every tick | Replay test over spawn, movement and removal |
| F8 | Roster facts have one owner: a formation spawned by the factory and the combat group built from the same formation agree on headcount and equipment, from the catalog only | Test comparing both from the same pack; source guard that the factory holds no roster numbers |
| F9 | Scale: with 1,000 characters the tick, creation, save, load and hash times and the allocation per tick stay within the budgets set in section 7 from the measurement | Benchmark numbers recorded here; a bounded test asserts the budget |
| F10 | If individuals at 5,000 to 10,000 exceed the budgets, an aggregate squad representation with lazily materialized individuals exists, with save and hash deterministic and equal to the individual representation where both apply | Equivalence test; numbers |
| F11 | Pooling or buffer reuse is added only where a measured hot allocation exists, with before and after numbers | The numbers in this document |
| F12 | Mutation checks: admission (a batch that should be refused is created), seeded generation (index or seed dropped), deferred rule | Mutation runs recorded |
| F13 | A headless demo spawns a 1,000-soldier field, ticks, checkpoints and replays, with timings printed | Executable run output recorded |
| F14 | Stock-flow bridge for combat (casualties and ammunition as Consumed, Destroyed and Lost inventory intents through the Director bridge) is built only if cheap once the factory exists; otherwise it stays listed | Decision recorded in section 8 |

## 6. Measurements

Measured 2026-10-27 with `src/GameRuntime/GameRuntime.CharacterBench` (Release, .NET 8.0.33, workstation concurrent GC, 12 logical CPUs, Windows 11). Raw rows: `docs/measurements/character-bench-baseline-2026-10-27.csv`. Method: the real engine (`GameFactory` over the engine's own modules: access, world setup, inventory, motion, tile navigation, work); characters are created through `World.PrepareCreation` then `World.CommitCreation` inside a director command, which is the engine's own creation path; `GC.GetAllocatedBytesForCurrentThread`, `GC.CollectionCount` and `GC.GetTotalPauseDuration` around each phase; a forced collection before each phase. Medians over 3 repeats (100 and 1,000) or 2 repeats (5,000 and 10,000, 30 ticks per phase instead of 60). Not BenchmarkDotNet: a self-contained harness, so these are single-machine wall-clock figures with no statistical analysis beyond the median; treat differences under about 15 percent as noise.

Two entity shapes, both real engine content: a **walker** (`tile_presence` and `mover`) and a **soldier** (those plus a `worker` and a `work_policy`, the shape the engine uses for anything that takes orders). The tick budget used below is 33 ms (30 ticks per second, the tick rate of the authored content).

### 6.1 Cost per tick with the population standing still (idle) and marching

| Characters | Walker idle ms/tick | Walker alloc/tick | Soldier idle ms/tick | Soldier alloc/tick | Soldier GC (gen 0/1/2 per 30 to 60 ticks) |
|---|---|---|---|---|---|
| 100 | 1.45 | 0.43 MB | 2.71 | 0.88 MB | 3/0/0 |
| 1,000 | 14.8 | 4.2 MB | 25.5 | 8.8 MB | 31/15/0 |
| 5,000 | 74.1 | 21.8 MB | 117.5 | 44.9 MB | 90/35/13 |
| 10,000 | 183.7 | 43.5 MB | 230.2 | 89.9 MB | 169/45/15 |

Per character per tick: about 14 to 18 microseconds and 4.3 KB for a walker; 23 microseconds and 9.0 KB for a soldier. The cost is linear in the population and does not depend on whether anything is happening.

Marching (each walker ordered to a distant tile on a 128 by 128 grid, so path searches run): 100 walkers 43 ms/tick, 1,000 walkers 420 ms/tick (9.7 MB/tick), 5,000 walkers 2,195 ms/tick, 10,000 walkers 4,231 ms/tick. This is a worst case for the engine's pathfinding, not a statement about an army that moves as a formation. Soldiers did not move in this run (their work policy owns their movement), so their marching row equals idle.

### 6.2 Creation, churn, save, load and hash

| Characters | Create N in one command (walker / soldier) | Churn: replace 5 percent per tick, ms/tick (walker / soldier) | Hash | Save (size) | Load |
|---|---|---|---|---|---|
| 100 | 1.1 / 2.8 ms | 6.6 / 2.6 | 0.6 / 1.0 ms | 0.7 / 1.1 ms (19 / 23 KB) | 1.7 / 2.5 ms |
| 1,000 | 15 / 26 ms | 67 / 28 | 2.9 / 10 ms | 5 / 12 ms (173 / 211 KB) | 11 / 17 ms |
| 5,000 | 59 / 110 ms | 666 / 117 | 21 / 38 ms | 20 / 42 ms (857 KB / 1.05 MB) | 50 / 83 ms |
| 10,000 | 179 / 225 ms | 1,226 / 218 | 38 / 59 ms | 44 / 87 ms (1.7 / 2.1 MB) | 114 / 142 ms |

The create figure includes the one tick that applies the command. Creation allocates 5.3 KB (walker) to 10 KB (soldier) per character and retains about 0.9 KB (walker) to 1.1 KB (soldier) per character after a forced collection. Hash, save and load are linear and small: even 10,000 soldiers save in 87 ms to 2.1 MB and load in 142 ms. Churn cost is dominated by the ordinary tick cost of the standing population (the churned fraction adds little), except for walkers at 5,000 and above where removal flushes (`List.RemoveAll` over the entity list and every component index, linear in the population) add several hundred milliseconds per tick.

### 6.3 Garbage collection

The 85,000-byte large-object threshold is not the issue: a character is about 1 KB of small objects. The issue is allocation volume per tick: 4 to 9 KB per character per tick forces generation 0 collections every one or two ticks at 1,000 and generation 1 and 2 collections as the population grows (soldiers at 5,000: 90 / 35 / 13 collections in 30 ticks). Reported GC pause time was 21 ms per 60 ticks at 1,000 soldiers (about 0.35 ms/tick) and 275 ms per 30 ticks at 10,000 (about 9 ms/tick). Pauses are a minor share of the tick; the bulk is the allocation and the work that produces it.

### 6.4 Where the per-tick allocation comes from (EventListener on the runtime's allocation-tick events, 1,000 entities, 40 ticks)

| Type | Walker share | Soldier share |
|---|---|---|
| `StateHasher` | 26.0 percent | 16.3 percent |
| `System.Action<IStateSync>` and display classes of delegate closures | 6.2 + 2.3 + 2.0 + others | 3.1 + 3.2 + others |
| `EntityView[]`, `TileSpatialSnapshot`, `MotionExecutionView` | 6.4 + 4.7 + 2.0 | 3.1 + 2.1 |
| strings, string arrays, hash sets, dictionary entries, enumerators | about 10 | about 20 (worker role enumerators 5.4, string arrays 4.6, strings 4.1) |

Reading of the source (`EntityObservation.ProviderScope`): every provider call the engine makes while updating an entity is wrapped in an *observation guard* that hashes the provider's persisted state before and after the call with a fresh `StateHasher` to prove that a read did not change state. That is a deliberate safety invariant of the engine (its notes forbid weakening such guards), and it is the main per-entity cost. It is not something a character factory should remove.

### 6.5 What the numbers say

- Individual character entities in this engine cost about 14 to 25 microseconds and 4 to 9 KB of allocation **per character per tick, whether or not anything happens**, plus path searches when they march.
- At the stated target (1,000 soldiers) individuals use 45 percent (walkers) to 77 percent (workers) of a 33 ms tick before any gameplay, and any real marching of individuals is far over budget (420 ms/tick for 1,000 walkers). At 5,000 and 10,000 individuals are out of reach by one to two orders of magnitude.
- Creation, hash, save and load are not the problem at any size tested.
- Limits of this measurement: one machine, medians of 2 or 3 runs, a worst-case march, content with the engine modules I could compose without a game's own systems (a real game adds its own per-tick cost on top, which makes the budget tighter, not looser), and no profile by call stack (type names only).


### 6.6 After the implementation: squads, factory-made individuals and the pooling experiment (same harness, same machine, 2026-10-27)

Raw rows: `docs/measurements/character-bench-squads-2026-10-27.csv`. A squad here is a factory-raised squad of 100 members: 1,000 soldiers is 10 squads, 10,000 is 100. "Marching" is the same worst case as 6.1 (every mover ordered to a far tile; here the movers are the squads). Medians of 3 repeats, 60 ticks per phase.

| Soldiers (squads) | Idle ms/tick | Idle alloc/tick | Marching ms/tick | Marching alloc/tick | Create (one command) | Hash | Save (size) | Load |
|---|---|---|---|---|---|---|---|---|
| 100 (1) | 0.10 | 8 KB | 2.3 | 15 KB | 0.3 ms | 0.2 ms | 0.4 ms (2.4 KB) | 0.8 ms |
| 1,000 (10) | 0.19 | 47 KB | 4.6 | 109 KB | 0.5 ms | 0.6 ms | 0.6 ms (9.0 KB) | 1.7 ms |
| 5,000 (50) | 0.74 | 217 KB | 23.1 | 519 KB | 1.1 ms | 2.0 ms | 2.7 ms (35 KB) | 5.7 ms |
| 10,000 (100) | 1.51 | 431 KB | 46.6 | 1.02 MB | 1.7 ms | 4.1 ms | 4.0 ms (73 KB) | 11.2 ms |

Against individual walkers from 6.1: idle is 76 times lower at 1,000 (14.8 against 0.19 ms), 100 times at 5,000 and 122 times at 10,000; marching is 91 to 95 times lower at every size. A squad costs 1.2 KB retained (0.012 KB per soldier) against about 1 KB per individual.

**Budgets from section 7, checked against these numbers:**

| Budget | 1,000 soldiers | 10,000 soldiers |
|---|---|---|
| Tick cost idle (4 ms / 10 ms) | met: 0.19 ms | met: 1.51 ms |
| Allocation per tick (500 KB / 2 MB) | met: 47 KB | met: 431 KB |
| Spawn in one command (100 ms / 500 ms) | met: 0.5 ms | met: 1.7 ms |
| Hash plus save plus load (100 ms / 300 ms) | met: 2.9 ms | met: 19 ms |
| Save size (500 KB / 2 MB) | met: 9 KB | met: 73 KB |
| **Tick cost while squads march (4 ms / 10 ms)** | **met after pass 24: 2.3 ms** (was 4.6) | **met after pass 24: 3.5 to 3.9 ms** (was 46.6) |

The marching budget was missed in pass 23 and is met after pass 24 (section 6.7). The pass-23 attribution to the path search was wrong: a call-stack profile showed the cost in the engine's own state validation, not in the pathfinder.

**The whole game, 10,000 soldiers** (`GameRuntime.Field.Headless`, Release, fixed frames, world + combat for 100 groups + commander + per-tick journal fingerprint): 62 ticks to the end of the fight, 6.3 ms per tick on average, 519 KB allocated per tick, one generation 0 collection, checkpoint taken in 32 ms (78 KB), and the journal replays all 62 ticks. At 1,000 soldiers: 47 ticks, 2.9 ms per tick (includes first-use compilation), 65 KB per tick.

**Individuals made by the factory** (lean entity: tile presence, mover and the character component; no worker): 1,000 characters idle 14.2 and 17.6 ms/tick after a 28.6 ms first repeat that was warm-up, against 12.5 to 13.4 for bare walkers measured in the same run: about 15 to 30 percent more than a bare walker, and 83 against 74 ms/tick at 5,000. Marching is the same as walkers (564 and 2,015 ms/tick at 1,000 and 5,000). Creation of 1,000 took 25 to 110 ms (first run includes compilation), under 0.15 ms per character, with the whole-batch admission run before creating. So individuals are for the few characters a game wants to simulate one by one (heroes, named workers, duels): hundreds, not thousands.

**Pooling experiment (decision F11).** The measured hot allocation was the observation guard's `StateHasher` (section 6.4). I prototyped a per-thread stack of reusable hashers for it (about 20 changed lines in `StateSync.cs` and `EntityObservation.cs`) and ran the same 1,000-entity idle benchmark with and without it, back to back:

| 1,000 entities, 30 idle ticks | Allocation per tick | Gen 0 / 1 collections | GC pause | Wall clock ms/tick (3 repeats) |
|---|---|---|---|---|
| walker, current engine | 4.18 MB | 7 / 3 | 10.7 to 15.0 ms | 37.4, 20.2, 19.7 |
| walker, pooled hasher | 3.10 MB (-26 percent) | 5 / 2 | 4.5 to 5.5 ms | 36.3, 34.1, 19.9 |
| soldier, current engine | 8.80 MB | 15 / 7 | 17.3 ms | 31.4, 32.0, 30.3 |
| soldier, pooled hasher | 7.27 MB (-17.5 percent) | 13 / 6 | 11.4 to 13.4 ms | 34.3, 32.5, 28.9 |

It removes exactly the share the sampling predicted and roughly halves GC pause time, but wall-clock time per tick did not improve beyond the machine's run-to-run spread (19.7 to 37.4 ms for the same build), so no throughput benefit is shown. **Not adopted:** the kernel files are back to their previous bytes (checked with a byte comparison). Reasons: no shown speed gain; the squad representation makes the saving irrelevant to the 1,000 and 10,000 goal; and the guard is the engine's own safety mechanism. The prototype and its numbers are kept here as the evidence that the allocation can be removed if a game ever needs it.
### 6.7 Pass 24: why marching cost so much, and the fix (2026-10-27)

Method: `dotnet-trace` with the runtime sample profiler on a run that only marches (`GameRuntime.CharacterBench --march squad 10000 300`), top-N by exclusive and inclusive time, then one change at a time with the build run back-to-back on the same machine. Raw rows: `docs/measurements/character-bench-marching-2026-10-27.csv`.

What the profile showed (facts): path search (`PathSearch.FindCore`) was about 11 percent of the marching tick even before any fix. About 60 percent was `Mover.Sync` and its `ValidatePayload`, called through the engine's observation guard (hash before and after every provider call), the snapshot capture and `ValidateLive`. `ValidatePayload` walks the whole route and, for every step, called `Geometry`, which re-selected and re-validated the tile-geometry list, and read the presence footprint twice through a cast. Another 10 to 20 percent was list growth and the garbage it caused in `GameSnapshot.Capture`.

Changes (all in SimEngine, none touches content, saves or hashes):
1. `Mover` resolves its tile geometry once (the full declaration check still runs on that first read) instead of per bounds test.
2. `ValidatePayload` reads the geometry and footprint once per route instead of per step.
3. `Mover.Sync` while writing no longer validates the same fields a second time; loaded data is still always validated.
4. `GameSnapshot.Capture` sizes its entity list up front.
5. `Mover` remembers that its current route (path, origin and goal) passed the full chain check and walks it again only after the route changes. Every writer of the path, origin and goal clears the memory; loaded routes are never covered by it. The hash still serialises the whole route, so a changed route cannot hide from the guard; the memory only skips re-proving an unchanged one.

Result (the benchmark's official `tick-moving`, same session, build before against build after, three repeats each, first repeat is warm-up):

| soldiers (squads) | before | after | budget |
|---|---|---|---|
| 1,000 (10) | 5.4 to 6.3 ms | 2.3 ms | 4 ms |
| 10,000 (100) | 41 to 56 ms | 3.5 to 3.9 ms | 10 ms |

The `--march` stress (a hundred squads ordered across the map at once, measured over the first 100 ticks while routes are longest) went from 44 to 51 ms to 9.4 to 11.0 ms at 10,000, and from 9.6 to 11.6 ms to 1.6 to 2.0 ms at 1,000. Each step's contribution is in the CSV; steps 1 and 2 gave the largest gains. Idle, churn, hash, save and load rows did not regress (`tick-idle` 0.6 to 0.7 ms at 1,000).

With tiered JIT switched off (`DOTNET_TieredCompilation=0`, so a 60-tick run is not decided by when the runtime tiers code up; with it on, an idle 1,000-soldier tick read 0.14 to 0.75 ms depending on tier-up timing in both builds, which is noise, not a regression), build before against build after, three repeats each: idle 0.16 to 0.21 ms against 0.17 to 0.19 ms at 1,000 and 1.7 to 2.2 ms against 1.5 to 1.8 ms at 10,000 (unchanged); marching 5.7 to 6.9 ms against 0.47 to 0.54 ms at 1,000 and 57 to 63 ms against 4.1 to 5.4 ms at 10,000 (about 12 times lower); churn, which also marches, 4.5 to 5.2 ms against 0.36 to 0.41 ms and 46 to 50 ms against 3.4 to 4.0 ms.

Not done and not measured: sharing a search among squads that head the same way (the profile showed the search is not the cost, so I did not build it); a formation moving along one path. Determinism is covered by `MoverRouteTests` (save mid-route, load, continue to the same hash and bytes; a new order replaces the route; two runs hash equal at every tick) and the existing 260 runtime tests plus 351 content tests, all green. The route memory cannot be shown to matter by a public-API test: a stale flag only matters if the route were corrupt, which only a tampered save can cause, and loaded routes are always checked in full.
## 7. Decision

Decision from section 6, made before any factory code:

1. **(a) Individual entities are fine up to a few hundred, not at 1,000 or more.** Per-character cost is linear and high because of the engine's per-entity guards. 1,000 individually simulated soldiers do not meet a 33 ms tick once they move; 5,000 and 10,000 are far out of range. I therefore do **not** claim that individuals work at 1,000+.
2. **(b) Soldiers in formations are represented as an aggregate squad entity.** A *squad* is one engine entity (one tile presence, one mover, so one path search for the whole body) carrying a *squad component*: the formation id, the roster as counts per archetype, a caller seed, the lost slots, and the map of materialized individuals. Individuals are **not stored**: character `i` of squad `s` is a pure function of (squad seed, i, archetype) computed on demand, so a squad of 10,000 costs the same per tick as a squad of 10 and saves a few hundred bytes plus the sparse deviations (casualties, promotions, materialized ids).
3. **Individuals are materialized lazily.** When something needs one (inspection, a named hero, a duel, a worker role), the factory creates a real lean entity (walker shape plus a `character` component) from the same pure function, links it to the squad slot and marks the slot as materialized so the aggregate does not also count it. Dematerializing returns the slot to the aggregate only if no state diverged; otherwise the individual stays. Both representations produce the same character for the same slot.
4. **Budgets (acceptance criterion F9)**, set from the baseline above. With 1,000 soldiers in squads of about 100: added tick cost no more than 4 ms/tick idle and while squads march, allocation no more than 500 KB/tick, spawn of the whole field in one command no more than 100 ms, hash plus save plus load no more than 100 ms in total, save no more than 500 KB. With 10,000 soldiers in squads of about 100: no more than 10 ms/tick, 2 MB/tick, 500 ms spawn, 300 ms for hash plus save plus load, 2 MB save. Against the baseline these require 6 times (1,000) and 20 times (10,000) lower per-tick cost than individual soldiers; the squad design delivers it by having 10 or 100 engine entities instead of 1,000 or 10,000.
5. **Where individuals still matter**, the factory builds them as lean entities (tile presence, mover, `character`; not a worker with a work policy unless the archetype asks for one), because the soldier shape costs 1.7 times the walker per tick.
6. **Pooling.** No pooling of characters or squads in the simulation core. The measured hot allocation in the engine is the observation guard's `StateHasher` (16 to 26 percent of per-tick allocation, one fresh instance per provider call). A reused, reset hasher per thread would remove that share without changing a hash value, but the guard code is the engine's own safety mechanism and was written deliberately with a "fresh, non-escaping hash ownership" comment; I will change it only after the squad work is done and only if I can show a before and after number and that every hash is identical, as a separate step (checklist C5). The view layer (Godot) pools nothing in the simulation; it draws from a snapshot (section 1.5).
7. **Identity.** Squads and materialized individuals use the engine's monotonic ids. Unmaterialized soldiers have no engine id; they are addressed as (squad id, slot), and that pair is stable because slots are never renumbered (a lost soldier leaves its slot in the lost list).

## 8. Result and remaining boundary

Status of the acceptance criteria of section 5 after the work (2026-10-27):

| # | Status | Evidence |
|---|---|---|
| F1 single owner | met | the engine refuses to save, load or hash a character or squad the factory did not make (test); source guards: nothing outside the module names the two components, only the factory writes their state, no director effect or headless game calls the creation path |
| F2 strict JSON, codes | met | 18 content-mistake tests refused at load with a message naming the fault; 21 bad-request tests with their refusal codes at the bridge; strict payload reading (unknown, missing, duplicate, wrong type) |
| F3 deterministic generation | met | pinned character; culture test (3 cultures); mutations: index dropped from the seed and caller seed ignored both fail tests |
| F4 all-or-nothing | met | whole-batch admission before any entity is made; refusal tests compare the entity hash before and after; a layout whose last member is off the map creates nobody |
| F5 one creation path, ids never reused | met | the factory only calls PrepareCreation and CommitCreation; ids stay monotonic across removal, save and load (test) |
| F6 deferred rule | met at the bridge | a request changes nothing until the world ticks and the answer is events (test); the factory itself is only reachable inside a tick (director effects) |
| F7 save, load, replay | met | save/load/hash equality with squads, individuals and losses; two runs give the same hash at every step; the field game replays its whole fight tick for tick (tests, and by hand 92 ticks) and a saved run continues to the same final state and world hash |
| F8 roster facts have one owner | met | squads raised from every arms-modern formation have the catalog headcount and the combat group agrees; equipment ids exist in the catalog; the engine module and the combat engine hold none of it (source guards) |
| F9 scale budgets | met | table in 6.6 and 6.7: all budgets met for 1,000 and 10,000 soldiers; marching 2.3 ms against 4 and 3.5 to 3.9 ms against 10 after pass 24 |
| F10 aggregate squad | met | squads with lazily materialized individuals; a materialized member equals what the squad described for its slot (test) |
| F11 pooling only with measured benefit | no pooling adopted | experiment in 6.6: allocation -17 to -26 percent, GC pause about halved, no wall-clock gain beyond noise; reverted |
| F12 mutation checks | met | admission skipped (2 to 5 tests fail), layout checks removed (5), index dropped (3), seed ignored (1), bridge admission skipped (15), casualties not carried into squads (2), spread of fire removed (2) |
| F13 headless demo | met | `GameRuntime.Field.Headless`: 1,000 and 10,000 soldiers, output in section 8.1 |
| F14 combat stock-flow bridge | built in pass 24 | `docs/COMBAT_STOCK_FLOW.md`: people and rounds as inventory on each squad, recorded as Initialized, Consumed, Destroyed and Lost flows, reconciled with combat and the squads at the end of a run |

### 8.1 The demo, as run

```
game field a=5xarms-modern.rifle-company@arms-modern b=5xarms-modern.rifle-company@arms-modern seed=1 tick-rate=30 mode=fixed-frame frame-us=33000
t2 raised a=5 squads of arms-modern.rifle-company (500 people), b=5 squads of arms-modern.rifle-company (500 people)
t17 round 1 headcount a=400/500 b=400/500 ...
t47 ended side-1-wins
final a=252/500 b=207/500 rounds=3
timing build=121.0ms run=135.2ms ticks=47 per-tick=2.878ms allocated-per-tick=64828B gc=0/0/0 entities=10

game field a=50xarms-modern.rifle-company@arms-modern b=50xarms-modern.rifle-company@arms-modern seed=1 tick-rate=30 mode=fixed-frame frame-us=33000
t2 raised a=50 squads of arms-modern.rifle-company (5000 people), b=50 squads of arms-modern.rifle-company (5000 people)
t62 ended mutual
final a=2155/5000 b=2151/5000 rounds=4
timing build=112.6ms run=391.6ms ticks=62 per-tick=6.316ms allocated-per-tick=519215B gc=1/0/0 entities=100
timing save=32.1ms
```

### 8.2 What is not done, and the boundary

- **Combat stock-flow bridge (F14): built in pass 24, see `docs/COMBAT_STOCK_FLOW.md`; its limits are listed there.** What pass 23 verified in the source: the engine has `IResourceTransaction.CommitInventory(ResourceInventoryIntent)` with the kinds Consumed, Destroyed and Lost over an endpoint entity (`ConstructionRecovery` uses it), which is the path casualties and spent ammunition would take. What it needs and I did not build: a `storage` endpoint on the squad entity, inventory resources for people and for each ammunition item (the catalog-to-SimEngine converter would have to produce them), an Initialized flow at spawn from the formation's strength and equipment, and a layer above both the inventory module and the character module (the character module deliberately depends on no other module). That is a feature of its own, not a small addition.
- **Marching cost** (6.6, 6.7): fixed in pass 24; the stress case of every route at its longest (`--march`) is still 9.4 to 11.0 ms at 10,000 soldiers.
- **Godot rendering of the field:** nothing built or run; the view contract in 1.5 (draw from a snapshot with a MultiMesh or server canvas items, pool nothing in the simulation) is a plan, not code. No Godot run was made.
- **Unnamed members have no entity id:** they are addressed as (squad, slot). A system that needs an id for every soldier (for example targeting one soldier from another squad) has to materialize that soldier first. Materialization is one way by design (an individual stays until it dies).
- **Casualties take unmaterialized members only** in the squad's seeded order; individuals die only through `Kill`.
- **Measurement limits:** one machine whose speed varied between runs (the same build measured 12.5 to 37 ms for the same 1,000 walkers on different occasions), medians of 2 or 3 repeats, a worst-case march, and no call-stack profile (type names only). Treat the ratios (squads against individuals, 76 to 122 times at idle, 91 to 95 times marching) as solid and the absolute milliseconds as indicative.
- **SimEngine's own test suites:** this tree has no runnable SimEngine test project (only the test kit), so I could not run engine-side suites. The engine change is one additive module in `Standard/Characters` plus the director bridge's event forwarding in GameRuntime; no kernel file is changed (the pooling experiment was reverted and compared byte for byte). It is exercised by 59 + 27 + 7 + 5 tests in GameRuntime.Tests.
