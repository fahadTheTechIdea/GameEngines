# Terrain-derived program regions

This framework example derives three semantic regions from one connected suitable
landmass. It contains no authored region seeds or root program coordinates.
`Regions.IntentPartition` assigns required intents to components and uses the
shared connected area kernel to derive their shapes and interior anchors.
`Placement.RegionAnchors` binds three complete catalog compounds to those anchors;
each retains two physical modules. Existing local routes and a generated network
connect all declared destinations. The result has11 nodes and9 physical sources.

`recipe.json` retains a100×80m outline and a4-point protected void.
`terrain-edit-recipe.json` cuts a375m² notch from the outer outline without dropping
required content. Both retain the513×513 source terrain definition, original
footprint sizes, movement widths and finite budgets. Capture stores the required
original-lattice window; it does not resample or shrink the source definition.

The normal C# entry point is `WorldMetadataGenerator.Generate(recipe,catalog)`;
the same inputs work with `generate-metadata <recipe> <catalog> <new-output-dir>`.
The publisher provides recipe/catalog/world JSON, physical accounting and a
JavaScript/SVG schematic. `MetadataGenerationSession` supports attributed edits,
saved history and restore through the same generation owners.

This policy currently requires one normalized suitability profile. It weights
subregions by each intent's minimum area, optionally modulated by a declared
density field. Minimum area, clearance and inter-intent distance constraints are
hard requirements. Fixed seeds protect their complete source components. Attempts,
partition receipts, geometry and budgets replay on read/export. Mixed-profile
subdivision refuses explicitly. Component search and the declared split grammar
are bounded; refusal does not prove no other world design could work.

These are semantic terrain regions, not generation ownership, storage chunks or
automatic finished ground content. This example qualifies geometry and pipeline
composition; broad game-world design, artwork and Godot remain separate gates.
See [qualification](../../../docs/evidence/2026-10-31-region-partition-verification.json).
