---
name: gameframework-worldcreator
description: "Use GameFrameWork's WorldCreator to generate world metadata from game-defined catalogs and recipes: archetypes and parts, hierarchy and connections, regions, placement and access, networks and routes, interiors, services, progression, chunks and ground cover, with deterministic recipe/catalog digests and verification. Use when a game needs a generated world, terrain, settlements, roads or world metadata, or wants WorldCreator output inside a GameFrameWork game."
---

# WorldCreator

Reference: `docs/engines/WorldCreator.html`; owned records `docs/WORLD_METADATA.md`, `docs/ADVANCED_WORLD_GENERATION_MASTER_PLAN.md`, `docs/ROADMAP.md`.
Code: `src/WorldCreator/WorldCreator.Core` (net8.0), `WorldCreator.Cli`.

## What it is

An engine-independent **metadata** creator. Inputs:

- `WorldMetadataCatalog` — archetypes (id, kind, purpose, properties, parts, connection rules, child requirements, optional structure/footprint/access/capacity/services/component).
- `WorldMetadataRecipe` — scope, root archetype, scale, axis space, limits, and optional surface/environment/regions/placement/networks/routes/interiors/services/context/corridors/progression/layers/chunks/groundCover/population source.

Output: `WorldMetadataDocument` (schema 2) with nodes, connections and plans (layers, placement, access, routes, networks, environment, interiors, capacity, services, regions, local layout, progression, chunks, ground cover, drainage/water/wind, population coordination).

It does **not** produce or prove collision, navigation, gameplay, presentation or approved art. A renderer, Godot adapter or GameArts pipeline is an optional downstream consumer.

## Generation

```
var generator = new WorldMetadataGenerator(catalogs, layouts);
WorldMetadataDocument world = generator.Generate(recipe, catalog, token);
```

Pipeline: validate → content expansion → terrain → regions → allocation/placement → access → environment/interiors → networks/routes → corridors/services → context/progression/layers → chunks/ground cover/population → accounting. Each stage has an input and dependency fingerprint; expensive network/route/placement work is memoised within the run. A changed dependency refuses reuse.

## Rules

- Every node has an archetype and purpose. No arbitrary scattering, no vacant-cell fallback.
- Coordinates are optional; when `requireAllPositions` is true, every node must end positioned.
- Bounds: ≤100k nodes, ≤500k connections, ≤50k additional connections, ≤256 parameters, ≤8 axes, ≤1k archetypes, ≤10k parts; archetype depth ≤32, no cycles.
- Connection rules must be complete (`Chain` has no target; others name a valid part).
- Route/network kinds cannot collide; a route budget needs route rules; placement needs surface and environment.
- Same recipe + catalog ⇒ same document. `verify-metadata` replays the complete source and compares.
- The procedural city generator is removed; do not restore it. Metadata completeness ≠ playable world.

## CLI

```
WorldCreator.Cli generate-metadata <recipe.json> <catalog.json> <new-output-dir>
WorldCreator.Cli metadata-session ...
WorldCreator.Cli verify-metadata ...
WorldCreator.Cli study-metadata ...
WorldCreator.Cli domain-session / generate-world-domains / generate-regional-domain-world / region-session
WorldCreator.Cli select-population / verify-population
WorldCreator.Cli capture-schema / review-capture / compile-capture
WorldCreator.Cli structure-pipeline metadata|reconcile|ground|scene|production-inventory|production-bind|native-static|tile-topology
WorldCreator.Cli list-catalogs|list-buildings|list-objects|list-areas|list-facilities / save-catalog-content
```

Operations are recorded with operation logs, audit and notifications through `JsonLineOperationStore`; cancellation is honoured.

## Mockup-derived worlds

`capture-schema` → `review-capture` → `compile-capture` records shape (point/polyline/polygon), evidence (observed/inferred/authored/unknown), junction kind, review and coverage (partial/complete). GameArts gates require approved assets/atlases and motion metadata before production use. A scene skeleton is not a visually complete world, and passing geometry checks is not owner approval or Godot acceptance.

## Export

`WorldDocumentExporter` writes world JSON and, with `IWorldPlanRenderer`, an SVG/plan view. `IWorldExporter` is the boundary; `CoordinatedSettlementWorldBuilder` composes settlement-scale content from the same planners.

## Tests

The WorldCreator test catalog and `tests/WorldCreator.Tests` cover planner rules, validation refusals, deterministic replay and shipped content. Run the focused project for the slice you changed.
