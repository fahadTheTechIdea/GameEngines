# Required program spaces without authored coordinates

Reusable framework inputs, not a configured game. Each recipe declares required
`ProgramIntents` and an `AreaPattern`, with an empty anchored `Programs` list.
The C# engine derives complete spaces and centroid anchors from the selected
terrain region, then qualifies full source-sized structures, reservations and the
required interface route through existing owners.

- `uniform`: equal requirements, uniform terrain density.
- `weighted`: changed density moves programs, preserving content/terrain/regions.
- `unequal`: differing minimum areas receive proportional shares.
- `alternative`: an axis cannot fit the full reservations; a declared alternative
  succeeds after the rejection is retained.

All four keep seven nodes, two 84 m² structures and two 192 m² reservations. They
retain explicit partial-world coverage. Geometry, allocations and shared spans
are real engine output; no game name, city model, mockup, artwork or Godot is needed.

Each folder supplies `recipe.json` and `catalog.json` for the current command:
`generate-metadata <recipe.json> <catalog.json> <new-output-dir>`.
Outputs include generated area/weight provenance, all source metadata, semantic
geometry and the JavaScript schematic. Invalid centroids, insufficient spaces or
source shapes refuse without placement fallback or accepted-world replacement.

See [contract](../../../docs/WORLD_METADATA.md#terrain-derived-required-program-spaces)
and [verification](../../../docs/evidence/2026-10-29-program-spaces-verification.json).
This is a bounded game-intent grammar; whole-world design synthesis, broad scale
and visual acceptance remain unfinished. Godot stays deferred.
