# Declared local strategy selection

Framework fixtures for the shared engine, not a configured game. Each keeps seven
metadata nodes, two exact 14×6 m structures with 16×8 m reservations, all four
constituent metadata members and one physical bidirectional interface connection.
Region-relative program offsets and required placement anchors do not move.

- `grid-to-weighted`: a 20 m local column cannot contain the full first source
  reservation at its fixed anchor. The declared weighted alternative partitions
  at x=22 and accepts the complete program.
- `weighted-to-grid`: the initial weighted influence -72 places the boundary at
  x=20. The declared 22 m local-grid alternative partitions at x=22 and fits.

Both accepted first allocations have independent area 440 m²; the second has
360 m². Programs and source constraints remain identical within each search.
`LayoutAdaptation.Dials` is empty; `Alternatives` names ordered complete layout
options. Baseline-first search retains the rejected baseline and accepted option
ID, source definitions, full physical result and work. The existing CLI
`generate-metadata` exports each recipe/catalog pair.

The C# suite also covers three-choice ordering, all-infeasible refusal, invalid
unused options, protected-contract changes, exact work/attempt ceilings, cancellation,
source-edit rollback/checkpoint replay and a complete 17-node/11-route region
reselection after local strategy exhaustion. JavaScript consumes saved geometry.

This realizes declared game-independent spatial grammars. Autonomous world intent,
broader strategy synthesis, world-scale performance and visual acceptance remain
open. Godot and production art are deferred.

[Verification](../../../docs/evidence/2026-10-29-layout-strategies-verification.json).
