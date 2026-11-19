# Terrain-aware world connections

The [complete-program comparison](complete-program/README.md) retains two outposts,
all eight physical sources and six required directions across four terrains.
It uses the same shared generator as this smaller route algorithm fixture.

This is a small algorithm fixture: two authored destinations in a concave exploration region, with a forbidden terrain strip between them. The same reusable core can interpret arbitrary game-defined terrain kinds and connection kinds. It does not instantiate city districts, streets, parcels or a global grid.

`recipe.json` declares a surface, one `travel-intent` route selector, a one-unit passage radius and terrain costs. `catalog.json` declares the two destinations and their logical connection. The C# engine calculates the detour; JavaScript displays those exact coordinates. Both directions retain connection and rule provenance.

```text
dotnet src/WorldCreator/WorldCreator.Cli/bin/Debug/net10.0/WorldCreator.Cli.dll generate-metadata examples/metadata/terrain-routes/recipe.json examples/metadata/terrain-routes/catalog.json <new-output-directory>
```

The agent executes generation as part of normal work; this command documents reproducibility. The saved review is `generated/terrain-routes/schematic.html`. Terrain and passage controls are separate from dashed logical links. The region/destination circles are markers, not physical structures.

Observed route: `(15,15) → (43.5,26.5) → (53.5,26.5) → (100,15)`, with the reverse separately calculated. The straight distance is 85 units; the qualified detour is approximately 88.634 units. This fixture does not claim realistic world composition, terrain-feature generation, complete networks/junctions, footprints, artwork or Godot readiness.

[API, budgets and limits](../../../docs/WORLD_METADATA.md#terrain-aware-passage-generation) · [verification](../../../docs/evidence/2026-10-26-world-routes-verification.json)
