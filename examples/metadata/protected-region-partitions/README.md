# Fixed destinations within generated regions

This engine-independent example keeps three chosen destinations while generating
their surrounding connected regions, complete nested physical programs and travel.
It uses the existing catalog/recipe API; no city model, mockup, artwork or Godot
is required.

`recipe.json` and `catalog.json` retain all 11 nodes and 9 exact physical sources:
three compound parents, their required modules and the original marker. A terrain
void remains excluded. Each region intent declares `RequiredPosition`; its real
region must contain that exact point with the declared clearance. Region anchors
feed the normal physical placement owner. Six local and four network travel
directions remain complete.

`changed-recipe.json` extends the world boundary from 100×80 to 120×80 meters.
The suitable area changes from 7,904 to 9,504 m². Required destinations stay fixed,
while region polygons change to fit the new source. Counts, source sizes, hole,
movement profiles and finite search budgets remain intact.

Use the normal `WorldMetadataGenerator`/`MetadataGenerationSession` APIs or
`generate-metadata`/`metadata-session` CLI entry points. Inputs and source history
remain authoritative. An invalid fixed point rejects generation/publication;
a failed edit preserves the previously accepted packet. `RequiredPosition` fixes
the point for that request. Preventing a subsequent authored edit from changing
the declaration uses the separate session input-protection contract.

Within a declared `PartitionSearch`, both directed sides of each allowed axis
may be considered for pinned semantic members. Each considered side consumes the
existing attempt/work budgets and retains its rejection or selected receipt.
Unpinned requests retain their canonical side ordering. The default single-cut
policy still has its original finite scope. An exhausted search does not prove
that every possible continuous partition is impossible.

The [contract](../../../docs/WORLD_METADATA.md#terrain-derived-required-regions-and-placement-anchors)
describes component assignment, signed clearance, area/density and typed diagnostics.
[Qualification](../../../docs/evidence/2026-11-01-protected-partitions-verification.json)
records SDK/CLI parity, complete source geometry/travel, rotation, holes, mixed
automatic/fixed anchors, zero density, bounds, cancellation, edit/replay and rollback.
Browser rendering is not qualified in this increment; broader professional world
design and variety/scale work remains open. Godot/artwork are deferred.
