# Field-derived environmental population

Framework example using the C# metadata generator. No game setup, mockup, artwork
or Godot is required. `baseline` and `raised-terrain` retain the complete six-compound
terrain-region program and add eight required source-sized environmental features:
three lowland, three highland and two woodland members.

The engine extracts connected contours of the declared height field at -4, +4
and 0 meters. Purpose anchors select components; declared counts, spacing and
insets determine positions along those actual paths. Contour coordinates are
calculated by the C# field/geometry owners, not copied into a JavaScript mockup.
Raising the terrain by 0.2 meters moves every environmental member while preserving
all required source compounds and their nineteen bidirectional connections.

Each result contains 37 nodes, 36 physical members and fourteen nested program
instances. Exact source footprints, reservations, passages and region/layout
plans remain authoritative. Generated contour paths and member witnesses are
saved under `PlacementPlan.PopulationContours`; the schematic displays them as
dashed green lines. An impossible item rejects the proposal without relocation
to an arbitrary empty position.

Run `generate-metadata <recipe.json> <catalog.json> <new-output-dir>` through the
WorldCreator CLI. Current qualification packets are in
`generated/field-contour-population-accepted`. Inputs can be reproduced into a
new directory with the test command `--write-contour-population-examples`.

This is sparse, bounded environmental composition. Counts, field levels, intent
anchors, catalogs and regional purpose are authored. Broad area distributions,
automatic intent, transition inference and world-scale performance remain open.
This example uses search stages with the sixteen-demand bound. The separate
[prescribed example](../prescribed-population-scale/README.md) qualifies larger
exact assignments through the same physical owners. Godot is deferred. See
[evidence](../../../docs/evidence/2026-10-28-contour-population-verification.json).
