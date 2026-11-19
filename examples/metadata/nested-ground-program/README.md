# Nested source capacity

This framework example preserves required content through multiple placement
stages. It is a reusable compound program, not a configured game or artwork.

The 20 × 20 m ground reservation contains two 8 × 16 m branch reservations.
Each branch contains two required 6 × 6 m solid sources: four solids occupy
144 m², leaving 256 m² (64%) open. The supplied recipe requires at least 60%
open space and generates all eight nodes with seven physical allocations.
Reservations do not count as solid occupancy.

Use the public `WorldMetadataGenerator.Generate` / `GenerateFilesAsync` APIs or
`generate-metadata <recipe.json> <catalog.json> <new-output-directory>`. Reload
the published package with `ReadPublishedAsync` or `verify-metadata`.

Increasing `ground.PhysicalFootprint.GroundUse.MinimumOpenFraction` in the catalog
to `0.75` is impossible without changing the design. The engine now refuses
before candidate placement with `GROUND_USE_CAPACITY`. The witness retains all
four required source IDs, dimensions, areas and both contributing source stages.
It does not shrink or drop members to make the request fit.

This proof combines disjoint branches only through footprint-contained stages.
A solid ancestor is counted once; reservation-scoped escape is not assumed
contained. Exact final accounting still handles cases the preflight cannot prove.
Passing this example does not establish complete world design or visual approval.
Godot and artwork are deferred.
