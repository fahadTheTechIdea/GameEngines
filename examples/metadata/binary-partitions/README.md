# Recursive local partition framework example

Reusable framework inputs; this is not a configured game. `recipe.json` uses a
recursive local cut around a terrain-selected region anchor. `alternative-recipe.json`
preserves the same hard program and lets the existing bounded strategy selector
reject an unsuitable grid before selecting the recursive division. Both use
`catalog.json` and retain seven nodes, two complete structures and their source
footprints/reservations. No city model, mockup, artwork or Godot is required.

The engine command is `generate-metadata <recipe.json> <catalog.json> <new-output-dir>`.
The result contains engine metadata, semantic layers and a JavaScript schematic.
Cut origins are relative to the same generated regional anchor as the program
anchors. A cut's normal points toward its positive child. Tree leaves name the
required programs; partition boundaries do not imply connections or walls.

See [contract](../../../docs/WORLD_METADATA.md#recursive-cut-partitions) and
[verification](../../../docs/evidence/2026-10-29-binary-partitions-verification.json).
The full world generator, broad design acceptance and scale qualification remain
unfinished. Godot stays deferred.
