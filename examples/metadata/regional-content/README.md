# Reusable regional content

Framework fixtures, not a configured game. The same complete two-structure program
follows a terrain-derived regional seed without explicit world placement anchors.
The two structures retain all four constituent metadata members; each keeps its
exact 14×6 m footprint and 16×8 m reservation. Every output has seven metadata nodes and one
bidirectional physical interface connection.

- `grid`: source program offsets (-7,0), (11,0) and grid origin (-20,-10)
  resolve about the computed (20,10) regional seed. The declared rotation dial
  changes the initial 0° local grid to -10° to fit the complete source program.
- `translated-grid`: identical catalog, offsets, layout and adaptation declarations;
  translating the terrain/domain by (-80,40) computes seed (-60,50) and moves all
  local physical content accordingly. No explicit placement anchors are added.
- `weighted`: the same source program uses a weighted partition. Its first
  allocation has independent area 440 m² and shared boundary x=22.

`Placement.AllocationAnchors` binds each structure's placement relation to its
resolved allocation program anchor. The engine retains source offsets and resolves
all physical geometry; JavaScript consumes saved geometry. The grid first-area
oracle is 400/cos(10°); the translated shared-edge equation is
cos(10°)(x+80) - sin(10°)(y-40) = 20.

The C# test additionally covers weighted translated terrain, nested fixed/relative
scopes and complete 17-node/11-route regional reselection with optional width
adaptation. Invalid inputs, original attempt/work limits, cancelled generation,
altered sources and checkpoint replay are checked. These tests do not close
whole-world strategy synthesis, streaming/scale or visual acceptance. Godot and
artwork remain deferred.

[Verification](../../../docs/evidence/2026-10-29-regional-frames-verification.json).
