# Catalog-driven region capacity

This framework example derives region requirements from complete physical program
sources. It does not require a game name, mockup, artwork or game engine.

`recipe.json` declares three intents with minimum area1 and no required clearance.
`RegionDemands` selects the expanded root demands by their `RegionId` and declares
maximum envelope coverage12%. The engine resolves actual catalog footprints and
reservations through the same placement owner used during generation. It sums
root envelope areas once and derives required region area as area/coverage.
Clearance uses the largest envelope radius about its actual local origin.
Contained modules remain governed by full nested placement and route checks.

`catalog.json` gives every root a120m² envelope. `larger-catalog.json` enlarges
the second source to180m²; its required region area grows from1000 to1500m²,
and its actual derived region grows from2634.67 to3387.43m². Both complete worlds
retain11 nodes,9 physical sources and10 directed physical paths.

`composed-recipe.json` / `composed-catalog.json` add bounded program selection.
A cheaper complete source requires more space than the terrain provides. The
composition owner records that refusal and selects a feasible complete alternative.
Its required members are retained; every region request and preparation work remain
counted. The engine does not resize a source or remove members to make it fit.

Use the normal `WorldMetadataGenerator.Generate(recipe,catalog)` or
`generate-metadata <recipe> <catalog> <new-output-dir>` entry point. Publication
provides exact JSON and an interactive JavaScript/SVG schematic; selecting a root
shows the source-derived requirement. Sessions support catalog edits, clean
equivalence, saved history and failed-edit preservation.

Area and circular clearance are conservative requirements, not packing proofs.
Coverage here measures root source envelopes, not fully populated or intentionally
open ground. Semantic regions do not establish independent generation ownership.
Full world-design qualification, streaming, artwork and Godot remain open.
See [qualification](../../../docs/evidence/2026-10-31-region-demand-verification.json).
