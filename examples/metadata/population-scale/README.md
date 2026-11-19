# Physical population scale on irregular terrain

These three framework declarations request **256, 512 and 1,024 complete physical
resource members** in the same irregular habitat. The existing C# area grammar
derives each position from a positive authored density field and its equal-mass
stratum, with a declared 27° analysis axis. There is no random scattering or
mandatory city/street grid. The catalog retains a 0.6 × 0.2 m solid source and its
0.8 × 0.4 m clearance reservation for every member.

Placement remains prescribed at the generated anchors with 0.5 m minimum
separation, a 20M root-placement work limit and 200M aggregate placement limit.
Source hierarchy, full footprints, field density and chunk references remain
explicit. `1024/changed-density.json` reverses the density gradient; the source
count and dimensions remain fixed while positions change.

## Shared engine improvement

The existing placement owner now indexes each demand's incident relationships.
Prescribed candidates have a conservative immutable bounds hierarchy; branches
are omitted only when strict box distance proves separation. Anchor points are
included in the bounds, and exact contact/overlap/clearance checks remain at the
original polygon owner. Candidate witnesses retain their original demand order.
Search/growth/provisional candidate handling remains dynamic.

Assignment access validation prepares each physical site's geometry once. The
same opening/blocking predicate checks every possible solid/port interaction in
its original order. A pair with no possible port interaction needs no access
check; footprint collision remains a separate mandatory constraint. No source,
relationship, port, required member or clearance rule is dropped.

## Observed results

One Windows/.NET 10 machine, identical input files and unchanged limits:

| Physical members | Placement work before → after | Transient allocation before → after |
| --- | ---: | ---: |
| 256 | 2,663,414 → 1,325,161 | 199,261,760 → 134,972,576 bytes |
| 512 | 8,070,231 → 2,655,700 | 455,298,120 → 195,059,160 bytes |
| 1,024 | 27,440,457 → 5,633,943 | 1,361,993,576 → 323,282,416 bytes |

At 1,024 members the observed generation time changed from 4.571 to 1.731 seconds.
These are single-process measurements with warm-up/machine variation, not a
throughput guarantee. Allocation is total managed allocation during generation,
not live memory; peak working set is a process lifetime high-water mark. Complete
node/site/stratum/chunk geometry remains identical to the frozen baseline.

The complete 1,024-source case also publishes, edits the density field, matches
clean generation, preserves accepted state after oversized/cancelled proposals
and replays its source-history checkpoint. Independent exact polygon moments,
density integrals and rectangle distances check 3,840 strata and 1,734,784 source
separation pairs across the scale/counterfactual packets. Solid and combined union
areas are 122.88 and 327.68 m²; these source areas are not world-completion metrics.

The existing physical-access/prescribed limit remains 1,024 sites. A 1,025-member
request refuses without output, preserving the previous packet. These technical
resource populations qualify supported finite scale; they do not establish a
complete biome/game design, arbitrary-size streaming, art or Godot behavior.

[Exact commands, source identities and regression results](../../../docs/evidence/2026-10-30-population-scale-verification.json).
