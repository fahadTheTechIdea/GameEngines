# Source-derived game ground

These are reusable framework controls. They are not a configured game or approved
world design. The engine generates material metadata from declared terrain,
structures, surroundings and corridors; no image, game-name wizard or Godot is
required. No objects are added just to hide open areas.

`site-recipe.json` and `site-catalog.json` retain the complete rotated, off-center
physical source, its reservation, entrance and four surroundings features.
`GroundCover` assigns explicit terrain, body, yard, access, arrival and buffer
materials. Every cell retains original geometry and source/parent/rule lineage.

- `partial-recipe.json` retains unassigned land as unresolved. Its coverage flag
  permits this partial result; requesting complete material coverage refuses it.
- `source-edit-recipe.json` / `source-edit-catalog.json` enlarge the actual source:
  body material becomes88m² instead of80m², preserving all required site features.
- `terrain-edit-recipe.json` preserves a40m² source void and9960m² assigned cover.
- `irregular-recipe.json` / `terrain-gap-recipe.json` use the same concave95m² domain and4m² void. The terrain-gap control retains66m² sampled cover and25m² unresolved source terrain; strict coverage refuses it. These are metadata-only controls.
- `trails-recipe.json` / `trails-catalog.json` select already-qualified shared
  segments and junctions. Ground materials never redesign routes or their widths.
- `allocations-recipe.json` / `allocations-catalog.json` select actual semantic
  region and local allocation cells. Their boundaries do not imply streets.
- `registered-recipe.json` / `registered-catalog.json` use the installed modern
  catalog's exact cottage and site geometry. Catalog remaining requirements persist.
- `regional-request.json` derives ownership and measured program demand, generates
  all15 metadata members/nine physical sources/ten directed journeys, and covers
  every nonexcluded regional domain with declared material intent.

The ordinary public commands are
`generate-metadata <recipe.json> <catalog.json> <new-output-dir>` and
`generate-derived-regional-world <regional-request.json> <new-output-dir>`.
They publish native world metadata, `ground-cover-plan.json` for individual worlds,
and the exact JavaScript/SVG inspector. Source history sessions use the same pipeline.

In C#, `WorldMetadataRecipe.GroundCover` runs during normal generation.
`new MetadataGroundCoverPlanner(registry, layouts).Plan(definition, world)` derives
the same plan from an existing complete metadata source, using installed dependencies
for source validation. Pass the current registry/layouts when required by that source.

Rules select `Terrain` by original kind; `Footprint`/`Reservation` by archetype ID;
`SiteFeature` by enum kind; `Region`/`Allocation` by ID; and `Corridor` by realized
group ID. Minimum match counts are explicit (default one); optional declarations
can request zero. Counts measure resolved source polygons, including regional cells.
Higher priority wins. Equal top-priority materials must agree; every agreeing
source stays in the cell's provenance.

Ground cover assigns materials, not content completeness. Planting, props, activities,
gameplay interactions, packing, art and streaming need their own declarations and
qualification. No universal palette or physical simulation is inferred. Geometry
is bounded by the existing source/cell/work/JSON and common area-kernel limits.
The decomposition cells are independent of gameplay grids and production tiles.

See [API details](../../../docs/WORLD_METADATA.md) and
[verification](../../../docs/evidence/2026-10-31-ground-cover-verification.json).
