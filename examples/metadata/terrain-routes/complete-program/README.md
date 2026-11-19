# Fixed complete program on four terrains

These are reusable framework inputs for the ordinary `generate-metadata` API/CLI,
not a configured game. Select `flat-recipe.json`, `hillside-recipe.json`,
`coastline-recipe.json` or `valley-pass-recipe.json` with the common `catalog.json`.
Only terrain changes between recipes. The generated valley/pass uses sampled
ridge operations; no route waypoints are supplied.

Every result retains the same nine nodes: two outposts, each with a shelter,
work yard and supply cache. All eight physical footprints/ports, source positions
and three bidirectional connections remain complete. The coastal inlet and ridge
pass change real path alignment. The hillside changes directed ascent/descent
costs. Independent polygon/triangle geometry checks validate full passage width
against solid bodies, water and terrain above the declared slope limit.

This is a complete declared program, not an entire game or a scale benchmark.
The catalog owns source sizes and parts; semantic game behavior belongs to its
consumer. Presentation, artwork and Godot are downstream and deferred.

[Connection-stage evidence](../../../../docs/evidence/2026-11-02-s04-core-gate-verification.json)
