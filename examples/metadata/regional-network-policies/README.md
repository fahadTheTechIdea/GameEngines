# Game-authored regional network policies

This framework reference composes current C# contracts through data. It adds
purposeful hierarchy scopes for two neighboring programs, seven source-sized
parent sites and fourteen separate physical stations. The whole boundary is
irregular; only the planned local allocations use a grid.

| Policy | Actual generation settings | Declared obligation |
| --- | --- | --- |
| Planned supply access (`street`) | Four scoped destinations; radius 0.6 m; east entrances; ground traversal; local alignment preference | Remain connected after losing any one selected logical connection. |
| Field paths (`footpath`) | Three scoped destinations; radius 0.3 m; west entrances; free-angle ground traversal | Connect every required field destination. |

An explicit walking passage joins the exact selected entrance positions at the
regional interface. Each profile retains its own width and costs. The 0.6 m-wide
walking passage does not grant wider agents traversal or convert movement modes.
No physical junction appears merely because route geometry intersects.

All four recipes use the same `catalog.json`, source sizes, node poses and solver
budgets:

| Recipe | Changed input | Verified result |
| --- | --- | --- |
| `recipe.json` | Both policies selected | Four supply links and two field links, plus three explicit interface connections. |
| `water.recipe.json` | A 1 × 1 m water patch blocks a previously selected supply passage | Supply geometry changes while all destinations, content and the connectivity obligation remain. |
| `without-supply-roads.recipe.json` | Omit the supply policy | No `street` connections; all 24 metadata nodes and 21 physical members remain. Footpaths and explicit walking passages remain. |
| `renamed-kinds.recipe.json` | Rename connection kinds to `supply-lane` / `scout-trail` | The same engine produces identical network geometry. |

`RegionNodeId` selects actual descendant destinations. `Scale` and connection kind
are descriptive metadata; they do not select hidden city/genre algorithms.
Behavior comes from the declared demand, traversal profile, entrance and graph
settings. Alignment preference is soft. The optional `RoutingBoundary` on a
`MetadataNetworkRule` declares a hard physical routing domain; the ordinary solver
keeps the entire passage envelope inside it, including around concave boundaries.

`bounded-regions.recipe.json` adds explicit west/east routing domains to the same
complete program and shared catalog. `bounded-regions-water.recipe.json` places
water on an actual selected eastern passage at unchanged source poses and budgets.
Eastern routes change; the western network remains identical. Editing this terrain
in a generation session reuses the western search while recomputing the eastern
one, with exact clean-generation world/work equality. Crossing or touching sources
remain dependencies. Removing water restores the original passages. These recipes
use the same CLI/API as the four unbounded policy examples above.

`WorldSurfaceQuery.RestrictTo` supplies the detached regional query. It retains
all intersecting source polygons in full. Sampled fields keep their original full
source buffers; shared conservative cell windows plus a one-cell halo control
regional terrain-feature reads and network memo dependencies. This is not minimal
triangle-level dependency inference. Parent stages and
publication validators may still execute. The source world keeps every physical
member; filtering disjoint query dependencies does not remove world content.


`bounded-sampled.recipe.json` uses the same complete program on an original
11 × 9 sampled terrain field. `bounded-sampled-height.recipe.json` edits an eastern
height sample at unchanged coordinates, counts, profiles and budgets. Walking
costs increase under the original permissive profile; its neighboring supply
search is reusable. The tested western counterpart reroutes the stricter supply
profile around an actual slope barrier. The shared window retains original lattice
coordinates/triangle ownership and all 99 source samples. Exact undo restores
saved values rather than relying on floating-point subtraction.
See [sampled qualification and limits](../../../docs/evidence/2026-10-30-sampled-regions-verification.json).

Use the existing `generate-metadata` CLI/API with one recipe and the shared
catalog. Normal output contains current metadata, physical accounting and the
JavaScript schematic, which offers SVG export. `WorldNetworkPolicyTests` exercises
generation, exact endpoint joins, scoped demand, narrow-port refusal, replay,
session changes and failed-edit rollback. Independent verification recomputes
graph connectivity and each selected-link failure, costs and complete passage
envelopes. Logical link-failure tolerance is distinct from physical-segment or
junction failure tolerance.

This is a complete declared technical reference scope, not an entire settlement,
urban land-use generator, traffic simulation or accepted game design. Physical
footprints cover 336 m² and clearances reserve 560 m² of the 7,887.5 m² domain;
allocated program area does not mean occupied ground. Artwork and Godot remain
deferred. See the [master plan](../../../docs/ADVANCED_WORLD_GENERATION_MASTER_PLAN.md)
and [policy verification](../../../docs/evidence/2026-10-30-network-policies-verification.json).
The bounded examples have separate [regional dependency qualification](../../../docs/evidence/2026-10-30-network-regions-verification.json).
