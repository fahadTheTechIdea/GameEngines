# Retained-water metadata reference

`recipe.json` and `catalog.json` declare a protected basin, explicit inflow and
retained sink, a fixed lake level, dry encounter destinations, surrounding land,
a land-only passage and optional water-aware wind derivation. This is a bounded
framework reference, not a specific game or a complete playable world.

The ordinary `Water.RetainedBasins` input drives the existing water stage. Its
occupied result feeds the normal wind/environment, region, site, route and chunk
pipeline. A level edit regenerates its geometry and all dependent outputs, while
original terrain/drainage and required content can remain cached. Source-rate
edits update receipts without silently inventing a new level. Failed closed-shore
requirements and cancelled edits preserve accepted world/cache state.

The small reference has three metadata nodes. At level 1, its analytic basin
occupies 2 square meters with 2/3 cubic meters geometric capacity; remaining dry
support is 14 square meters. At level 1.5, occupied area becomes 4.5 square meters
and capacity 2.25 cubic meters. The land-only passage bends around actual water.
Original elevations remain unchanged; water surface planes are level.

The larger nested reference raises an existing protected depression's level from
1.15 to 1.17. Source/population/wind/demand recomputation preserves all seven
complete compounds, 16 landscape members and 82 paths: 59 nodes/58 physical
members. Edited output equals clean generation and its original-input checkpoint
restores after atomic publication. The 1.8m ridge, 25000m² minimum, source
dimensions and work budgets are unchanged. A captured one-ULP generated seam
failure was corrected at the water geometry owner; the failing inputs remain in
the evidence, and the requested level was retained.

Saved lake bodies contain their definition, selected connected region, bed-plane
and depth/capacity records, patch IDs, terminal receipt and shoreline witnesses.
Standalone water JSON needs its drainage context for explicit validation; strict
world reads and metadata checkpoints replay the full derivation. The ordinary
16MiB JSON publication/read limit remains enforced.

The schematic displays saved faces, level/capacity tooltips and exact engine
coordinates. Browser selection, terrain visibility, fit and actual SVG export
were checked. SVG inspection verifies all six water faces, 2 square meters and
the three actual node coordinates. JavaScript checks bounded saved-data
consistency; C# owns numerical generation and replay. No art or native-runtime
acceptance follows from this technical review.

See [core policies and limits](../../terrain/retained-water/README.md) and
[verification](../../../docs/evidence/2026-10-29-retained-water-verification.json).
Wider world/environment/domain variety, dense-field limits and design acceptance
remain open; artwork and Godot remain deferred.
