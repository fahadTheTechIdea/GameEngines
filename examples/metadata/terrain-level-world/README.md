# Terrain and connected levels

This framework example combines the complete composed exterior with three
independent physical levels. It preserves 160 nodes and 156 physical sources:
seven exterior compounds and their full content, plus 48 rooms and 48 fixtures.
Every local route and transition uses the same 0.3 m traversal radius.

`definition.json` is the baseline. `coastal-edit.json` changes only the exterior
and its population source surface. `larger-fixtures-edit.json` then enlarges all
16 middle-level fixture sources from 0.8 × 0.5 m to 1.2 × 0.8 m. Source input
hashes are recorded in `source-inputs.json`. Other source dimensions, all counts
and original work limits remain fixed. The original 24-room single-stage refusal
is not bypassed by this example.

Use the existing CLI `domain-session start` and `domain-session edit` commands,
or the public workflow example's `--domain-sequence` entry point. The saved
qualification runs cover CLI/SDK equality, clean regeneration and checkpoint
history replay. Required outward and return journeys contain 46 steps initially
and 45 after the terrain edit. The shoreline edit reuses 48 of 66 local stages;
the fixture edit reuses 52 of 66.

`too-wide-portal.json` and `blocked-terrain.json` are retained refused requests.
They require native endpoint-width and terrain-intent failures without a
published output packet.

The exterior and levels use independent coordinate frames connected through
explicit game portals. They do not establish fitting interiors inside the
exterior workshop, continuous stairs, 3D correspondence, owner design acceptance,
artwork or Godot behavior. Coverage remains explicitly partial.

See [qualification evidence](../../../docs/evidence/2026-10-30-terrain-level-world-verification.json).
