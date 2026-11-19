# Terrain-qualified partitions

This reusable framework reference demonstrates how terrain and required content
influence world ownership. It is a technical world-generation control, not a game
or a finished visual design. Godot and artwork remain deferred.

Two complete nested programs require different terrain kinds and at least3000m²
of suitable terrain each. The first weighted division gives each enough outer
area, but only2000m²of its required terrain. The existing authored90-degree
alternative provides4000m²to each program. The engine now discovers that
alternative through terrain qualification inside the shared area search.

The saved requests exercise baseline terrain, a changed terrain extent, an
irregular six-vertex world boundary, explicit destinations that require another
ownership division, and larger18×14m catalog compounds with measured clearance.
Each retains ten nodes, six physical sources and all required outward/return
journeys. Terrain kinds and traversal permissions are authored game rules.
Ownership polygons allocate generation responsibility; they do not impose city
boundaries, streets or a mandatory world grid.

Use `MetadataRegionalOwnershipPlanner.Plan(request)` and
`MetadataRegionalWorldGenerator.Generate(definition)`, or the existing
`generate-derived-regional-world` command. No new game/profile wizard is required.
`OwnershipPlan.PartitionQualification` retains actual rejected/qualified leaf
queries and final selected ordinals. Its work is distinct from geometry work and
charged to the same planning budget. Normal full physical generation, saved
read/export, source edits, restored history and rollback remain mandatory.

Finite alternative exhaustion, attempt/work limits and malformed policy are
different failures. The engine does not reduce content to make an alternative
fit. Later packing or global-route failures do not yet drive another partition
search. Broad world design, variety/scale, finer developer editing and independent
streaming remain open. See the [contract](../../../docs/WORLD_METADATA.md) and
[verification, exact commands and remaining limits](../../../docs/evidence/2026-10-31-terrain-qualified-partitions-verification.json).
