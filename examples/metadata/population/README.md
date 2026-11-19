# Purposeful physical population

The reusable catalog declares two journey entrances, four approach markers,
four reserve-boundary markers and one required resource. The engine places all
eleven with exact source footprints and reservations on gently sloping terrain.
Two bidirectional routes connect the resource's real entrance to both gates.

The placement recipe derives anchors from a bent path, an explicit span of a
concave polygon and named sockets. It declares counts and purpose; no vacant
space is filled automatically. The authored marker path is not a generated road.

```text
generate-metadata recipe.json catalog.json <new-output-directory>
```

Retained output: `generated/purposeful-population`. `schematic.html` renders the
exact C# world coordinates and exposes source, terrain and access evidence.
`blocked-entrance.json` moves the resource socket into a gate approach. It must
fail with `METADATA_ACCESS_APPROACH_BLOCKED` in placement feedback and publish
nothing. Required items cannot be omitted to make the route pass.

Tests additionally cover a nested parent frame, numeric child indices beyond 9,
wrong/missing sockets, insufficient path capacity, footprint boundary protrusion,
overlap, terrain holes, source-history replay and full/incremental equality.
[Evidence](../../../docs/evidence/2026-10-27-world-population-verification.json)
records build, focused regressions, CLI, JavaScript and browser checks.

This is a complete declared population test program, not a completed world or
city. Generated-route population, region interiors, sightlines, controlled
variation, large-world scale, artwork and native Godot acceptance remain open.
