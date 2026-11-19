# Terrain-driven regional demand

These framework inputs generate complete connected content programs. They are
technical metadata examples, not a specific game or finished art direction.

The C# ownership planner accepts `OwnershipEnvironment` and a selected
`Pattern.DensityFieldId`. Here `demand = 1 + x / 336` shapes three regions by
equal integrated demand, so their geometric areas differ. Reversing the field
changes actual ownership and complete program positions. The environment is
explicit; program suitability environments still control playable anchors.

| Input | Declared physical content |
| --- | --- |
| `request.json` | 48 nodes, 45 physical sources, 10 directed local/regional paths |
| `reversed.json` | Same complete program, reversed spatial demand |
| `piecewise.json` | Same complete program, density derived from actual terrain elevation and a continuous change of slope |
| `source-size.json` | Same membership, complete compound envelope grows from 58×50 to 64×50m |
| `more.json` | 87 nodes, 84 physical sources, 16 directed paths; all three modules per compound retain their required surroundings |
| `irregular.json` | Additional concave outer boundary at the same 336×224m extent; 48 complete members and original travel requirements |
| `coordinate-heightfield.json` | The same 48-member program over a 17×17 checkerboard heightfield, with a 64-cell regional budget and coordinate-defined density |

All controls retain the original 560m² protected void, 0.4m actor diameter,
source footprints, access widths, required 85% open activity ground and existing
budgets. Ownership includes excluded ground; placement and paths avoid it.
Density cannot satisfy a geometric area requirement or resize required content.
No ownership edge implies a street, wall, gameplay grid or storage chunk.

Coordinate-only density and its derived/clamped fields use compact regional
geometry. Elevation, slope and sampled-field densities retain their terrain
decomposition. The checkerboard example preserves every source sample while
integrating `1 + x / 336` analytically across the complete world.

Use `MetadataRegionalOwnershipPlanner.Plan` followed by
`MetadataRegionalWorldGenerator.Generate`, or the existing
`generate-derived-regional-world` CLI. Independent region generation, native saved
replay and source-history sessions use those same owners. The schematic renders
the saved geometry and performs bounded structural checks; native replay remains
the authentication authority.

Current limits include 2..16 regions, two spatial axes, complete known terrain,
one simple ownership loop per region (≤64 vertices), existing cell/work budgets
and statically analyzable capacity programs. Missing/unused environments, unknown
fields, negative/zero density mass, terrain gaps, geometric infeasibility and
tampering refuse explicitly. Terrain/capture input restrictions from the existing
ownership contract still apply. This is bounded mass partitioning, not automatic
world design, coupled repair, independent streaming or a completed game.
Godot and artwork remain deferred.

See [the API contract](../../../docs/WORLD_METADATA.md#deriving-ownership-and-program-anchors)
and [qualification evidence](../../../docs/evidence/2026-10-31-ownership-density-verification.json).
