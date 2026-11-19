# Solid occupancy and entrance access

Three required 8 × 8 meter structures have north-edge entrances, each 2 meters wide with a 6-meter outward approach. Positions are constrained by authored anchors so an independently known obstacle blocks the straight route between the left and right entrances. C# must detour around the middle structure at full traversal width. A separate destination network uses the same named entrance geometry and obstacle surface.

These are authored test dimensions and constraints, not art measurements or a finished town. The four metadata nodes retain their centers; external routes end at approach points. Gold dashed legs show declared entrance approaches. Solid footprints are distinct from traversable parent reservations.

Use the engine's `generate-metadata` command with `recipe.json`, `catalog.json` and a fresh output directory. The [reviewed packet](../../../generated/physical-access-reviewed/schematic.html) contains actual engine output; [verification](../../../docs/evidence/2026-10-26-world-access-verification.json) records the tests and browser review.

Capacity, full catalog siting policy, automatic placement redesign after route/access failure, corridor merging, junctions, production art and native Godot acceptance remain open. Route costs currently describe external segments separately from the entrance legs.
