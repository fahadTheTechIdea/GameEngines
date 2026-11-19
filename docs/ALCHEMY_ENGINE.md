# AlchemyEngine

A pure rules library for combination games, plus its place in the game layer. Written 2026-10-27.

## Layer boundary decision

| Concern | Owner | Reason |
|---|---|---|
| Resource ledger (stock, reservations, transfers, flow facts, cash) | **SimEngine** (stays) | It is saved world state, hashed per tick, and a transfer is an atomic commit inside a tick. Moving it to another engine would split that commit across two engines and make save, replay and the single clock coordinate across them. |
| Recipes and reactions (inputs, catalysts, environment, outputs, chance) and what the player has discovered | **AlchemyEngine** (separate) | It is a pure function of items and a caller-supplied context. It has no `World`, no clock and no hidden random state, so a game can use it without SimEngine. |
| Running it per tick, receiving requests, answering with events | `GameRuntime.Alchemy` (`AlchemyComponent`) | The thin game-layer adapter, like `SimComponent` and `MissionComponent`. |

A shared `ResourceCore` contracts library is **not** built: nothing outside SimEngine needs its resource types yet, and a library with one consumer is a rule 6 violation. Extract it when a second consumer exists.

## What was built

- `src/AlchemyEngine/AlchemyEngine.Core`: `AlchemyContentLoader` (strict JSON, every refusal with its own code), `AlchemyRules` (`Combine`, `WhatMakes`, `WhatDoes`), `DiscoveryBook` (known elements and recipes, tried combinations, attempt counter, digest-checked save/load).
- `src/GameRuntime/GameRuntime.Alchemy`: `AlchemyComponent`, the five events (`CombineRequested`, `Combined`, `CombineRefused`, `DiscoveryMade`, `KnowledgeRestored`), `AlchemyCheckpoint`.
- `src/GameRuntime/GameRuntime.Alchemy.Headless`: the playable headless game (engine-style main loop, `InputState`, `AlchemyPlayerComponent` with `methodical` and `curious` policies, script, checkpoint, journal and replay).
- Shared owners moved up so both headless games use them: `EngineMainLoop` and `OutputLog` (GameRuntime.Core), `InputEdgeJournal` (GameRuntime.Services).

## Rules the content can express

- **Inputs** are consumed; **catalysts** must be in the vessel and stay; **environment** tags must all be on offer (for example `heat`); **outputs** may be several elements.
- **Ordered** recipes match only the exact input sequence; unordered recipes match any order.
- **Chance** is an exact fraction decided only from the caller's seed, the caller's attempt counter, the recipe and the items (SHA-256). A fizzle teaches nothing and stays open to another try.
- Every field is required; nothing is defaulted. Unknown fields, unknown references, duplicate ids, empty inputs or outputs, a catalyst that is also an input, a bad chance, an element that can never be made, and two recipes that could match the same combination are all refused with their own code.

## Game-layer behaviour

Raising `CombineRequested` only queues it. `AlchemyComponent.Update` answers requests in arrival order using that tick and the book's attempt counter, and emits `Combined` or `CombineRefused`, then `DiscoveryMade` for each new element and recipe. A request raised by a component that runs after it (the player) is answered in the following tick (tested). At Init it emits `KnowledgeRestored` with everything the book already holds, so a listener created after a load rebuilds its view from events.

## Research: how comparable games define combination rules

Sources checked 2026-10-27. These are community pages and one developer interview; no official design documents were found, so "documented" below means "stated on those pages".

- Doodle God ([fan combination guide](https://ayumilove.net/doodle-god-guide/)): documented as tiered recipes in which earlier results feed later ones (earth + fire = lava, air + earth = dust). The same guide says completion requires every reaction, not one per element, and that the game offers hints on a timer.
- Noita ([wiki: Alchemy](https://noita.wiki.gg/wiki/Alchemy), [80.lv interview](https://80.lv/articles/noita-a-game-based-on-falling-sand-simulation/)): documented as reactions listed with reagents, products and a rate, inside a per-pixel simulation; fire reaching water gives steam; custom materials and reactions are data files.
- Little Alchemy: community pages describe dragging one element onto another, several recipes per element, and a tiered build-up. They disagree on element counts, so none are used here.

| Pattern | Decision | Why |
|---|---|---|
| Data-defined recipes, elements built from earlier elements | Adopted | It is the whole model of the genre and keeps content out of code. |
| Several recipes may make the same element | Adopted | Allowed; discovery tracks recipes separately from elements. |
| Reactions carry a rate or probability | Adapted | An exact fraction decided from seed and attempt, so a roll is reproducible. Noita-style continuous simulation is a different engine. |
| Reaction depends on surroundings (heat) | Adapted | Explicit environment tags the caller offers; no spatial simulation. |
| Per-pixel physical simulation | Avoided | It belongs to a falling-sand engine, not a recipe library, and is not needed for combination games. |
| Hints on a timer, wall-clock cooldowns | Avoided | A wall clock breaks determinism. Hints, if wanted, would be a game feature driven by ticks. |
| Silent failures on unknown combinations | Avoided | Every invalid request has a named refusal code; "no reaction" is a distinct, valid outcome. |
| Unreachable content found by players | Avoided | The loader refuses elements that cannot be made from the starting set. |

## SimEngine bridge (SimEngine to alchemy)

The first design for this bridge (an authored mapping document compiled into SimEngine content) was dropped on your instruction: SimEngine already defines resources and recipes, so the bridge reads them. Alchemy stays a discovery layer; the engine stays the owner of the recipe fact.

- `SimRecipeSource.Read(ResolvedContent)` (project `GameRuntime.AlchemySim`) reads the resolved content of a composed world: its resources become alchemy elements (a resource no bridged recipe makes starts known; the authored category, phase and traits become properties), and each gated batch transformation becomes an alchemy recipe: consumed resources are inputs, held tools are catalysts, made resources are outputs. The alchemy document is produced in memory and loaded through the alchemy loader, so its own checks (ambiguous combinations, unreachable elements, bad ids) apply with their own codes.
- Nothing physical is copied into alchemy. Quantities, tool counts, ports, duration, cash and quality rules are exposed as the engine's own read-only `SimRecipeFacts` (its `Bill`, `GameDuration` and `ResourcePortRoute` values), with the entity definitions that can run each recipe and the operation that queues it.
- What alchemy cannot represent is excluded with a code and never half bridged: `ALCHEMYSIM_CONTINUOUS` (rates, not a batch), `ALCHEMYSIM_ENTITY_OUTPUT`, `ALCHEMYSIM_NO_INPUTS`, `ALCHEMYSIM_TOOL_IS_INPUT` (one resource both consumed and held), `ALCHEMYSIM_NOT_GATED` (no technology unlocks it) and `ALCHEMYSIM_GATE_AMBIGUOUS` (its technology has prerequisites, unlocks other things, or a second technology also unlocks it). Two bridged recipes that could match one combination are refused by the alchemy loader (`ALCHEMY_AMBIGUOUS_RECIPE`).
- Order and chance have no engine counterpart and alchemy does not need them for these recipes (unordered, certain), so nothing is dropped. Environment tags: none are produced.
- **Discovery gates ordering through the engine's own mechanism.** The game authors a technology that unlocks each recipe (`technology_transformation_unlocks`); the engine's content-access rule then refuses the enqueue command until the company's knowledge holds that technology. When the alchemy book discovers a recipe, `RecipeUnlockComponent` requests a `sim.discover-recipe` effect; `DiscoverRecipeEffect`, the one writer, records the technology in the engine's company knowledge (the same record a finished research leaves) and publishes the engine's `TechnologyLearned` event. A technology learned some other way (finished research) comes back as that event and is taught to the book with `DiscoveryBook.LearnRecipe`, so the two stores converge from either side. `AlchemySimConsistency` reports any recipe the book and the sim disagree about, and the checkpoint refuses while there is one.
- `SimOrderComponent` submits the engine's enqueue command on a request and reports the engine's own verdict (`APPLIED` or the engine's refusal code, for example `technology.locked`). It checks nothing itself.

### Alchemist workshop (the proof game)

`src/GameRuntime/GameRuntime.AlchemySim.Headless`: the SimEngine proof content "Planet X production" (two recipes, one with a held crucible and a byproduct, one with a held fixture), a tile world with a hauler authored for it, and the gate document. Resources, recipes and the logistics handling profile are copied unchanged from git history (`src/SimEngine/tests/ExtensionProof/content/`); the original world used a point-space module that existed only in the deleted proof assembly, so the world document is rewritten on the engine's native tile space (the same approach as the haul test content). The player (a component with an `InputState`, a bot policy `alchemist`, a script, an engine-style main loop) tries combinations of the sim's resources; a discovered recipe unlocks in the sim a tick later; the player then orders it; the carrier hauls the inputs and the sim produces the part, shown by the engine's own stock events.

### Acceptance criteria result

See the eighteenth-pass entry in `RUNTIME_PIPELINE_INTEGRATION_PLAN.md` for evidence against A1 to A8.

## Recipe discovery over an existing production sim

Researched 2026-10-27, before the SimEngine bridge was written. "Documented" means stated on the cited page; "inference" is ours. Dyson Sphere Program was searched for and not found, so nothing is claimed about it. Where a page was only seen through a search summary rather than fetched in full, it is marked.

### What comparable games document

| Product | Documented | What it keeps as knowledge vs as a production rule |
|---|---|---|
| Minecraft recipe book ([wiki](https://minecraft.wiki/w/Recipe_book)) | Recipes unlock when the player meets a criterion (usually holding a particular item). Unlocked recipes stay in the "all" list even without materials, and an unaffordable one is highlighted red; a "craftable" filter shows only those the inventory covers. By default no recipe needs to be unlocked to craft; the `doLimitedCrafting` game rule makes the unlock required. Data packs express unlocks as hidden advancements with a `recipes` reward ([Forge docs](https://docs.minecraftforge.net/en/1.20.1/resources/server/recipes/), secondary). | Knowledge = the player's unlocked set. The recipe itself (ingredients, result) is a separate data file. Enforcement is optional and off by default. |
| Factorio ([recipe prototype](https://lua-api.factorio.com/latest/prototypes/RecipePrototype.html), [technology prototype](https://lua-api.factorio.com/latest/prototypes/TechnologyPrototype.html)) | A recipe's `enabled` defaults to true; "If a recipe is unlocked via technology, this should be set to `false`." Ingredients and results are lists of name and amount; duplicate ingredient names are not allowed, duplicate results are; multiple results are allowed. A technology's effects include an `unlock-recipe` entry naming the recipe (seen through the official wiki's search summary, not a full fetch). | Knowledge = researched technologies. The recipe is a prototype that stays disabled until a technology enables it, so the recipe and the technology each carry a piece of the same fact. |
| RimWorld ([research projects](https://rimworldwiki.com/wiki/Modding_Tutorials/Research_Projects)) | Research projects gate buildings and crafting recipes; most projects need a prerequisite, and some need a particular bench. | Knowledge = completed projects; production rules live on the buildings and recipes the project unlocks. |
| Satisfactory ([hard drive research](https://satisfactory.wiki.gg/wiki/Hard_Drive_Research)) | Scanning a hard drive in the MAM offers a choice of alternate recipes, each needing one drive; 106 alternates in all. | Knowledge = unlocked alternates, alongside the default recipes. |
| Little Alchemy, Doodle God ([guide](https://ayumilove.net/doodle-god-guide/)) | Combine two known elements; results become known; recipes are tiered; a completion list covers every reaction. | Knowledge is the whole game; there is no separate production sim. |

### Representation (item 2 of the brief)

- Documented: Factorio recipes list several results; Minecraft shows known-but-unaffordable recipes red and filters to craftable ones. Factorio forum threads (secondary: [one](https://forums.factorio.com/viewtopic.php?p=615536)) discuss marking a product a "catalyst" so productivity statistics ignore the returned amount, which reads as a statistics flag rather than a rule that the input stays.
- Not found: how RimWorld, Satisfactory or Dyson Sphere show known versus available versus craftable. Not claimed.
- Inference: three states matter to a player: *unknown* (not yet discovered), *known* (discovered, orderable), *runnable now* (the physical conditions hold). Only the first two belong to knowledge; the third is the production sim's.

### Bad patterns found, and what we take from them

| Pattern | Evidence | Rule we adopt |
|---|---|---|
| An unlock that nothing enforces | Documented: Minecraft's recipe book is advisory unless `doLimitedCrafting` is on. Inference: a flag nobody checks is the failure project rule 7 describes. | The unlock must be enforced by the sim's own order command, so a client that skips the UI is still refused. |
| The same fact in two tables that drift | Documented (secondary): a 0.17.3 Factorio report of recipes marked `enabled = false` that were nevertheless available, [forum](https://forums.factorio.com/viewtopic.php?p=405072). Inference: a recipe flag plus a technology list is two places. | One owner for the recipe fact (SimEngine content). Alchemy copies none of it. |
| Knowledge held in two stores | Inference from our own design: a discovery book plus an engine knowledge ledger can disagree. | One writer for the ledger, derived from the book, with a consistency check at every checkpoint. |

### Real content chosen

Searched: the SimEngine quickstart content (resources only), the tycoon content in `GameRuntime.Tool` and `GameRuntime.Headless` (cash, no transformations), the research and haul content in `GameRuntime.Tests` (technology and haul orders, no transformations), and SimEngine's own proof content, which remains in git history under `src/SimEngine/tests/ExtensionProof/content/` (the working copy of that project was deleted at your direction; only data files are restored from git). That proof content holds 54 documents with transformations. Counting recipes per document, `input-ports.json` has the most (8) but they are variants of one port probe; the Planet X production set is the richest coherent game: a resource list, two recipes, a staffed logistics world, built only from public engine modules.

**Chosen: Planet X production.** Smelting turns 10 ore, with a reusable crucible, into 6 alloy and 4 slag (a tool and a byproduct); assembly turns 3 blanks, with a reusable fixture, into 1 finished part. Its authored resources, recipes, world, work and logistics documents are copied unchanged; the only additions are the discovery gate documents a game must author (below).

### Acceptance criteria, fixed before coding

| # | Criterion | Evidence required |
|---|---|---|
| A1 | The recipe fact has one owner: SimEngine content. Alchemy content is derived at run time from the loaded content and holds no quantities, ports, durations, costs or quality rules. | The alchemy view is produced by reading the engine's resolved content; the project ships no recipe JSON; amounts appear only as read-only facts taken from the engine. |
| A2 | Alchemy keeps only discovered-set knowledge (elements, recipes, tried combinations). | The discovery book is the alchemy library's own class, unchanged. |
| A3 | Enforcement is the engine's: ordering an undiscovered transformation is refused by the sim command itself. | A test submits the engine's enqueue command directly (no player, no UI), sees the engine's locked refusal before discovery and acceptance after. |
| A4 | There is one writer of the engine's knowledge for a discovered recipe, and the two stores are checked for agreement. | A single effect class writes the ledger; a consistency check compares ledger and book and fails on a seeded disagreement. |
| A5 | Nothing the alchemy view cannot represent is silently dropped: it is kept as a read-only engine fact, or refused with its own code. | Tests for each case: tool becomes a catalyst; continuous mode, entity output, a tool that is also an input, an ungated recipe and a gate with prerequisites each give a named exclusion. |
| A6 | Discovery drives the sim by events and obeys the deferred-Update rule. | The end-to-end run shows discovery, then the unlock effect, then the order, each answered a tick after it was raised. |
| A7 | Journal replay and save/load match on the combined session. | Replay reports a match; a checkpoint taken mid-run continues to the same final fingerprint as an uninterrupted run. |
| A8 | The gating is mutation-checked. | Disabling the gate (or the enforcement) makes the gating test fail. |

## Verified

See the seventeenth-pass entry in `RUNTIME_PIPELINE_INTEGRATION_PLAN.md`; the eighteenth-pass entry covers the SimEngine bridge.

## Catalog-driven changes (2026-10-27, twentieth pass)

Content generated from the resource and recipe catalog (`docs/RESOURCE_RECIPE_CATALOG.md`) needed two honest extensions of the rules, both covered by `AlchemyEngineTests`:

- A reaction needs something in the vessel, not necessarily an input: **catalysts alone** are enough, so a tool used on its own (a pick in a mine, bare hands foraging) is a valid reaction that keeps the catalyst and makes the outputs.
- A reaction may **make nothing** when it consumes something (eating, burning). A reaction that consumes nothing and makes nothing is still refused.

The converter relies on environment tags for station, skill, era, time (`time-passes`) and alternate recipes over one vessel (`method-<recipe>`); the game offers the tags it wants a player to be able to use.

## Discovery over market production (pass 46, 2026-10-29)

Alchemy still carries no production: its recipe has kinds only. A recipe authored once in the catalog is discovered here and **run** elsewhere: as a per-entity transformation by SimEngine (the bridge above) or as market-level production by the EconomyEngine (`docs/ECONOMY_ENGINE.md` section 13, `docs/RESOURCE_RECIPE_CATALOG.md` section 11). `EconomyRecipeUnlocks` (project `GameRuntime.Economy.Bridges`) listens for `DiscoveryMade` of a recipe the economy document declares locked and raises `EconomyRecipeUnlockRequested`; the economy unlocks it in its own Update. The alchemy recipe ids are the catalog's, so no mapping table exists. One recipe runs in one layer (`RecipeOwnership`).
