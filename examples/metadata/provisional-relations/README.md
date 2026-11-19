# Provisional structure relations

Game-independent framework examples using the common C# metadata and placement
owners. All catalog counts, exact source polygons, terrain constraints and
planning limits are preserved. These are declared-scope technical references,
not a complete game, city or production-art package.

- `single`: two required structures at `(0,0)` and `(6,0)` constrain a third
  structure to be 5m from both. It is generated at `(3,4)`; terrain excludes the
  lower solution. References are to structure demand IDs, not duplicate anchors.
- `focused`: the dependent structure initially has no eligible fixed-anchor
  candidates in its required scalar-field window. An unpinned source is resolved
  first; the new relation intersection supplies the valid complete footprint.
- `backtrack`: terrain excludes both solutions for the preferred source at the
  origin. The source and dependent structure are solved together. Final members
  satisfy every original distance and terrain constraint; no size/count changes.
  Search can derive either endpoint of the symmetric relation.
- `nested`: one program creates three source-sized members inside each of two
  parent compounds. Authored upper-side intent selects `(3,4)` in each local
  frame, with the second parent translated by 20m. All nine nodes/eight physical
  members and both program instances are retained. Parent reservations cannot
  cover forbidden terrain; the negative case explicitly checks that refusal.

```powershell
dotnet run --project src/WorldCreator/WorldCreator.Cli -- generate-metadata examples/metadata/provisional-relations/nested/recipe.json examples/metadata/provisional-relations/nested/catalog.json generated/my-provisional-world
```

Replace `nested` with another example name to inspect it. Outputs contain metadata,
SVG and the common JavaScript schematic. C# owns calculation and replay; the
viewer consumes saved geometry. No mockup, grid, game setup or Godot is required.

[Exact sources, commands, independent geometry and remaining limits](../../../docs/evidence/2026-10-30-provisional-relations-verification.json).
Finite candidate search does not guarantee continuous packing or globally optimal
preferences. The broader core qualification matrix remains open; Godot deferred.
