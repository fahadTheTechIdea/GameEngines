# Single-floor interior journey

The shared engine compiles an authored concave floor program: three 6×6 rooms,
two perpendicular 2-meter-wide corridors, four 0.8-meter openings and three solid
1×1 contents. Thirteen nodes and twelve physical allocations are retained.
Floor area is 136 m². Six bidirectional path legs join exact doorway approaches
into a 26-meter entry-to-exit journey through the required middle room.

The root placement request explicitly sets `AllowBoundaryContact: true` so room
and corridor floors can share edges. Positive-area overlap still rejects; positive
`MinimumSeparation` still requires a gap. The default contact policy remains
disjoint. Nested source sizes and hierarchy are not resized to achieve a fit.

Generate using `generate-metadata recipe.json catalog.json <new-output-directory>`.
Run `dotnet run --project tests/WorldCreator.Tests -- --world-interior` for focused
checks. After producing `generated/interior-journey`, run
`node --test tests/javascript/world-interior.test.cjs` for saved schematic checks.

This is a source-defined single-floor program with engine-validated allocations,
door approaches and passage geometry. It is not a general dungeon-topology
generator, wall mesh, door animation or playable Godot scene. The floor boundary
and solid footprints are movement constraints; no artwork is implied.
