# Integrated coastal world reference

This framework fixture exercises the existing shared world pipeline. It is not
a configured game. The reusable catalog supplies seven compound envelopes,
four buildings per compound and two equipment members per building: **94
metadata nodes and 91 physical sources**. Dimensions remain in meters; the
engine expands the hierarchy, transforms the source geometry and calculates
allocations, entrance connections, routes and destination networks.

The irregular world boundary contains lowland ground, a continuous hillside,
one district-local grid, separate hillside power allocations and an excluded
undeveloped shore. Four planned compounds have a supply network that survives
one logical connection loss; three hillside compounds have a footpath network.
Three explicit interfaces connect required local and cross-district journeys.
Every building has a bidirectional connection to its compound entrance. This
produces 31 authored route pairs plus six generated network edge pairs.

`recipe.json` and `catalog.json` are the baseline inputs for `generate-metadata`.
`water-recipe.json` blocks the baseline regional crossing: generation selects
another valid crossing without changing any content or source positions.
`roadless-recipe.json` omits surface routing/network policies while preserving
all buildings, equipment and logical relationships. Allocation boundaries do
not become streets. `CompleteDeclaredScope` describes this explicit recipe;
it does not establish an entire game, production artwork or arbitrary scale.

The native `--world-coastal-mixed <new-output-directory>` scope generates and
replays the three packets. The independent JavaScript scope
`tests/javascript/world-coastal-mixed.test.cjs`, with `WORLD_COASTAL_MIXED`
pointing to that directory, checks all sources, hierarchy, full passage widths,
protected shore/water, preserved geometry and exact schematic consumption.
The native authoring fixture owns these example inputs; no generated ownership
receipt or historical world packet is embedded in them.

[Verification](../../../docs/evidence/2026-11-02-coastal-mixed-world-verification.json)
records current CLI/SDK equality, the actual browser review and SVG export,
the initial authoring error and unchanged resource limits. Godot and artwork
remain deferred. Broader generator qualification remains open.

`bounded-recipe.json` is the connection-regeneration variant. West and east
interface rules and networks declare their physical routing polygons. Global
compound and cross-district connections keep global dependencies.
`route-policy-edit.json` changes only the compound-entry ascent cost;
`route-budget-edit.json` intentionally fails with an insufficient aggregate
budget. Both edits target an initial session created from `bounded-recipe.json`,
this catalog and the empty `protections.json`. The public CLI flow has been
executed: its initial world/checkpoint and policy world/execution report match
the SDK exactly, and the failed proposal creates no output.

The `--world-route-memo` native scope owns this variant and checks complete
coastal edits, no-op retention, bounded/disjoint and boundary-contact changes,
immutable cache returns, original limits, rollback, cold replay and independent
domains. Its private cache does not enter world metadata; execution reports
record actual searches and reused logical work separately. Allocation and global
validation can still rerun. [Regeneration evidence](../../../docs/evidence/2026-11-02-route-granularity-verification.json).

`placement-edit.json` targets an initial metadata session created from
`roadless-recipe.json`, `catalog.json` and `protections.json`. It moves one
compound one meter, retaining its four buildings and eight equipment members.
The session computes thirteen physical geometry evaluations and reuses the
other 78. Current collision/access/assignment checks still run, and original
logical work is charged on hits. The public CLI initial world/checkpoint and
moved world/execution report match the SDK exactly. Source-size and environment
edits, live callback/contact/nogood refusal, budgets, clone isolation, cold replay
and failed-edit preservation are covered by `--world-placement-memo`. Use
`WORLD_PLACEMENT_MEMO` for the independent JavaScript packet checks.
This qualifies geometry-evaluation reuse, not whole-search reuse or arbitrary
world-scale performance. [Placement evidence](../../../docs/evidence/2026-11-02-placement-evaluation-verification.json).

The adaptive-layout controls keep the same complete source program on four
terrains. Use the baseline `recipe.json`, `protected-hole-recipe.json`,
`cliff-recipe.json` or `lagoon-recipe.json` with the existing `catalog.json`.
The changed terrains remove exactly 64, 108 and 409 square meters of suitable
ground, while retaining every source position/dimension and required journey.
They change actual region/allocation shapes; the local grid stays in the west.
`resource-recipe.json` with `resource-catalog.json` uses the same physical
variants for resource/interaction roles and emits no roads. Select the matching
catalog explicitly for each variant. Economy and runtime interaction require
their own declared behavior. All six native SDK/CLI worlds match exactly,
including the owning-repair case under `examples/metadata/joint-layouts/one-owner-repair`.
The input boundary rejects unknown recipe/catalog root fields, including renderer
settings; presentation calibration belongs in its downstream profile.
