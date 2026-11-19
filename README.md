# GameFrameWork

WorldCreator is a reusable C# world generator for game developers. Games supply
their own catalogs, recipes, purposes, constraints and spatial requirements.
The engine expands nested content, calculates supported layouts and connections,
validates the declared scope, and publishes inspectable world data. Different
parts of one world can use independent coordinate domains or geometry-free topology.

The **supported metadata engine core is technically qualified** against the
original S01–S10 plan. It includes terrain/environment layers, terrain-aware
connections, region generation, nested source-sized content and surroundings,
generated interiors, explicit inter-domain journeys, protected source edits and
checkpoint replay. The final qualification retains all 40 original requests:
39 complete declared worlds and one intended terrain refusal. A separate complete
composition retains 91 nodes and 86 physical members; scale is measured through
577 nodes. [Evidence and limits](docs/evidence/2026-11-02-s10-core-gate-verification.json).

Developers still author their game's catalogs, purposes, rules and finite search
domains. This is not automatic design for every genre or arbitrary-scale
generation. Human approval of a particular game's world, live-agent behavior,
production art and native runtime remain separate. Godot is deferred. The
[roadmap](docs/ROADMAP.md) and [master plan](docs/ADVANCED_WORLD_GENERATION_MASTER_PLAN.md)
retain those boundaries and the later visual stages.

## Start with the public C# API

The existing [runnable SDK workflow](examples/worldcreator-workflow/README.md)
references `WorldCreator.Core` directly. It demonstrates generation, inspection,
publication, editing and restore across wilderness, interior, compound and logical
programs, plus regional and composed-domain workflows. It uses the same public
APIs a consuming game uses; no AI process, artwork setup or Godot is required.

Use [catalogs and recipes](examples/metadata/README.md) to describe reusable
game-authored content. Choose physical geometry, terrain, access and traversal only
when the requested world needs those capabilities. The [metadata API](docs/WORLD_METADATA.md)
documents the current contracts and limits. JSON packets preserve engine output;
their JavaScript/SVG schematics inspect that output without solving new layouts.

The rejected city-first generator and its CLI commands are removed. A city is one
possible world program. Local grids are optional layout choices; regions, paths,
interiors and other domains do not inherit a mandatory street grid. Historical city
outputs are not current procedural-generation entry points.

## Source components

- `src/WorldCreator/`: world generation, catalogs, CLI and Godot export (`WorldCreator.*`).
- `src/ScenarioEngine/`: scenario lifecycle, content, persistence, replay and diagnostics (`ScenarioEngine.*`), copied from the SimpleRTS workspace.
- `src/SimEngine/`: simulation engine (`Beep.SimEngine`), copied from its standalone repository.

All three belong to GameFrameWork and are listed in `GameFrameWork.slnx`.
WorldCreator's SDK example builds against Core alone. SimEngine is a reusable
engine; OilFieldDays is its consumer. Engine integration and consumer migration
have separate verification gates in the [runtime integration plan](docs/RUNTIME_PIPELINE_INTEGRATION_PLAN.md).
The scoped WorldCreator checks below do not establish a passing aggregate solution
build or OilFieldDays integration.

## Working with Claude or Codex

For metadata generation or framework development, the agent follows the
[world-generator skill](.agents/skills/world-generator/SKILL.md), inspects current
inputs and authors reusable declarations for the C# engine. This route requires
neither a game name nor visual setup. Actual autonomous agent behavior still has
its own evaluation gate.

For a new game that requests managed artwork or visual engine integration, the
agent reuses the saved project settings and runs the applicable GameFrameWork and
canonical GameArts setup workflows. GameArts owns artwork production and exact
artifact approvals. Reference, district/mockup and production approvals remain
separate. Agents perform authorized setup operations themselves.

## Two-part product

1. **WorldCreator C# engine** supplies reusable generation, validation, source-edit
   and data-publication APIs, independently of an agent or renderer.
2. **Claude/Codex framework** helps author inputs and orchestrates planning,
   engineering, optional artwork and verification.

Projects and namespaces use `WorldCreator.*`: `Core`, `Worlds`, `Catalogs.Modern`,
`Godot` and `Cli`. GameFrameWork remains the repository and agent-framework name.
Godot integration is downstream and deferred for the current generator work.

## General catalog authoring and composition

Any requested structure can own constituent structures when the game program calls for them. The workflow inspects reusable entries, adds missing part definitions, authors repeated/nested parts and layout constraints, persists the catalog content, then generates through the appropriate current C# metadata or catalog-composition owner. A single-building archetype can be extended into a compound program; there is no fixed parent-type list in the authoring compiler.

All [197 Modern families have a composition review](docs/CATALOG_COMPOSITION_REVIEW.md): single structures, context-dependent sites, compound sites and infrastructure assemblies. Reviews distinguish buildings from grounds, infrastructure, equipment and interiors. Suggested parts are design guidance, not generated assets or universal requirements.

`CatalogContentAuthoring` loads/saves validated structure definitions and parameterized facility programs. Project content in `.gameframework/catalogs/*.json` reloads automatically; `--catalog-content <manifest>` loads an explicit file. Catalog changes have manifest digests and require explicit replacement IDs. The CLI's `save-catalog-content` validates before saving; agents perform this through the canonical workflow without making users edit JSON or run commands. Compatible source packs retain their policies; cross-pack composition uses explicit imports afterwards.

[Contracts and limits](docs/SETTLEMENT_IDENTITY_AND_BUILDING_RULES.md#general-catalog-content-authoring-from-requested-parts) describe the API. Existing club/factory programs are regression examples using the common compiler. Tests also exercise every frontage family and unrelated housing/education/care programs, new part definitions, persistence and fresh-process generation. Explicit equipment/site-object definitions and variants now compile into separate slots, owned physical objects and optional access routes, including nested object-only groups. `list-objects` keeps their inventory separate from building families. [Contracts and examples](docs/PROCEDURAL_CITY.md#catalog-equipment-and-site-objects-2026-10-26). Standalone `GroundArea` parts now use reusable area definitions/variants, explicit through-access, nested slots and the same `SiteAreas` output collection. `list-areas` separates their inventory. [Ground contracts/examples](docs/PROCEDURAL_CITY.md#reusable-standalone-grounds-2026-10-26). Interiors, infrastructure program parts, artwork approval, live-agent evaluations and playable Godot export remain separate gates.

The rejected procedural city production path is removed in full. Shared catalogs, nested composition and the authored mockup compiler remain separate current capabilities. Earlier city packages and examples are historical evidence; they cannot be regenerated with a retained legacy engine.

## Optional authored visual-world generation

The following mockup/compiler and Modern catalog capabilities are a separate
visual input route. They are not prerequisites for the procedural metadata APIs
above, and do not restore the retired procedural city engine.


The C# engine accepts a `WorldRequest` with the current `MockupWorldDesign` and explicit `Catalog` selection/capability flags: mockup/style identity, visual regions, paths, road profiles, settlement frontage rules, terrain rules, building catalog and explicit landscape/prop grammars. It computes positions and footprints; callers do not supply every repeated object's anchor.

Building scale follows the archetype and capacity context. The catalog now includes **197 building/structure families and 585 size variants in 15 categories**: residential, retail, food/hospitality, offices/finance, education/research, healthcare, civic/safety, culture/worship, sport/recreation, transport/logistics, industry/extraction, agriculture, utilities/communications, public-space structures and linear/water infrastructure. Custom definitions, capacities, dimensions and site rules are data, not new engine classes. A stadium cannot be shrunk into a house plot. Required size or road-access mismatches return diagnostics.

`list-buildings <catalog-id> [category]` returns the complete or category-filtered JSON inventory, including dimensions, capacity units, site rules and placement domains. Specialized families require explicit world-program/composition choices; adding a refinery or airport to the catalog does not make it appear in every settlement. The 187 frontage families use parcel/site planning; the 10 linear/water infrastructure families use dedicated bridge, tunnel and shoreline planning through `MockupWorldDesign.InfrastructureRules`. These profiles are configurable examples, not an exhaustive inventory, measured artwork or engineering standards. [Coverage and examples](docs/SETTLEMENT_IDENTITY_AND_BUILDING_RULES.md).

Site programs derive entrance walks, courts, loading yards, parking, gardens and green buffers within their parent parcels. Separate park zones and planting grammars distribute vegetation intentionally, with explicit species dimensions and clearance checks. No fallback fills vacant cells or relocates a conflict.

The existing inventory lives in the separate **Modern** pack. Shared provider/registry/policy/demand/infrastructure contracts accept independently registered C# packs with their own fixed code and vocabulary. Modern explicitly enables electricity, potable water, wastewater, motor roads, districts and technology policies. Missing catalog/siting context, unknown flags and disabled required capabilities reject generation; there is one current contract and no compatibility path. [Contract and host usage](docs/SETTLEMENT_IDENTITY_AND_BUILDING_RULES.md#host-and-cli-usage).

`list-catalogs` discovers registered packs; `list-buildings modern Healthcare` filters Modern content. Add `--catalog-plugin <trusted-assembly.dll>` to load an external compiled pack. `generate-world <request.json> [output-dir]` uses the same injected catalog contracts and shared operation reporting. Unsupported commands are rejected. [The CI input](tests/fixtures/synthetic-desert-world.json) is explicitly synthetic; it grants no GameArts approval. The current settlement grammar supports rectangular north/south frontage regions and diagonal/curved polyline roads, with continuous source geometry preserved beside the raster output. Full Godot resource compilation is a separate gate.

## Optional mockup capture

The shared source-pixel capture contract records road polylines, widths/levels/junctions, building roofs versus ground footprints, green/water regions, access points, uncertainty and incomplete coverage. The engine handoff applies reviewed calibrated geometry to the existing design JSON and retains source lineage; it does not require a city-specific C# class. A review viewer overlays annotations on the exact PNG and can toggle the original image.

[Capture, Codex prompts and atlas procedure](docs/GAMEARTS_INTEGRATION_CONTRACT.md) covers actual image references, topology-derived road kits, isolated component frames and the canonical GameArts deterministic atlas builder. `review-capture`, `compile-capture` and `capture-schema` are internal agent/CLI entry points. [Authored city capture example](.gameframework/worlds/modern-city/world-request.json) compiles 99 selected capture features, with authored ground calibration and 49 at-grade junction records resolving all 51 crossings. The actual engine generates 43 buildings, including a 180×140m stadium and 140×90m factory, plus 32 road paths and 44 source-anchored park trees. `generate-world` accepts `--capture` with `--asset-root` and exports a generated geometry map and source comparison. Artwork approval and Godot runtime remain separate gates.

## Audit a coordinated production package

```text
dotnet run --project src/WorldCreator/WorldCreator.Cli -c Release -- verify-world <world.json> <manifest-directory> <trusted-asset-root>
```

This audits the coordinated portable production-document format, not the design-only `WorldDocument` emitted by `generate-world`. The manifest directory contains one `<asset-id>.json` approved-art manifest per dependency. The command returns JSON with `Valid` and `Issues`, exits with 0 on integrity success or 1 on rejection/error, and records correlated log, audit and notification events under `.gameframework/operations` in the current directory. Ctrl+C requests cancellation (exit 130). A successful audit checks declared dependency revisions and file hashes; lifecycle approval, visual fidelity and gameplay readiness require their separate gates.

The repository's `global.json` requests .NET SDK 10.0.402 with `latestPatch`
roll-forward. Current focused checks run on installed SDK 10.0.403/runtime 10.0.14;
see the [current core qualification](docs/evidence/2026-11-01-core-qualification-verification.json).
This is not a claim that the full solution, every adapter or a game runtime was tested.

Development policy: only the existing `main` branch.
