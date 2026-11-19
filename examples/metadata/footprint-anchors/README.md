# Source-footprint allocation anchors

These small analytic inputs verify geometry through the normal metadata engine.

- `slanted`: a 4×2 source rectangle cannot fit at the intended `(10,9)` activity
  anchor. Source-derived translation contacts find `(9,8)` inside the slanted
  domain, retaining the exact rectangle and its eight square meters.
- `hole`: the same source would overlap a protected hole at `(4,6)`. With the
  explicitly declared 0.0001-meter clearance, the accepted anchor is within
  floating-point operation precision of `(2.9999,6)`. The hole and source remain
  intact. Geometry closure alone does not override forbidden terrain boundaries.

`AllocationAnchors[].Footprint` names one root demand and its finite search
policy. It is exclusive with the existing `Interior` mode. The footprint
resolver supplies source rotation and reservation; C# verifies the full union,
outer boundary and declared clearance. Counters, selected contact provenance and
source-envelope hash are saved and rederived on read/export.

Focused checks also cover source rotation, both engine coordinate limits,
candidate/contact/work exhaustion, oversized sources, conflicting modes,
cancellation and tamper refusal. Candidate generation is finite; no continuous
feasibility or global-optimality proof is implied. These are technical fixtures,
not artwork or complete game worlds.

See [API and limits](../../../docs/WORLD_METADATA.md#footprint-aware-allocation-anchors)
and [verification](../../../docs/evidence/2026-10-29-diverse-programs-verification.json).
