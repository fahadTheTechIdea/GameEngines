# Interior-derived gameplay population

Framework inputs for the shared engine, without artwork or Godot. An ordinary
source phase generates the complete branching room graph, source-sized floors,
fixtures, walls, openings and full-width paths. A second catalog adds two required
equipment members inside every room, using the existing generated-route arc-length
and facing grammar. Source connection IDs select each room's incident passage;
the engine computes positions and tangents. No room or path coordinates are copied
into authored placement anchors.

| Case | Complete nodes | Physical sources | Required new equipment |
| --- | ---: | ---: | ---: |
| branching | 25 | 24 | 12 |
| rotated | 25 | 24 | 12 |
| compact | 13 | 12 | 6 |

The rotated declaration rotates the original equipment anchor and source body
with each floor; source dimensions and counts remain unchanged. Earlier fixed-side
declarations whose equipment overlaps an existing fixture remain failed attempts
in the evidence. This is not a guarantee that every authored path offset is feasible.

The source phase still owns connected-room growth. The consumer retains that root
grammar and all original fixtures, adding equipment to each room's local stage.
The source's exact room IDs, wall/opening geometry and forward/reverse route records
must remain identical. Preserved paths are rechecked against current terrain,
walls and solids at the original movement width; new content cannot redesign them.
Interior work receipts measure the current validation work, rather than copying
the original search work. Changed/missing source interior rules refuse.

`source-width-edit.json` changes compact equipment from 0.2 to 0.3 meters wide,
retaining all members and original paths. `blocked-recipe.json` and
`blocked-catalog.json` are an explicit frozen-source negative: placing the required
equipment directly on the accepted passage must refuse without publication.
The frozen source uses the current normal `SourceWorld` contract; positive recipes
retain executable `PopulationSource` inputs.

The actual API and CLI retain exact world/checkpoint bytes. Native source replay,
standalone interior validation, source-size edit/checkpoint/rollback, work and
cancellation guards are qualified. Independent arc-length/tangent/body equations
check all 42 equipment instances across five API packets. The JavaScript viewer
preserves the saved full geometry and validates source-derived orientation traces;
it is not an independent replacement for native source replay.

[Evidence](../../../docs/evidence/2026-10-30-interior-population-verification.json).
This is a bounded composition capability, not complete world/game design, unlimited
interior geometry, moving-door behavior or native runtime acceptance. The master
plan remains IN PROGRESS. Godot and artwork remain deferred.
