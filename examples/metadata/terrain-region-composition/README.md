# Terrain-dependent regional composition

Framework inputs for the C# world metadata generator; no particular game, artwork,
mockup or Godot dependency. `baseline` and `raised-terrain` contain the same complete
six-compound catalog program and topology. Only terrain elevation changes.

The irregular 11,225-square-meter domain contains ground, woodland and a protected
pond. Required lowland, highland and woodland regions use distinct suitability
profiles over one shared terrain partition. Suitable union area is 11,081;
woodland is 612 and the pond excludes 144. All 29 nodes, 28 source-sized physical
members, fourteen nested program instances and nineteen bidirectional connections
are retained in both variants. The root allocation uses continuous region shapes;
these internal convex geometry pieces are not roads or world blocks.

`cross-profile-interface` verifies a physical crossing at the actual shared
regional contour. `walled-cross-profile-interface` adds two touching source-sized
rooms and ten wall pieces; traversal uses the walls while regional eligibility
continues to use the base terrain.

Generate with `generate-metadata <recipe.json> <catalog.json> <new-output-dir>`
using WorldCreator.Cli. Current acceptance packets are under
`generated/terrain-region-composition`. The test fixture can write these inputs
to a new directory with `--write-region-composition-examples`.

This is bounded, sparse composition. Intent seeds, programs and catalogs remain
authored; most eligible land is unpopulated. Automatic regional intent, environment
population, broader expressive range, transition inference and world-scale
performance remain open. Godot is deferred. See
[verification](../../../docs/evidence/2026-10-28-region-composition-verification.json).
