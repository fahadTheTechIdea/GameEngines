# Derived generation regions

These are framework controls, not a configured game or an approved visual design.
The source requests contain complete regional recipe/catalog programs and game-
authored weights, area/clearance minima and suitability profiles. They contain no
ownership polygons, generation-domain polygons, root origins or selected root
anchor coordinates.

`MetadataRegionalOwnershipPlanner.Plan(request)` derives a normal
`MetadataRegionalWorldDefinition`. The common connected area kernel divides the
complete outer domain under the declared weights/minimum areas. The existing
terrain region owner then chooses each program anchor using its actual terrain,
environment and suitability requirements. The derived definition binds ownership,
generation domain, root origin and the named placement anchor into each template.
`MetadataRegionalWorldGenerator.Generate`, independent `GenerateRegion`, assembly,
publication and sessions use their existing algorithms and replay that derivation.

The public command is
`generate-derived-regional-world <request.json> <new-output-dir>`.
It produces complete world/definition JSON, individual regional packets and the
combined JavaScript/SVG inspector. No wizard, image, game name or Godot is required.

`request.json` has three complete programs. `moved-request.json` moves the protected
void: ownership stays the same, while the actual program anchor responds to the
terrain. `weighted-request.json` doubles one region's allocation weight.
`concave-request.json` uses a notched outer domain. Each complete result retains15
nodes,9 physical sources and actual outward/return journeys.

`request-4.json`, `request-8.json` and `request-16.json` retain the same100×80m world,
513×513 terrain source, source geometry, ports, movement widths and budgets. The
16-region result has80 nodes,48 physical sources,32 directed local paths and30
directed regional paths. These counts establish bounded program composition;
they do not establish finished ground content or broad world-design acceptance.

`request-small-units.json` is a separate metadata-only unit-scale regression: a
0.5×0.5-unit domain contains both location programs with declared minimum area
0.05unit². Ownership
has no implicit one-square-meter/unit threshold. This does not change the source
sizes, extent, counts or physical widths of the full-size controls above.

Ownership includes excluded/unplayable ground and does not imply streets or a
gameplay grid. These controls use weighted uniform area over one simple outer
boundary. Optional demand-derived weights are described below; explicit
[terrain/environment density](../density-owned-world/README.md) can also shape
ownership. Root anchors, content and routes remain terrain-qualified.
Templates must leave `Surface`, `Origin`, semantic `Regions`, `LocalLayouts` and
`RegionDemands` unset, and the root placement boundary unset. Existing explicit
anchors are preserved; an anchor with the selected generated ID is a conflict.
Nested physical programs retain their own local constraints and coordinates.

`request-program-capacity.json` supplies `Capacity` on each region. The engine
analyzes each complete unplaced program and measures its root source envelopes.
Declared50% maximum coverage derives240m² per program. `request-larger-program.json`
enlarges one actual compound from120m² to180m²: required area becomes360m² and
ownership grows proportionally. Both worlds keep15 members,9 whole physical sources
and10 directed local/regional journeys. Nested programs are analyzed completely
without counting their area twice. Registered sources, reservations, rotation and
surroundings use the common footprint resolver; no second source sizing policy is
introduced. Use the same installed `WorldMetadataGenerator` for ownership planning
and regional generation when custom layouts or registered catalogs are required.

`AnalyzeProgram` is also a public API for examining complete content before assigning
coordinates. Its result is not a placed world. This analysis route explicitly
retains one static source/population pair as `SourceProgram`. Terrain-derived
population counts, program-composition choices and already-placed source-world
analysis still refuse explicitly. See the
[portable complete population world](../portable-population-world/README.md).
Coverage-derived area/clearance does not prove packing or finished ground content;
the full physical generator can still reject an infeasible program.

See [ownership qualification](../../../docs/evidence/2026-10-31-derived-ownership-verification.json)
and [program demand qualification](../../../docs/evidence/2026-10-31-program-ownership-demand-verification.json).
