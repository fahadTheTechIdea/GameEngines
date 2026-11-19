# Composed framework world

This example combines the complete terrain-derived compound program, a physical
outdoor/interior program and independent logical progression. It is a reusable
framework fixture, not a configured game. It needs no AI, art profile, mockup or
Godot project.

`definition.json` contains three independently generated domains:

- `terrain`: 61 nodes and 60 physical members, including seven compound families,
  nested structures, ground areas, equipment and landscape content. Inland terrain
  and the catalog program determine its regions, placements and network.
- `local`: 27 nodes and 26 physical members: two outside structures with named
  entrance ports, six generated rooms, six original fixtures and twelve additional
  equipment members placed by one parent-scoped source-route program.
- `progression`: a root and two required states, with no invented physical space.

Two explicit bidirectional game transitions connect independent coordinate
domains. The first binds a compound port to an outside structure port. The second
binds a generated room opening to logical progression. These are declared game
transitions; they do not imply a physical bridge, portal artwork or runtime logic.
The room endpoint uses `InteriorOpening: {ConnectionKind: "door",
ConnectionIndex: 0}`. The engine selects the canonical incident connection and
qualifies its exact generated opening. The input author does not run generation
first to discover a connection ID or copy coordinates. The result records
`ResolvedInteriorConnectionId`; a missing chosen opening refuses without selecting
another connection. Explicit `InteriorConnectionId` remains available when that
specific source identity is the intended rule.
The required journey starts at the terrain lookout and traverses the network,
outside entrance and rooms before reaching a progression state, then returns.

`coastal-edit.json` replaces the complete terrain input with its coastal
counterfactual. All 91 nodes and 86 physical members remain required. The local
and progression worlds remain byte-identical and reuse their stage caches.
`missing-rooms-edit.json` is a deliberate failing edit: it excludes doors from
the journey's allowed kinds. Domain-level reachability still exists, but the
actual required physical journey must refuse. No partial edited packet publishes.

The public Core-only host in
[worldcreator-workflow](../../worldcreator-workflow/README.md) accepts these first
two files through its existing `--domains <definition.json> <edit.json>
<new-output-directory>` mode. The CLI exposes the same lifecycle through
`domain-session start` and `domain-session edit`. Outputs contain the complete
domain data, per-domain schematics, an index and a replayable session checkpoint.
Edit authored definitions rather than resolved coordinates or cached output.

Qualification compares actual public SDK, CLI and test-host outputs and complete
checkpoints. An independent verifier expands every saved source route segment,
opening and logical connection in both journey directions: 24 steps per direction
inland and 23 coastal. Those counts describe saved paths, not a distance or
gameplay-quality score. Six saved JavaScript packets preserve the complete data.
See [verification](../../../docs/evidence/2026-10-30-broad-composition-verification.json).
The current semantic endpoint inputs are qualified separately in
[opening-selection evidence](../../../docs/evidence/2026-10-30-semantic-opening-verification.json).

This qualifies one bounded mixed composition. It does not establish complete
game design, every genre, unlimited scale, moving doors, temporal gameplay,
production artwork or player/runtime acceptance. Godot remains deferred.
