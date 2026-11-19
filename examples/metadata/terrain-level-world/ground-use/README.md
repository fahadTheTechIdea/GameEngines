# Explicit ground uses in the complete terrain and level program

This variation retains all four domains, 160 nodes, 156 exact physical sources,
48 rooms and 48 fixtures from the enclosing example. Traversal radius remains
0.3 m everywhere. Counts, source dimensions, placement and route budgets are
unchanged.

All nine exterior ground areas explicitly require at least 80% of their source
polygon to remain free of solid source footprints. All 48 room sources explicitly
require 95%. Parent compounds stay unmarked: their reservations do not imply that
their interiors are deliberately open ground. These fractions are example design
choices, not framework defaults or rules for every game.

The engine publishes 57 ground-use observations per packet. It subtracts the union
of all solid source footprints, counts overlapping declarations once in aggregate
areas, and preserves exact source geometry. `review.html` in each domain explains
the declarations and measurements; `physical-accounting.json` retains the full data.
This source-area measurement does not subtract generated wall strips, water or
actor-clearance envelopes. The existing native terrain/interior/path constraints
qualify movement separately; it is not a walkable-area certificate.

`definition.json`, `coastal-edit.json` and `larger-fixtures-edit.json` support the
same CLI/session and public C# `--domain-sequence` flow as the enclosing example.
The coastline edit preserves all three levels. Enlarging all sixteen middle-level
fixtures reduces that level's open source area by exactly 8.96 m², without changing
the other domains or either required journey. The complete CLI and SDK worlds and
checkpoint bytes match; clean regeneration and restored history also pass.

`unsatisfied-ground-edit.json` instead requires 100% open source area in those
occupied middle-level rooms. Their contained fixture sources now prove the shortfall
before candidate search: the request fails with `GROUND_USE_CAPACITY`, publishes no
packet and preserves the accepted state. The exception retains a typed capacity
witness. Cases without that conservative proof still require exact final ground
accounting. The engine does not drop fixtures, resize sources or reinterpret a
failed constraint as success.

These remain independent coordinate domains connected through explicit portals.
No fitting building envelopes, 3D stairs, owner design acceptance, production art
or Godot behavior is claimed. The framework core plan remains in progress.

See [current qualification](../../../../docs/evidence/2026-10-30-ground-use-verification.json).
