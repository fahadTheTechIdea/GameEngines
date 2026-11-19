# Physical world loops and failure tolerance

These framework inputs use the shared C# generator and the existing
`generate-metadata` command. They require no game setup, art profile or Godot.

- `cycle`: four terrain-qualified destinations and four bidirectional passages.
  `RequireSingleSegmentFailureTolerance` is enabled. Every directed journey has
  a saved three-segment alternative excluding its failed physical segment.
- `shared-trunk`: a logical triangle and intersecting branch share physical
  segments. The same explicit failure policy rejects publication. Logical loops
  do not create independent physical alternatives.
- `compound-inland`, `compound-inlet`, `compound-larger-content`: the same five
  complete compounds retain all 21 nodes, 20 exact physical footprints and
  nested shelters, yards and supplies. Six generated logical loops feed 20
  bidirectional routes and 40 directed corridor journeys. Terrain and a larger
  shelter source change placement without shrinking or dropping content. These
  examples do **not** request or certify single-segment failure tolerance.

The failure option belongs to each selected corridor group. It checks existing
directions and explicit same-level junction transitions under the shared
traversal policy. A failure removes both directions of one physical segment.
The complete alternative keeps the original endpoints; no extra path is drawn
or inferred. Irrelevant failures leave the original journey intact. The engine
replays receipts on read/export; the viewer validates their exact coverage and
step continuity before display.

Search uses the existing work budget and cancellation, with at most 4,096
failure checks and 1,000,000 recorded witness steps. Failure tolerance concerns
endpoint reachability only: junction/area hazards, required intermediate visits,
conditional gameplay rules, elevated crossings and runtime traffic remain
separate. The generator can reject a physical candidate through existing bounded
placement feedback; this option is not an exhaustive alternative-network solver.

These are technical geometry and connectivity fixtures, not approved game-world
design or production artwork. See [the contract](../../../docs/WORLD_METADATA.md#optional-physical-segment-failure-tolerance)
and [execution evidence](../../../docs/evidence/2026-10-29-physical-resilience-verification.json).
