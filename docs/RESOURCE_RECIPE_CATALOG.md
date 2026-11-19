# Resource and recipe catalog

Dated 2026-10-27. This document is the research, the decisions and the acceptance criteria for one versioned JSON standard for game resources and recipes, the tools that validate it, converters to AlchemyEngine and SimEngine, and the original content packs written in it. It was written before any of that code.

**Method and honesty.** Sources are the games' own wikis and documentation where a search or fetch reached them. Many searches returned community wikis, forums and guides; those are labelled *secondary*. "Documented" means stated on the cited page; "inference" is ours. A game or topic the searches did not reach is listed under *not reached* and nothing is claimed about it. No game data is copied: the packs are original and generic.

## 1. What the research found

### 1.1 Genre by genre

| Genre / game | Documented | Source | Caveat |
|---|---|---|---|
| Survival crafting: Minecraft | Crafting, smelting, blasting, smoking, campfire cooking, stonecutting and smithing recipes all use one data-driven system; brewing has its own section. Shaped recipes depend on placement, shapeless ones do not. Furnace-family recipes carry a cooking time (vanilla: 200 ticks, 100 for smoker and blast furnace, 600 for campfire). Smithing takes a template, a base and an addition. | [Minecraft Wiki: Recipe](https://minecraft.fandom.com/wiki/Recipe), [NeoForged built-in recipes](https://docs.neoforged.net/docs/1.21.1/resources/server/recipes/builtin) | Wiki seen only in part. |
| Survival crafting: Valheim | Crafting stations are upgraded by placing nearby extension pieces; each piece raises the station level by one and recipes unlock by station level (for example a Workbench Level 3 recipe). A forge has more levels than a workbench. | [Valheim wiki: Forge](https://valheim-wiki.mave.me/Forge), [PC Gamer](https://pcgamer.com/valheim-workbench-upgrade-level) | Guides disagree on maximum levels and costs. |
| Survival crafting: Don't Starve | Items are unlocked by "prototyping" at a Prototyper (Science Machine, Alchemy Engine) by tier; a plain Crafting Station does not unlock the recipe for the player. Recipes are grouped in crafting tabs. | [Don't Starve wiki: Prototype](https://dontstarve.wiki.gg/wiki/Prototype) | Single-player with DLC. |
| Factory: Factorio | A recipe has a category; a crafting machine lists the categories it can craft, a crafting speed, an energy usage and source, module slots and optional fluid boxes. The default "crafting" category cannot hold fluid recipes. Items have a stack size (1 if not stackable), an optional spoil time and spoil result, a fuel value and fuel categories, a weight and a subgroup. Ingredients and results are lists of name and amount; duplicate ingredient names are refused, duplicate results are allowed. A recipe unlocked by technology should start disabled. | [Prototype/Recipe](https://wiki.factorio.com/Prototype/Recipe), [Prototype/CraftingMachine](https://wiki.factorio.com/Prototype/CraftingMachine), [Lua API: Item](https://lua-api.factorio.com/latest/prototypes/ItemPrototype.html), [Lua API: CraftingMachine](https://lua-api.factorio.com/latest/prototypes/CraftingMachinePrototype.html) | Property list read from the first 100,000 characters of each page. Spoilage needs the Space Age expansion. |
| Factory: Satisfactory | Alternate recipes are unlocked individually (one hard drive each, 106 in all); machines are tiered by role. | [Hard drive research](https://satisfactory.wiki.gg/wiki/Hard_Drive_Research) | Official recipe tables were not reached. |
| Factory: Dyson Sphere, Shapez | *Not reached.* | | |
| Colony: RimWorld | A recipe turns ingredients, which are consumed, into products; ingredients can be named as general categories ("any meat"); bills are attempted from the top and skipped when ingredients are out of reach or the pawn lacks the skill; research projects gate buildings and recipes. | [Bill](https://rimworldwiki.com/wiki/Bill), [RecipeDef](https://rimworldwiki.com/wiki/RecipeDef), [Research projects](https://rimworldwiki.com/wiki/Modding_Tutorials/Research_Projects) | Work amount and material ("stuff") rules were not reached. |
| Colony: Dwarf Fortress | Reactions are data: a reaction names reagents (quantity, material type) and products, is available at the buildings it lists, and may name fuel, a skill, or run automatically. Reactions must be added to a civilization's entity file to be usable. | [Reaction](https://dwarffortresswiki.org/index.php/DF2014:Reaction), [Raws](https://dwarffortresswiki.org/index.php/v0.34:Raws) | Older namespaces; material classes were not clearly documented in the results. |
| City/colony: Anno | Residents sit in tiers; needs are basic or luxury; meeting basic needs raises residents per house, and houses must be full to advance. Consumption is a per-residence rate. | [Anno 1800 wiki: Population](https://anno1800.fandom.com/wiki/Population?oldid=1132), [Artisan Residence](https://anno1800.fandom.com/wiki/Artisan_Residence) | Community wiki; revisions differ. |
| City: Cities Skylines | Specialised industries turn raw materials into products and generic industries turn those into goods for commerce; storage buffers extractors and processors; missing inputs are imported. | [Steam discussion](https://steamcommunity.com/app/255710/discussions/0/615086038684225212) | Player posts only. |
| Colony: Banished | Food goes to a barn first; markets draw from barns; trading posts offer one category of goods per merchant. | [Gamepressure: Trade](https://www.gamepressure.com/banished/trade/z46086) | Guide, not official. |
| Tycoon: RollerCoaster Tycoon, Software Inc, Capitalism | Income from tickets, concessions and sponsorship against build, upkeep, staff and marketing; staff roles and product features in Software Inc; supply-chain advice in Capitalism. | [Software Inc listing](https://gg.deals/au/game/software-inc/), [Capitalism Lab strategy](https://www.capitalismlab.com/?p=8112) | Marketing and aggregator pages; no mechanics reached. |
| Farming/life: Stardew Valley | Artisan equipment turns inputs into goods over game time: a preserves jar makes jelly from fruit and pickles from vegetables, a keg makes beverages; processing time is in game minutes and sleeping changes the clock. | [Preserves Jar](https://stardewvalleywiki.com/Preserves_jar), [Keg](https://stardewvalleywiki.com/Keg) | Quality tiers were not reached. Harvest Moon: *not reached.* |
| Cooking/alchemy: Overcooked | A dish is picked up, prepared at a station (chop), cooked where required, plated and delivered; plates must be washed. Onion soup is three chopped, cooked onions. | [Overcooked wiki: 1-1](https://overcooked.fandom.com/wiki/1-1_(Overcooked!)) | Cooking Mama: *not reached.* |
| Cooking/alchemy: Potion Craft | Potions are built from ingredients with effect levels; a recipe lists effect levels. | [Steam thread](https://steamcommunity.com/app/1210320/discussions/0/3716062978744238749) | Player post; no wiki reached. |
| Discovery: Little Alchemy, Doodle God | Combine two known elements; results become known; tiered; completion needs every reaction. | [Doodle God guide](https://ayumilove.net/doodle-god-guide/) | Fan list. |
| RPG: World of Warcraft | Crafted gear has five quality tiers, other crafted items and gathered reagents three; reagent tiers combine at 5 to 1; higher skill makes higher output tiers likelier. | [Crafting reagent](https://warcraft.wiki.gg/wiki/Crafting_reagent), [Blizzard: professions](https://worldofwarcraft.com/news/23876529) | Dragonflight-era; changes by expansion. |
| RPG: Skyrim | Alchemy combines ingredients that share an effect; more ingredients make a match likelier; fortify potions boost smithing. | [Creation Kit: Ingredient](https://ck.uesp.net/wiki/Ingredient), [Gamer Guides](https://gamerguides.com/skyrim/guide/character-creation/skills/alchemy) | Guides disagree on caps. |
| RPG: Path of Exile | Item rarity (normal, magic, rare, unique) limits modifiers; currency items act on items (upgrade rarity, add, reroll, remove, scour modifiers). | [Official forum guide](https://pathofexile.com/forum/view-thread/2580682) | Thread partly cut off; effects not checked against the official wiki. Diablo: *not reached.* |
| 4X: Civilization | Tile resources are strategic (needed for certain units and buildings), luxury (amenities, one benefit per type) or bonus (yield). | [Civilization wiki: Resource](https://civilization.fandom.com/wiki/Resource), [Civ VI](https://civ6.fandom.com/wiki/Resource) | Community wiki. |
| 4X: Stellaris | Energy credits, minerals and food are basic; alloys and consumer goods are made from minerals; strategic resources exist. | [Economy guide](https://gamerempire.net/stellaris-economy-guide/) | Secondary. Endless Space: *not reached.* |
| Industry: EVE Online | Blueprints carry material efficiency (less material) and time efficiency (less time); originals and copies; invention makes higher-tier blueprints; build times scale with the blueprint's rank. | [EVE Industry](https://www.eveonline.com/article/eve-industry-all-you-want-to-know), [Researching the Future](https://eveonline.com/article/researching-the-future) | Developer posts of the Crius era; may have changed. |
| Military: Hearts of Iron | Equipment needs several raw materials (steel for most; oil, rubber, aluminium, chromium, tungsten for specific equipment). | [Paradox forum](https://forum.paradoxplaza.com/forum/threads/hearts-of-iron-iv-player-resources.1576080) | Forum and mod pages. C&C: *not reached.* |
| Trading: Elite Dangerous | Stations produce and consume commodities by economy type; supply and demand move buy and sell prices; prices relative to a galactic average; rare commodities gain value with distance (to 200 ly). | [Commodities](https://elite-dangerous.fandom.com/wiki/Commodities), [PC Gamer](https://www.pcgamer.com/uk/elite-dangerouss-trading-systems-detailed-by-frontier) | X4 and Patrician: station modules by function reached only through forum posts; Patrician *not reached.* |

### 1.2 Standards worth adopting

| Standard | Documented | Decision |
|---|---|---|
| UCUM | Case-sensitive code system for units, maintained by Regenstrief; aims at unambiguous electronic communication of quantities with units; 7-bit ASCII representation ([HL7 terminology](https://terminology.hl7.org/CodeSystem-v3-ucum.json.html)). The official specification text was not reached. | **Adopt for resource units.** A resource's unit is a UCUM code; the validator holds a closed table of the codes it supports (kg, g, t, L, m3, J, kJ, MJ, W.h, kW.h, s, min, h, d, By, plus braces annotations such as `{each}`, `{person}`, `{coin}`). |
| ISO 4217 | Alphabetic (3 letters) and numeric (3 digits) currency codes and a minor-unit exponent ([ISO](https://iso.org/iso-4217-currency-codes.html)). Table contents were not fetched. | **Adapt.** A real currency resource may name its ISO 4217 alphabetic code; a fictional currency says so. No table of real codes is shipped; the validator checks only the three-letter shape. |
| UN CPC 2.1 | Five levels: 10 sections, 71 divisions, 329 groups, 1,299 classes, 2,887 subclasses; section 0 agriculture, 1 ores and minerals, electricity, gas and water, 2 food, beverages, textiles, 3 other transportable goods ([UNSD](https://unstats.un.org/unsd/classifications/Family/Detail/1074)). | **Adapt as an optional tag, not a hierarchy.** A resource may carry a `cpc` section digit as a tag so packs can be grouped like real-world products. No copied tables. |
| UNSPSC, GS1 | *Not researched.* | Not adopted. |

### 1.3 Taxonomy extracted

Resource classes (✔ documented in a cited game; ○ our inference to cover a gap):

| Class | Evidence |
|---|---|
| raw (natural) | ✔ Minecraft ores, Stellaris minerals, Civ bonus resources |
| intermediate | ✔ Cities Skylines specialised-industry products, Factorio intermediates (inferred from recipes) |
| product / end good | ✔ Cities Skylines "goods for commerce", Stellaris consumer goods |
| consumable | ✔ Anno needs, Banished food |
| currency | ✔ Stellaris energy credits, Elite credits; ISO 4217 for real ones |
| energy / fuel | ✔ Factorio fuel value and categories |
| fluid, gas | ✔ Factorio fluid recipes need their own category |
| information | ○ research and data in tycoon and 4X games |
| labor / time | ✔ RimWorld pawn skill and work, EVE build time |
| population | ✔ Anno resident tiers |
| tool / equipment | ✔ Valheim and Don't Starve tools and stations; Factorio machines |
| quality / grade / rarity | ✔ WoW five and three tiers, Path of Exile rarity |
| durability / tool wear | ✔ Skyrim and Factorio tool durability (Factorio's lives on the tool page, not reached) |
| perishable | ✔ Factorio `spoil_ticks`/`spoil_result` |
| stackable / non-stackable | ✔ Factorio `stack_size` |
| unique / instance | ✔ Path of Exile unique items |

Recipe and process classes:

| Class | Evidence |
|---|---|
| transform, smelt, refine (with byproducts) | ✔ Minecraft furnace family; Factorio multiple results |
| craft, assemble | ✔ Minecraft shaped/shapeless, Satisfactory machines |
| combine / discover | ✔ Little Alchemy, Doodle God |
| disassemble / recycle | ○ inverse of craft; not found documented in the reached pages |
| grow, breed, harvest, extract/mine | ○ implied by raw sources; Stardew crops and artisan chains (processing only) |
| brew, cook | ✔ Minecraft brewing and campfire, Stardew keg, Overcooked |
| trade / convert | ✔ Elite markets, Banished trading posts |
| upgrade / enchant | ✔ Minecraft smithing, Path of Exile currency |
| research | ✔ RimWorld, Factorio technologies |
| repair | ○ |
| construct / build costs | ✔ Valheim station upgrade pieces, tycoon build costs |
| upkeep, consume / need | ✔ Anno needs, tycoon upkeep |
| decay | ✔ Factorio spoil |
| random yield, loot, chance | ✔ Path of Exile modifiers; Factorio forum threads on probabilistic products |
| catalysts and tools | ✔ Factorio (secondary), Valheim/Don't Starve tools |
| station / environment | ✔ Factorio crafting categories, Valheim station levels, Dwarf Fortress buildings |
| skill / level gate | ✔ RimWorld bills, DF skill token, WoW skill, Valheim station level |
| duration | ✔ Minecraft cooking times, Stardew minutes, EVE time efficiency |
| energy / power | ✔ Factorio machine energy usage. Our choice: energy cost is an input of an energy-class resource, so one mechanism covers fuel and power draw |
| quality outcomes | ✔ WoW tiered outputs by skill |

### 1.4 Bad patterns to avoid (carried over from the earlier discovery research)

- A recipe held in two tables that drift (Factorio recipe `enabled` plus technology list; secondary report): **one owner per recipe**.
- An unlock flag nothing enforces (Minecraft recipe book without `doLimitedCrafting`): a pack declares gates, the consumer enforces them.
- Free-resource loops (barter arbitrage, a recycle recipe that returns more than it takes): the validator checks conservation (see criteria).
- Material "stuff" categories expanded ad hoc per game (RimWorld ingredient filters): the standard has explicit `tags` and recipes name resources, not filters; filters are a consumer concern.

## 2. Ownership: how this relates to what exists

| Existing owner | Owns | Relation |
|---|---|---|
| SimEngine | Runtime resource, unit, transformation, technology and tycoon content and the rules that run it | The catalog is an **authoring source**. A SimEngine converter generates SimEngine documents from a pack; those are generated artifacts. A game that authors SimEngine content by hand keeps doing so; the earlier `SimRecipeSource` reads such content into alchemy and is unchanged. |
| AlchemyEngine | Discovery rules and the book | The Alchemy converter generates alchemy content from a pack. |
| WorldCreator | World/structure/settlement metadata and its catalogs (`WorldCreator.Catalogs.Modern`) | **Not a resource owner.** Searched `WorldMetadataContracts` and the modern catalog for resources and recipes: none. No overlap and no change to WorldCreator (another session is editing its tests). |
| `schemas/` and `docs/schemas/` | Repo-level JSON Schemas (`gameframework-project`, `gameframework-setup-state`, `mockup-capture`) | **Extended**: the catalog's schemas go in `schemas/catalog/` beside them. |
| `ScenarioEngine.Content.JsonSchemaContractValidator` (JsonSchema.Net) | Schema evaluation with no external references | **Reused** for structural validation. No second schema evaluator. |

## 3. Coverage today (before this work)

Read from the repo; "generated" is what the new converters will produce.

| Feature | SimEngine | AlchemyEngine | Neither |
|---|---|---|---|
| Resources with unit, category, phase, traits, capacity | ✔ `Content/Resources.cs` | elements with properties | |
| Stock, ports, endpoints, reservations, transfers, flows | ✔ `Standard/Inventory*`, `Resource*` | | |
| Batch transformation with inputs, outputs, tools, partial tool consumption, duration, ports | ✔ `Standard/Transformations.cs` | inputs/catalysts/outputs, no amounts | |
| Continuous transformation (rates) | ✔ | | |
| Entity output, output quality axes, resource lots | ✔ `Content/ResourceOutputQualities.cs`, `ResourceLots.cs` | | |
| Cash cost and income, purchasing, sales, upkeep costs | ✔ `Standard/Tycoon/*` | | |
| Research and unlocks | ✔ `Standard/Technology*.cs` | recipe discovery | |
| Construction costs and placement | ✔ `Standard/Construction*.cs` | | |
| Equipment condition and wear | ✔ `Standard/EquipmentCondition.cs`, `ConditionWear.cs` | | |
| Game conditions (readiness gates) | ✔ `Standard/GameConditions.cs` | | |
| Order-dependent combination, environment tags, recipe-level chance | | ✔ | |
| Discovery of recipes by trying | | ✔ | |
| Random yield and loot tables | no recipe-level RNG found | recipe-level chance only | **gap** for per-output chance and ranges |
| Skill levels and skill gates | no skill model found (worker roles exist) | | **gap** |
| Resource spoilage over time | none found | | **gap** |
| Station levels (upgradable stations) | | | **gap** |
| Disassemble link (reverse recipe) | expressible as a second transformation | | catalog-level only |
| Population tiers and needs | workers exist; no needs model | | **gap** |
| Price discovery by supply and demand | sales and suppliers; no market model | | **gap** |

## 4. Acceptance criteria (fixed before coding)

| # | Criterion | Evidence required |
|---|---|---|
| C1 | **One standard, strict.** One versioned JSON format for packs, resources, recipes, stations and skills, defined by JSON Schema files in `schemas/catalog/`. Unknown fields and dangling references are refused; no decision has a default (absence is never meaning). | Schema files; tests that an unknown field, a missing required field and a dangling reference are each refused with their own code. |
| C2 | **One owner per fact.** No second catalog beside an existing owner; SimEngine and Alchemy content from a pack are generated, not hand-copied; schema evaluation reuses the existing validator. | Section 2; the new projects reference the existing validator; no WorldCreator change. |
| C3 | **Standards used where they fit.** Units are UCUM codes from a closed supported table; currency may name an ISO 4217 code; ids follow a namespace rule enforced by the validator. | Tests for an unsupported unit, a bad currency code shape, an id outside its pack's namespace. |
| C4 | **Semantic validation with codes.** Duplicate ids, dangling references, id namespace, unit mismatches (discrete resources with fractional amounts, time and energy fields with wrong dimensions), unreachable crafted resources, ambiguous `combine` recipes, conservation and free-resource cycles, tool and catalyst conflicts, bad chance and range values. | One test per code. |
| C5 | **Free-resource cycles are caught.** A recipe of a non-generative kind may not produce more of a resource than it consumes of it; a loop of single-input single-output recipes whose ratios multiply to more than 1 is refused. | Tests with a seeded profitable loop and a seeded over-yielding recipe. |
| C6 | **Alchemy converter with a stated capability level per feature.** Each feature is *supported*, *reduced* (reported in the conversion report, never silent) or *refused* with a code. Excluded recipes carry codes. | Conversion report test; capability matrix in the plan entry. |
| C7 | **SimEngine converter loads through SimEngine's own readers.** Generated content composes in a SimEngine world for the features the engine can express; the rest are refused with codes. | A test that composes generated content for every pack that has expressible recipes; per-pack list of what is refused. |
| C8 | **Original, generic packs.** Eleven genre packs plus a shared `core` pack, dozens of resources and recipes each, coherent and validated; no trademarked names and no copied tables; provenance and license stated in each manifest. | Packs on disk; validator accepts every `pack.json` provenance `original`. |
| C9 | **Every pack validates, round-trips deterministically and has no free-resource cycle.** | Tests over all shipped packs; serialize, load, serialize gives identical bytes. |
| C10 | **Every pack converts to Alchemy.** | Test over all packs; report counts. |
| C11 | **Dynamic at run time.** A headless run selects a pack by id from JSON on disk with no recompile. | `--pack` run output; test. |
| C12 | **The validator's rules are mutation-checked.** | Breaking one rule makes its test fail; restored. |
| C13 | **Honest boundary.** The plan entry holds a capability matrix (feature by Alchemy by SimEngine by gap), what is unrun, and how to add a genre. | The nineteenth-pass entry. |

## 5. The standard as built ("gf-catalog format 1")

- **Layout.** `src/GameContent/Packs/<pack-id>/pack.json` (manifest: id, semver version, title, genres, caret-semver dependencies, license with `provenance: original`, document list) plus the documents it names (`resources`, `stations`, `skills`, `recipes` arrays). Schemas: `schemas/catalog/gameframework-catalog-pack.schema.json` and `...-document.schema.json`, evaluated by the existing `ScenarioEngine.Content.JsonSchemaContractValidator`.
- **Strict.** Every field is required (explicit `null` for none); unknown fields are refused; ids are `<pack>.<slug>` and only visible through declared dependencies.
- **Resources:** class, UCUM unit from a closed table, origin natural|crafted, stack, grade, perishable, durability, currency (ISO 4217 or fictional), CPC digit, tags, properties.
- **Recipes:** kind (21 kinds), inputs, catalysts, tools with uses, outputs (fixed or min/max), byproducts, chance drops, station + level, skill + level, environment tags, duration, quality rule, reverse recipe.
- **Code.** `GameContent.Catalog` (loader, validator, canonical writer), `GameContent.Catalog.Alchemy`, `GameContent.Catalog.Sim`, `GameContent.Cli` (`list|validate|alchemy|sim`), `GameContent.Tests`.
- **Validator codes** (each has a refusal test): schema, file missing, not json, pack id, document pack, dependency missing/version/cycle, duplicate id, id namespace, unknown ref, not visible, duplicate ingredient, unknown unit, unit/class mismatch, discrete unit with a fraction, currency, stack, durability, perishable, origin, bad amount, bad chance, no inputs, no outputs, tool conflict, station level, skill level, reverse, quality, not conserved, free cycle, unreachable, ambiguous combine.

## 6. Capability matrix (feature by engine)

| Feature | AlchemyEngine | SimEngine | Gap |
|---|---|---|---|
| Inputs to outputs | Yes, as a combination of kinds | Yes, transformation | Alchemy has no amounts |
| Amounts, ratios | Reduced (note `AMOUNTS_NOT_MODELLED`) | Yes, exact, unit-converted | Alchemy is kind-only |
| Tools, catalysts | Catalysts (reduced) | Held tools; wear as `tool_consumption` 1/uses | none |
| Byproducts | Extra outputs (reduced) | Ordinary outputs | none |
| Stations | Environment tag (reduced) | A processor entity per station; level 1 only | Station levels above 1 refused in Sim |
| Skills | Environment tag (reduced) | Refused (`SKILL`) | SimEngine has no skill gate on a transformation |
| Environment tags | Native | Refused (`ENVIRONMENT`) | none in Sim |
| Chance drops | Refused | Refused | Neither has per-output chance |
| Amount ranges | Reduced to max | Refused | no ranged output |
| Quality/grade | Reduced | Refused / reduced | no quality model |
| Spoilage | Not carried | Reduced (note) | no decay model |
| Duration | Reduced | Yes; instant refused | none |
| No-input (extract/harvest) | Refused | Refused (`NO_INPUTS`) | needs sources, not recipes |
| No-output (consume/upkeep) | Refused | Refused | needs needs/sinks |
| Ambiguous combination | First by id kept, rest refused | n/a | Alchemy keys recipes by input set |

## 7. Results against C1 to C13 (first pass; sections 9 and 10 give the second pass and today's 28 packs)

All met except as noted. C1 to C5: schemas plus 34 validator codes, each refused in a test (`ValidatorTests`); two mutations of the balance rules each failed a test and were restored. C6/C7: `ConverterTests` pin every refusal and reduction; every shipped pack composes through SimEngine's own readers. C8/C9/C10: 12 packs (core plus 11 genres, 17 to 80 own resources and 20 to 80 own recipes each) validate, round-trip to identical bytes, have no free cycle and no unreachable resource, and convert to Alchemy. C11: `--pack <id> [--packs <dir>]` on the headless alchemy game; a test plays a pack written to disk during the test with no rebuild. C12: done. C13: the nineteenth-pass entry.

Alchemy conversion of a pack keeps 13 to 36 of its recipes: the rest are refused with a code (mostly extraction/consumption recipes and recipes that share a combination of kinds), which also removes elements nothing remaining can make.

## 8. Coverage boundary and adding a genre

"All possible" recipes and resources is unbounded; this work covers the eleven genres above plus a core. Not reached: see the list in section 1.1. To add a genre: create `Packs/<id>/` with a manifest depending on `core`, write resources and recipes in the standard, run `GameContent.Cli validate Packs <id>`, fix the codes, then `alchemy` and `sim` to see what converts; the shipped-pack tests pick up the folder automatically (the genre count assertion in `ShippedPackTests` is the one list to update).

## 9. Second pass audit (2026-10-27)

**Method.** Re-read the section 8 limits, grepped every field of the standard for a reader (accepted-then-ignored), listed every code and looked for tests and raisers, ran every pack through the three tools, and compared names across packs.

**Found, with what was done.**

| # | Finding | Fix |
|---|---|---|
| 1 | Alchemy refused 166 gathering, 19 consumption and 2 drop notes, plus ambiguity and unreachable cascades (13 to 36 recipes kept per pack) | AlchemyEngine accepts catalyst-only and consume-only reactions; converter turns gathering into tool use (or the generated `world.hand`), lone drops into the recipe chance, other drops into separate reactions, alternates into `method-` tagged reactions, spoilage into a `time-passes` reaction. **0 refusals in all 19 packs.** |
| 2 | SimEngine refused quality, station levels and no-input recipes | quality maps to `resource_qualities`/`output_qualities` (inherit-input, floor); station levels become one entity per level; extraction runs. Still refused, with the exact engine requirement in the matrix |
| 3 | No balance checks beyond quantity loops | warnings: `VALUE_LOOP`, `VALUE_OUTLIER`, `DURATION_OUTLIER`, `ENERGY_OUTLIER`, `CHOKEPOINT`; `stats`; each mutation-checked. They found a pickaxe chokepoint, a 2x labor trade, an unbalanced ride and slipped durations, all fixed |
| 4 | Duplicate resources across packs (`bread`, `flour`, `spoiled`, `grain`, `blanket`, a `Cooking` skill, a `Lab` station) | shared ones moved to `core`; library rule `DUPLICATE_NAME/SLUG` over resources, stations and skills |
| 5 | `Provenance` code declared and never raised; `Origin` code never raised | `Provenance` removed (the schema constant reports it); `Origin` implemented (a manufacturing recipe may not make a natural resource), which found 9 wrong origins |
| 6 | `cpc` and `stack`/`durability`/`currency`/`grade` read by no converter | `cpc` becomes an Alchemy property and a Sim trait; Alchemy states the rest in one pack-level note |
| 7 | Names only in the source language | optional locale documents per language, keyed by id, checked against the pack's own ids; `CatalogText.NameOf` has no fallback (decision: Factorio/Minecraft/Godot all key translations by id; a missing text is reported, never guessed) |
| 8 | Docs claimed "each code tested" with no guard | `AuditGuardTests`: every code is named by a test and raised by library code; schema property names equal the writer's |
| 9 | Prices in two spellings (`list-price`, `base-price`) and a value store too small for 30 kW.h | one reserved `base-price`; store raised and covered by a test |
| 10 | CLI had no stats, no whole-library check, no scaffold, no `--notes` | added `stats`, `validate-all`, `new-pack`, `--notes`; error paths tested |
| 11 | Two threads validating schemas at once let an invalid document through once | schema evaluation serialized |

**Still open.** Packs are not play-tested for balance beyond the warnings. `properties` other than `base-price` are pass-through game data by design. Skill gates, random yield, spoilage and environment gates in SimEngine need engine features (section 6); none was added.

## 10. Equipment, vehicles and eras

### 10.1 Research (searched 2026-10-27; facts are what the source states, inferences are marked)

| Source | Documented | Our inference |
|---|---|---|
| [Three-age system](https://en.wikipedia.org/wiki/Metal_Age) (Thomsen, 1816 to 1825; later Palaeolithic/Mesolithic/Neolithic and a Copper Age) | Prehistory ordered by dominant tool material: stone, bronze, iron; regional, criticised as determinist | Period labels are an ordered vocabulary, not dates; a world declares whether its eras are real years |
| [Civilization VI eras](https://www.fandom.com/articles/breaking-civilization-vi-tech-tree) | Ancient, Classical, Medieval, Renaissance, Industrial, Modern, Atomic, Information; community-recalled start years | Eras have an order, a tier and optional years; our labels are the shared subset |
| [Age of Empires II](https://www.ageofempires.com/learn-to-play/advancing-aoe2/) | Four ages, advanced by building prerequisites plus a resource cost; each age unlocks buildings and units | An era gate is an availability entry plus ordinary recipes (the advance cost is a recipe) |
| [Hearts of Iron IV tank designer](https://eip.gg/hoi4/guides/best-tank-designs/) | Designs from up to 9 modules; stats such as speed, armor, piercing, reliability; cost and supply weight | Stats are a vocabulary with units, per class; reliability and supply weight fit as stats |
| [Valheim weapon pages](https://gamewith.net/valheim/article/show/24756) | Bronze, iron, silver tiers; damage type, knockback, weight, durability, forge level; sources disagree on some numbers | Damage type is an enum stat; durability stays a resource field; forge level is the recipe's station level |
| [Warship classification](https://cimsec.org/what-do-you-call-it-the-politics-and-practicalities-of-warship-classification/) | Cruiser, destroyer, frigate, corvette overlap and change meaning over time | Vehicle class is coarse (land, sea, air, space); the name of a design class is a tag, not a rule |

Not reached: Stellaris and Endless tech tiers, aircraft designation systems, Kenshi (searches returned nothing usable). Real references for weapon types (sword, longbow, musket, rifle) are common knowledge and used generically.

### 10.2 Decisions

- **One standard.** Equipment is an `item` description attached to a resource (identity, cost via recipes, durability and availability stay the resource's and recipes' own). It is built by ordinary recipes, so ingredients, stations, skills and era gates reuse the recipe machinery.
- **Eras** are data: `order` (unique across the library), `tier`, a closed period label, and years only in a historical world. A pack declares `world`: timeless (no eras), historical (years, stops at the present) or fictional.
- **Availability** is a table `{id, from, until}`; a thing with no entry is timeless. A pack may place a thing of a dependency in an era, so the `ages` pack can date the core materials.
- **Rules.** Recipe may not start before an ingredient, tool or station exists or after it is gone; may not make a thing before it exists; a thing cannot exist before any recipe could make it; a replacement may not appear in an earlier era than what it replaces; historical worlds have no spacecraft and no future labels.
- **Stats** are a vocabulary (`stats`): dimension, unit from a closed table (a unit must belong to its dimension), number/integer/enum, range, item classes it applies to. Item stat values are checked against it.
- **Layer boundary.** SimEngine has vehicle content (mover, storage, transporter) and equipment condition, and no combat or weapon system. Weapon and armor stats stay read-only data. A separate CombatEngine would consume the stat blocks; it is not built.

### 10.3 What was built

**The standard (format 1, still one standard).**
- Manifest `world`: `timeless` (no eras or availability), `historical` (eras carry real years, labels stop at `modern`, no spacecraft) or `fictional`.
- Documents may carry `eras`, `availability`, `stats` and `items`; schemas, loader, canonical writer and 15 new codes (`ERA_ORDER`, `ERA_SPAN`, `ERA_WORLD`, `ERA_WINDOW`, `ERA_ANACHRONISM`, `ERA_LINEAGE`, `AVAILABILITY_DUPLICATE`, `STAT_UNIT`, `STAT_SHAPE`, `ITEM_RESOURCE`, `ITEM_STAT`, `ITEM_LINK`, `ITEM_LINEAGE`, `ITEM_NO_BUILD`, `ITEM_WORLD`).
- An item is the equipment side of a resource: class (weapon, armor, tool, vehicle-land/sea/air/space, mount, structure-unit), stats from the pack's vocabulary, slots, cargo (mass, volume, count, population), crew (skill, level, count), fuel (resource, capacity, burn per hour), ammunition, upkeep per day and the item it replaces. Cost, durability, availability and how it is made stay the resource's and recipes' own, so a build needs ordinary recipes with ordinary stations and skills (`ITEM_NO_BUILD` refuses an item nothing manufactures).
- Stat vocabulary is data (`gear` pack: damage, damage type as an enum, range, rate of fire, armor rating, weight, speed, operating range, ceiling, reliability, efficiency). A stat names a dimension and a unit from a closed table; a unit outside its dimension is refused (`STAT_UNIT`), a value outside the stat's range, kind or item class is refused (`ITEM_STAT`).
- Anachronism rules (all mutation-checked on the ingredient, made-before-exists, lineage and unit rules): a recipe cannot start before an ingredient, tool or station exists or after it is gone; cannot make a thing before it exists; a thing cannot exist before any recipe could make it; a replacement cannot appear in an earlier era than what it replaces.

**Content (28 packs now).** `gear` (vocabulary, 11 stats, 6 crew skills), `matter` (45 materials and 60 recipes: flint, hide, arrows, powder, cartridges, engines, titanium, ceramics, superconductors, fusion cores, mithril), `ages` (8 historical eras, Stone Age to modern age, with years; dates 33 existing materials and recipes), `horizons` (near and far future), and five equipment packs with **146 items and 99 replacement links**:

| Pack | World | Items | Eras |
|---|---|---|---|
| arms-ancient | historical | 49 | stone 13, bronze 13, iron 12, classical 11 (weapons, armor, tools, boats, carts, chariots, mounts, catapults) |
| arms-medieval | historical | 27 | medieval 14, gunpowder 13 (swords, longbow, plate, trebuchet, cannon, cog, galleon, ship of the line) |
| arms-modern | historical | 31 | industrial 13, modern 18 (rifles, locomotive, ironclad, airship, tank, destroyer, submarine, jet, helicopter) |
| arms-future | fictional | 25 | near future 12, far future 13 (railgun, exo-frame, hover truck, orbital lifter, starfighter, colony ship) |
| arms-fantasy | fictional | 14 | medieval 14 (mithril, runes, dragon-scale, griffin, wyvern, sky galleon, golem) |

The build recipes use the existing material packs (core, mining, naval, electronics, military and space stations, rpg stations); the validator checks every material is available no later than the recipe. The deepest replacement line is six items.

**Converters (capability matrix additions).**

| Feature | AlchemyEngine | SimEngine | Gap |
|---|---|---|---|
| Item class | property `item-<class>` | vehicles become mover entities (class land, sea, air, space; speed from the stat whose dimension is speed; 1 tile = 1 km); other classes stay catalog data | no combat, equipment-stat or crew system in SimEngine |
| Stats, slots, cargo, crew, fuel, ammunition, upkeep | one pack-level note | one note for vehicles (`VEHICLE_STATS_NOT_MODELLED`) and one for the rest (`NO_COMBAT_ENGINE`) | a CombatEngine would consume the stat blocks |
| Era availability | environment tag `era-<era>` on the recipe | one note (`ERA_NOT_MODELLED`); SimEngine's mechanism is a technology unlock, which gates player orders only | no engine concept of eras |
| Station level | environment tag | one entity per level | none |

**Layer boundary.** Combat is a separate layer: weapon, armor and vehicle stats are the contract it would read (damage and damage type against armor rating, range, rate of fire, ammunition links, crew skills). SimEngine and AlchemyEngine do not fake it. It is not built.

**Coverage boundary.** Eras: stone to far future, plus a fantasy variant on the medieval eras. Not covered: era systems of other cultures, intermediate ages (Copper, Renaissance, Atomic), unit rosters (infantry types with training), economies of ownership and upkeep beyond the per-day list, space habitats and stations as items, many ammunition types. The method to add one is: add an era to `ages` or `horizons` (order, tier, label), add `availability` rows for what appears, add items with stats from the `gear` vocabulary (or new stats) and build recipes; `validate-all` then checks the dates.

### 10.4 Numbers

28 packs; 322 `GameContent.Tests`; 136 `GameRuntime.Tests`; solution 0 warnings and 0 errors. Every pack validates with no warnings, converts to Alchemy with no refusal and (except the `gear` vocabulary, which has nothing to convert) composes into a SimEngine world through its own readers; every pack but `gear` is playable by `--pack` in the headless alchemy game.

## 11. The Economy converter and recipe ownership (pass 46, 2026-10-29)

**Decision (read against sections 2 and 6 in full, `docs/ALCHEMY_ENGINE.md` and the SimEngine transformation code).** A recipe is authored **once**, in the catalog. AlchemyEngine cannot carry production: its recipe has kinds only, no amounts, rates, time or stations (matrix row "Amounts, ratios": reduced, `AMOUNTS_NOT_MODELLED`). SimEngine already runs a recipe as a per-entity transformation with exact amounts. The Economy now runs a recipe as **market-level production**. The three layers have three jobs: Alchemy discovers, SimEngine runs it per entity, the Economy runs it per market. A game runs a given recipe in **one** of the last two: `RecipeOwnership` (below) says which, and refuses a game where both would run it.

**Project.** `GameContent.Catalog.Economy` (same place and pattern as the Alchemy and Sim converters; in `GameFrameWork.slnx`): `CatalogToEconomy.Convert(set, options)` returns an `EconomyConversion`: the `goods` and `recipes` arrays of an economy document (text), the ids of both, the names behind the localisation keys, and a `ConversionReport`. `Compose(world)` puts them into an economy world that supplies everything else (currencies, markets, lines; it must not have its own `goods` or `recipes`, which would be a second owner of the same fact). `GameContent.Cli economy <packs> <pack> --milli-per-price <n> --period-seconds <s> [--owners <file>] [--notes] [--out <dir>]`.

**What the game must state (no hidden default).** `EconomyConversionOptions`: the thousandths of a minor unit in one unit of a resource's `base-price`; how many seconds an economy period lasts (to turn a duration into periods); the price tuning of the goods (`GoodTuning`, which is game feel, not catalog data; `GoodTuning.Town` is the town sample's, picked explicitly); the recipes the document declares locked; optionally the ownership declaration.

**Capability matrix (Economy column; the Alchemy and Sim columns are in section 6).**

| Feature | Level | Economy | Code |
|---|---|---|---|
| Inputs to outputs, byproducts | supported | inputs and outputs of a run (a byproduct is an output; a good named twice is summed) | byproducts: reduced note `CATALOG_ECONOMY_BYPRODUCTS_AS_OUTPUTS` |
| Whole amounts | supported | integers 1 to 1,000,000 | |
| Fractional amounts | reduced | a run is the smallest batch of catalog runs (at most 1,000) that makes every amount whole; same duration | `CATALOG_ECONOMY_BATCH_SCALED`; refused when no batch of 1,000 does or an amount passes 1,000,000: `CATALOG_ECONOMY_PRECISION` |
| Duration | reduced | whole periods, rounded up (0 stays 0: the outputs join the stock at once); at most 64 | `CATALOG_ECONOMY_DURATION_ROUNDED`; refused over 64: `CATALOG_ECONOMY_DURATION_TOO_LONG` |
| Station | reduced | the market's production line for the recipe stands for it; its capacity is the throughput; level not modelled | `CATALOG_ECONOMY_STATION_AS_LINE` |
| Catalysts | supported | a catalyst that is a priced tradable good becomes a **requirement** of the recipe (Economy `requires`): the market must hold at least that amount in stock for the line to run, and it is never used up (the catalog's "kept"); the amount is not scaled by a batch; no note. **Since Economy format 6 (pass 57) a requirement is held by one line at a time within a period**: a line that runs holds the amount it requires and a later line of the same market (in the engine's recipe order) sees only the rest, so two recipes that keep one catalyst need the sum of their amounts in stock to run in the same period. The catalog does not say a kept item can be in two reactions at once, so the stricter reading is the honest one and loses nothing the catalog states; no note | fractional amount: reduced note `CATALOG_ECONOMY_REQUIREMENT_ROUNDED` (rounded up to a whole amount held) |
| Tools | supported, with wear (Economy format 5, pass 51; held by one line at a time, format 6, pass 57) | the tool is a requirement of 1 held in stock (**two recipes that need the same tool need two of it to run in one period; the first in the recipe order holds one, the next is held**, see `docs/ECONOMY_ENGINE.md` section 17) and **wears by its `uses`**: `wear: {units: batch, runs: uses}` ("each run wears 1/uses of one item", exactly the catalog's meaning; a batch of k catalog runs loses k items' worth in `uses` runs); when it is worn out the line is held until the market holds it again; no note, because nothing is lost. `CATALOG_ECONOMY_TOOL_WEAR_NOT_MODELLED` is **retired** | |
| A catalyst or tool that is not a priced tradable good (unique, currency, no `base-price`, price out of range) | reduced | the recipe runs without it | `CATALOG_ECONOMY_REQUIREMENT_NOT_A_GOOD` |
| A catalyst or tool the recipe also makes | reduced | a requirement on the recipe's own good cannot be modelled (a cycle of one); left out | `CATALOG_ECONOMY_REQUIREMENT_ON_OWN_GOOD` (a catalyst that is also an input cannot reach the converter: the validator refuses it with `CATALOG_TOOL_CONFLICT`) |
| A catalyst or tool made by the chain that needs it (a cycle through the requirement) | reduced | the **requirement** is given up, not the recipe, so both recipes are kept | `CATALOG_ECONOMY_REQUIREMENT_CYCLE` |
| Reverse link | reduced | not modelled | `CATALOG_ECONOMY_REVERSE_NOT_MODELLED` |
| Spoilage, grade, stack size | reduced | not modelled; the good's decay is the tuning's | `CATALOG_ECONOMY_RESOURCE_FIELDS_NOT_MODELLED` |
| A resource's own `durability.uses` (the item's total life) | reduced (pass 54, own code) | **not read, by decision**: the Economy wears a good only through the recipes that hold it as a tool, whose `uses` already says what fraction of one item a run wears ("each run wears 1/uses of one item"); the item's life is a separate count of the item's own use that no catalog field ties to a recipe. The note says what was lost: the life itself, and either that no kept recipe wears the good or which recipe `uses` do (document and save unchanged, no format bump) | `CATALOG_ECONOMY_DURABILITY_NOT_MAPPED` |
| More than 8 tags | reduced | the first 8 in order | `CATALOG_ECONOMY_TAGS_TRUNCATED` |
| Era availability | reduced | one pack-level note; the game unlocks the recipe when its era comes | `CATALOG_ECONOMY_ERA_NOT_MODELLED` |
| Chance drops | refused | production makes fixed amounts | `CATALOG_ECONOMY_RANDOM_YIELD` |
| Amount ranges | refused | | `CATALOG_ECONOMY_AMOUNT_RANGE` |
| Skill gate | refused | the Economy has no skills | `CATALOG_ECONOMY_SKILL_GATE` |
| Environment tags | refused | | `CATALOG_ECONOMY_ENVIRONMENT` |
| Quality | refused | | `CATALOG_ECONOMY_QUALITY` |
| No inputs (extract, harvest) | refused | what a market makes from nothing is the pair's own `production`: a second owner would be a duplicate | `CATALOG_ECONOMY_NO_INPUTS` |
| No outputs (consume, upkeep) | refused | | `CATALOG_ECONOMY_NO_OUTPUTS` |
| A resource that is not a tradable good (currency, information, labour, population, unique) | refused | | `CATALOG_ECONOMY_NOT_A_GOOD` |
| A good with no `base-price`, or one that is worth under 1,000 or over 1,000,000,000 milli | refused | a market cannot price it | `CATALOG_ECONOMY_NO_PRICE`, `CATALOG_ECONOMY_PRICE_RANGE` |
| More than 8 goods on a side, or more than 8 catalysts and tools together | refused | | `CATALOG_ECONOMY_TOO_MANY_GOODS` |
| Recipes that feed each other in a cycle | refused, one at a time | production runs over an acyclic graph in topological order; the validator allows cycles that conserve value (a craft and its disassembly), so the converter breaks them: each round the graph library finds one cycle and gives up its most readily given-up recipe (disassemble, decay, repair, upgrade, consume, trade, research, upkeep, then the rest), the highest id on a tie | `CATALOG_ECONOMY_CYCLE` |
| Owned by SimEngine, or no owner declared | refused | with a declaration, only the Economy's recipes are converted | `CATALOG_ECONOMY_OWNED_BY_SIM`, `CATALOG_ECONOMY_NO_OWNER` |

Every recipe of a pack is accounted for: a test over the 28 shipped packs asserts converted + refused = all recipes, that what is kept composes into an economy document EconomyEngine loads, and that converting twice gives the same text. Measured on this machine (`GameContent.Cli economy ... --milli-per-price 10000 --period-seconds 3600`; a pack is converted with its dependencies): `core` keeps 9 recipes (29 refused), `factory` 9 (56), `mining` 16 (52), `kitchen` 11 (49), `matter` 61 (113). Most refusals are honest: no price in the pack (813 notes over the library, so the packs with prices are the usable ones), no inputs, not a good. Recipes without a `base-price` on a resource are the largest gap: the packs were not authored for market pricing.

**Ownership.** `RecipeOwnership` (in `GameContent.Catalog`; the catalog owns recipes, so it owns who runs them): strict JSON `{ "format": 1, "owners": { "pack.recipe": "sim" | "economy" } }`; `Check(set)` refuses a recipe the pack does not have (`CATALOG_OWNER_UNKNOWN_RECIPE`); `CheckConversions(simRecipes, economyRecipes)` refuses any id in both (`CATALOG_OWNER_DOUBLE`). `CatalogToSim.Convert(set, ownership)` and the Economy converter leave out what the other layer owns (`CATALOG_SIM_OWNED_BY_ECONOMY`, `CATALOG_ECONOMY_OWNED_BY_SIM`) and what the declaration does not mention (`CATALOG_SIM_NO_OWNER`, `CATALOG_ECONOMY_NO_OWNER`): there is no default owner. At assembly time `EconomyRecipeLayers.Check(economySet, simResolvedContent)` (bridge project) reads the transformation ids of the composed SimEngine world and the economy's recipe ids and asks `CheckConversions`, refusing with `ECON_RECIPE_DOUBLE_OWNER`; one owner of the rule. Tests: both conversions of one pack without a declaration list the same recipes and `CheckConversions` fails on them; with a declaration each layer takes its own and the check passes; a composed SimEngine world plus the economy document of the same pack is refused, and accepted once split.

**Discovery.** Alchemy stays the discovery layer. Recipe ids are the catalog's in all three layers, so a discovered alchemy recipe names the economy recipe directly. The unlocked set is **runtime state in the Economy** (saved, digest-covered, replayed through its request), and the document only declares which recipes are `locked`; see `docs/ECONOMY_ENGINE.md` section 13.3 for why that, and not a set in the document, and `EconomyRecipeUnlocks` (bridge) for the event wiring.
