# S02 — Terrain, spatial domains and environmental layers

## Verified — supported-core terrain/domain gate (2026-11-02)

The original seven S02 criteria have bounded technical acceptance for current
authored/sampled surfaces, independent local domains and optional environmental
layers. Thirteen native checks, fourteen JavaScript checks, three actual CLI
exports and direct browser field inspection pass. The
[current seven-task evidence map](../../evidence/2026-11-02-s02-core-gate-verification.json)
records source/binary digests, commands, outputs and limits. Earlier dated
increments below retain their own historical qualification boundaries.

The existing viewer now exposes every derived/sample field declaration, including
unused ones, with purpose/source rules, units, bounds and exact source values.
Source-buffer extrema, declared clamps and saved rule-point observation ranges
remain distinct. Invalid source indices clear stale values. Browser testing
verified live index feedback, derived-control hiding and a usable map alongside
inspection. Presentation cannot change generated geometry. The contract states
the exact observation source and bounded detail count.

Current checks also qualify common mockup/metadata queries, absent-vs-zero
sampling, analytic interpolation, fixed shoreline geometry/provenance, protected
ridge/valley lattice samples, physical terrain counterfactuals, hard suitability,
overlapping floors and unsupported-frame refusal. Optional drainage/water/wind
checks retain independent conservation/geometry evidence, protected inputs,
dependency invalidation, clean-edit equality and saved history/rollback.

This accepts supported stage contracts, not the professional-generator goal or
unbounded terrain quality. Arbitrary physical volumes remain unsupported; terrain
protection has the documented interpolation boundary. Continue remaining original
stage acceptance in the master tracker. Godot and artwork remain deferred.

## Joint preferred-anchor reconsideration — 2026-11-02

For a shared partition with preference choices and declared relationship
feedback, the bounded area search can now reconsider earlier preferred anchors
when a later stratum exhausts its choices. It retains fixed positions, uses the
existing work, anchor-cell and partition-attempt budgets, and only advances on
semantic refusals. Reconsidered downstream partition attempts remain recorded
as superseded. Existing no-preference searches keep their prior path. This is
finite candidate backtracking, not a continuous or global optimum.

`--world-partition-preferences` at
`generated/partition-joint-anchor-confirmed-20261102` passed an analytic case
where a fixed later anchor required changing the first region's preference
rank, plus exact-work and insufficient-work checks. A three-region physical
world kept gamma pinned at `(88, 10.073895879458181)`; beta selected rank 7,
and their distance was `46.7350193483108` under the maximum `50`. The same
world retained 11 nodes, nine physical sites and its network, and replayed.
An independent Node check verified the saved points, rank, distance and count.
SDK and CLI world data matched SHA-256
`786A19E066DEAF42E6BE4BDBD744E0DF17EC05276BFD555289CF8F54171B1E52`.
Shared-partition and pinned-anchor regression suites passed. Build retains the
existing CS8602 warning at `WorldGroundUseCapacityTests.cs:133`. The
[framework input](../../../examples/metadata/shared-partition-preferences/pinned-related-recipe.json)
has partial declared scope. Broad world design and Godot remain open.

## Relationship-aware preferred-anchor retry — 2026-11-02 (joint search extended above)

When the selected anchor in a shared partition fails an existing semantic
qualification, the area owner now retries that stratum's remaining qualified
preference candidates in deterministic score order. It preserves hard terrain,
clearance, positive-density, work and anchor budgets; only semantic refusals
advance the search. `AnchorPreferenceRank` records the zero-based chosen option.
This does not revisit earlier strata after later candidates are exhausted and is
not exhaustive joint optimization. Previously saved preference-area receipts
must be regenerated for the new recorded candidate rank.

The `--world-partition-preferences` test at
`generated/partition-anchor-retry-confirmed-20261102` passed an analytic case
whose best points were 5 units apart while the required distance was 8–11; the
selected alternative gave distance 10 and rank 3 without changing the declared
partition. A three-region physical world retained 11 nodes, nine sites and its
required network. Independent inspection confirmed the original first-choice
distance `57.3293` exceeded the declared maximum `50`; the selected rank-1
anchor produced distance `44.2805`. Source publication/replay, SDK/CLI matching
SHA-256 `4B2CEA3BA3E0A933C92C2A329192D09EFB2EDA1AB61D90FD49EACF3DEBAFA72B`,
and shared-partition and protected-pin regressions passed. The framework example
is [related-recipe.json](../../../examples/metadata/shared-partition-preferences/related-recipe.json).
The declared game scope is partial; broad world design and Godot remain open.

## Preferences inside shared partitions — 2026-11-02

The shared region-partition owner now selects preferred anchors inside each
actual generated stratum before evaluating distance and shared-boundary
relationships. Distinct soft preferences are allowed when profile identity and
hard rules match; the per-region profile is retained. The same bounded point
search is reused, with full clearance, hard suitability, positive-density,
work and cancellation checks. Partition boundaries/component choices are not
globally optimized by preference, and conflicting hard profiles remain refused.

On Windows/.NET 10, `dotnet run --project tests/WorldCreator.Tests --
--world-partition-preferences generated/shared-partition-preference-confirmed-20261102`
passed two distinct preferences within a shared continuous component, point
containment, clearance, recorded scores, final-position relation checks, hard
profile mismatch refusal and cancellation. A three-region physical framework
example retained 11 nodes, nine physical sites and its required network; saved
world/source replay passed. Independent coordinate calculations compared it to
the clearance-only regression: scores changed from `0.8353` to `0.9177`,
`0.8353` to `1`, and `0.6830` to `0.88`. The separate single-anchor and shared
partition regressions passed. SDK/CLI world data matched SHA-256
`4B422EE3503701D2EB3EB1F4D0916DFEA2FD88C2E650BBC91ED29F1EA1C3C0C2`.
CLI correctly reported `PartialScope`; that is not a complete game. The build
retained the existing CS8602 at `WorldGroundUseCapacityTests.cs:133`. See the
[framework example](../../../examples/metadata/shared-partition-preferences/README.md).
The separate run `generated/shared-partition-preference-relation-20261102`
also confirmed that an impossible required relationship is refused.
Broad world quality and Godot remain open.

## Continuous-cell preference candidates — 2026-11-02 (shared partitions extended above)

The preference strategy now evaluates inset points between each convex terrain
cell's vertex-average point and its vertices in addition to the clearance anchor
and cell center. This allows an explicitly authored field preference to select a
different point even when one terrain cell covers the entire suitable region.
Hard eligibility, required clearance, deterministic ordering and the existing
anchor/work budgets remain mandatory. It is still a finite sample, not a
continuous optimum or a shared-component partition strategy.

The one-cell analytic plane chose `(15, 2.5)` instead of `(10, 5)` for the
declared eastward preference. The four-region physical counterfactual under
`generated/preferred-anchor-inset-20261102` improved its independently checked
terrain score from `0.924763671875` to `0.9930666666666668`, retaining the same
authored programs, all 20 nodes, 12 physical sites and three two-way routes.
Publication and source replay passed. A repeat in
`generated/preferred-anchor-inset-confirmed-20261102` checked clearance and
anchor-budget refusal; CLI and SDK world data matched SHA-256
`1FCCC5A1E3AD571904E1CF4DCF9EB2BBD4CFF483BD6BBFB45A837E536975C71B`.
Wider terrain/design qualification and
shared-component preference handling remain open; Godot deferred.

## Opt-in terrain-preferred regional anchors — 2026-11-02 (shared partitions extended above)

An authored region intent may opt into ranking qualified component anchors by
its existing point-suitability preferences. It considers the clearance anchor
and the vertex-average point of each component cell; hard field eligibility,
minimum clearance, finite work and anchor-cell limits still apply. Candidates
are deterministic and witnesses record the selected point score. This is a
finite candidate search, not a guarantee of globally best terrain, continuous
optimization, or complete world design. Pins and shared-component
`IntentPartition` cannot be combined with this strategy. Regional program
policies expose the same control to the shared world pipeline.

The analytic two-component terrain case selected the higher-scoring component
and an independent hard height limit still excluded forbidden ground. The full
four-region test `dotnet run --project tests/WorldCreator.Tests --
--world-preferred-region-anchor generated/preferred-regional-anchor-final-20261102`
generated and replayed a world where the selected anchor moved from
`(18.994140625, 60.8935546875)` to `(30.75, 57.2)`. Independent evaluation of
the declared plane and preference measured score `0.924763671875` to `0.9644`;
the world retained 20 nodes, 12 physical sites and three two-way routes. A second
run at `generated/preferred-regional-anchor-confirmed-20261102` also verified
that both cases selected the same authored content option. The
SDK and CLI world files matched SHA-256
`BCA7B57DC4E1DF33B8342F99DE066DCE023697345B38E802B3CA4F0AAABC098B`.
The existing blueprint regression passed; the build retains the existing
CS8602 warning in `WorldGroundUseCapacityTests.cs:133`. The
[framework example](../../../examples/metadata/regional-design/preferred-anchor-request.json)
declares the preference explicitly. Wider terrain, multi-intent shared-component
preference optimization, broad world-design review and Godot remain open.

## Verified increment — flat-core terrain influences (2026-11-02)

Support an optional inner radius on the existing authored landform operation,
so hills, depressions, ridges and level areas can have a full-strength interior
and bounded transition to the outer radius. Both radii use the transformed
distance; omitted values preserve prior samples; invalid radii fail early.
Verify analytic samples and falloffs, transformed paths, detail and level
behavior, windows and physical-world use. Godot is deferred.

`WorldLandformOperation.InnerRadius` now defines a full-strength interior in
the same rotated/stretched distance metric as its outer radius. `Level` reaches
the target at sampled core nodes; `Add` adds the full amount to the existing
terrain, which may itself vary. A positive radius is included in terrain-window
work accounting and the JavaScript saved-schematic validator. Omitted or zero
radius reproduces the prior samples; negative, nonfinite and outer-or-greater
values reject. Analytic tests cover all three falloffs, level-over-detail,
transformed polylines, sample windows and replay. A captured physical world
retained its required site and two-way route, changed its terrain samples and
replayed. SDK and CLI world JSON matched byte-for-byte; CLI verification and
the saved-schematic work-witness check passed.

Evidence: `--world-terrain-falloff generated/flat-core-terrain-final-20261102`,
`WORLD_FLAT_CORE_TERRAIN=generated/flat-core-terrain-final-20261102 node --test
tests/javascript/world-flat-core-terrain.test.cjs`, existing directional-landform
JavaScript regression against the same output, and `generate-metadata` /
`verify-metadata` using `generated/flat-core-terrain-cli-20261102`. CLI build
passed without warnings; the test build reported the existing CS8602 warning at
`WorldGroundUseCapacityTests.cs:133`. The first integration attempt failed
because its test incorrectly read a captured field as a full field; the corrected
test reads the actual terrain window. Core terrain source SHA-256
`F557A6799899C4A47D029ED12C36BA915FB8B492776246A56900DBDF0A7023CF`.
This is one terrain capability and one bounded world, not completion of S02 or
the professional world generator.

## Verified increment — directional landform influence (2026-11-02)

Allow each authored hill, valley or polyline landform to rotate and stretch its
distance field, independently of terrain-detail noise. Preserve omitted identity
output, operation ordering, Add/Level falloff and the same metric for optional
detail protection. Account for transformation work; preserve original-lattice
windows and bounded validation. Verify independent ellipse/segment samples,
source replay, changed physical-world terrain and paths, and current schematic
work accounting. No genre preset, fixed world grid or runtime is introduced.
Godot deferred.

Per-operation `OrientationDegrees` and `AxisStretch` now transform the distance
metric for point and polyline landforms, including detail protection. Independent
samples verify stretched and rotated hills, ridge distance, original-lattice
windows and protection. Identity and full-turn transforms preserve the original
heights. A physical world retains its site and two-way journey while rotating
the same landform changes the sampled terrain and actual route cost. Its saved
terrain window replays and passes schematic work validation; SDK/CLI worlds
match. Previously generated no-transform heights, nodes and routes matched.

Evidence: `--world-terrain-falloff
generated/terrain-landform-direction-current-20261102`, with
`WORLD_DIRECTIONAL_LANDFORM` pointing there for `node --test
tests/javascript/world-directional-landform.test.cjs`; CLI output is
`generated/terrain-landform-direction-cli-current-20261102`. The CLI build passed
without warnings; the test build reported the existing CS8602 warning in
`WorldGroundUseCapacityTests.cs:133`. This is one authored terrain strategy and
one physical fixture, not complete world-design or terrain-quality acceptance.

## Verified — game-authored landform falloff (2026-11-01)

Allow a landform operation to choose smooth, linear or quadratic radial falloff
without a genre preset. Keep omitted falloff numerically identical to the
existing smooth curve. Respect existing operation order, add/level semantics,
protection, terrain windows and work limits. Verify analytic samples for each
curve, explicit/omitted smooth equality, invalid input, deterministic windows,
and a complete physical world with terrain-responsive paths. This is a bounded
terrain capability, not full biome or game-world design. Godot remains deferred.

The three curves pass independent radial samples for both Add and Level,
polyline distance, protected detail and source-window replay. Two physical
worlds retain the same source and both journeys; route ascent cost changes from
0.015819865579 to 0.006639731158. Session edit/replay and invalid-edit rollback,
SDK/CLI equality, strict CLI refusal and schematic checks pass. Existing terrain
and protection regressions pass. Saved source-bound packets can require
regeneration when Core identity changes. [Example](../../../examples/metadata/terrain-landform-falloff/README.md),
[evidence](../../evidence/2026-11-01-landform-falloff-verification.json).

## Verified — directional terrain detail (2026-11-01)

Allow game-authored rotation and axis stretch of the seeded terrain-detail
coordinate domain. Use world-coordinate transforms so independently generated
windows and shared samples agree; preserve the existing output for omitted or
identity controls. Validate finite bounds and shortest wavelength along both
axes; retain landform protection, source geometry, budgets, replay and failure
behavior. Verify the coordinate transform independently and exercise a complete
physical world. This is terrain detail, not a complete biome or world design.

Orientation and axis stretch now transform world-coordinate sampling while
preserving exact omitted/identity behavior. Sixteen coordinate-reference checks,
shared windows, bounds and protected terrain pass. A complete nine-node physical
world preserves its sources and routes through edit/replay and invalid rollback.
The saved viewer now also includes both protection and transform work when
validating captured terrain; both directional and protected-only captures pass.
[Example](../../../examples/metadata/terrain-directional-detail/README.md),
[evidence](../../evidence/2026-11-01-directional-terrain-detail-verification.json).
Browser and broader world-design gates remain open.


## Verified — clamped-field suitability matrix (2026-11-01)

Compare generated suitable regions with independent analytic areas across
requirements below, at, inside and above clamped field bounds. Include lower
and upper plateaus, empty requirements, a multi-profile world and a real
sub-resolution strip. Retain explicit refusal for unrepresentable geometry and
the terrain roughness integration; do not enlarge the geometry tolerance.

Twenty-three generated ranges match independent analytic areas; two empty
requests refuse. Two disjoint profiles, intersecting hard requirements and
both one-sided clamp plateaus pass. A genuine 1e-10-wide region still refuses
instead of disappearing. The three physical terrain worlds and independent
triangle oracle also pass on the current engine. [Evidence](../../evidence/2026-11-01-clamped-field-matrix-verification.json).
This bounded matrix does not certify arbitrary numerical geometry.


## Verified — avoid redundant clamped-field splits (2026-11-01)

The terrain roughness counterfactual exposes a duplicate split at a field clamp
boundary, with near-zero signed residuals at vertices created by that same clamp.
Skip suitability cuts at or beyond declared field bounds: the environment owner
has already partitioned and clamped those pieces. Keep ordinary threshold cuts,
real small geometry, numerical refusals, all source sizes and work budgets.
Require the previously refusing middle input to generate and replay, preserve
low/high outputs semantically, and compare hard suitability against an independent
area calculation. Check general region, clamps, genuine thin gaps and cancellation.
This is a bounded numerical correction; Godot remains deferred.

All three settings now generate; an independent triangle-clipping oracle confirms
suitable areas 232.597439217928, 239.999829418118 and 232. Exact clamp plateaus,
interior thresholds, tiny-geometry refusal and a real 1e-11 gap remain verified.
All required sources and routes survive, source edits/checkpoints and invalid-edit
rollback pass, and SDK/CLI agree. Previous accepted cases retain source geometry,
areas and paths; region work changes, so affected saved plans require regeneration.
[Example](../../../examples/metadata/terrain-roughness-region/README.md),
[evidence](../../evidence/2026-11-01-terrain-clamp-boundary-verification.json).


## Historical — terrain roughness counterfactual before clamp correction (2026-11-01)

Exercise the octave controls through a terrain-qualified region and required
physical destination with a full-width connection. Keep catalog, suitability
and movement requirements fixed; compare authored roughness variants, record
any infeasibility, and independently sample the resulting terrain. Verify actual
region/placement/path changes rather than just a changed height buffer. Source
size, required content and budgets remain fixed; no decorative filler or terrain
realism gate is introduced. Godot remains deferred.

Low/high persistence keep the same required source and paths but change suitable
area from 232.597439 to 232 and forward ascent cost from 0.0445293 to 0.0780264.
Independent cell-area and 804 sampled centerline-slope checks, SDK/CLI source
replay, edit and rollback pass. The centerline and source pose remain unchanged.
**Historical failure, resolved above:** the middle setting, a valid declaration, refused at region clipping
with `WORLD_REGION_GEOMETRY_RESOLUTION`: raw area 2.43e-18 with signed values
near floating-point precision. The error now retains input vertices and signed
values for SDK diagnosis. That earlier run did not establish a safe geometry repair; its evidence is retained.
[Reproducible cases](../../../examples/metadata/terrain-roughness-region/README.md),
[evidence](../../evidence/2026-11-01-terrain-roughness-region-counterfactual.json).


## Verified — configurable multi-octave terrain detail (2026-11-01)

Allow game-authored persistence and lacunarity in seeded terrain detail, with
finite validated bounds and existing defaults. The old recipe and explicit
default controls must yield identical heights and work. Independently verify the
weighted octave equation, source-resolution/windows, seed determinism and
protected landforms; run a complete terrain-aware physical program and preserve
its required source sizes, routes and transactional replay. Reject malformed
controls and work exhaustion without publication. This adds terrain variety,
not a claim of complete game-world design or Godot integration.

Persistence and lacunarity now control normalized octave weights and wavelengths.
The original/default field remains exact. Weighted-octave equations, sampled
windows, protection, bounds, source edit/checkpoint/failure and a complete
nine-node physical world with three bidirectional routes pass. Prior/current
default and SDK/CLI outputs match; invalid CLI input publishes nothing. This
qualifies the declared terrain controls, not terrain appearance or a whole game.
[Example](../../../examples/metadata/terrain-roughness/README.md),
[evidence](../../evidence/2026-11-01-terrain-roughness-verification.json).


## Verified increment — protected terrain world integration (2026-11-01)

Exercise the protected-detail terrain provider with complete nested structures,
irregular regions and required bidirectional connections. Compare two detail
seeds on the same authored protected route corridor, preserving declared source
sizes and counts. Verify physical routes, exact terrain/sample effects and
source-package replay. This qualifies the integration, not every world or genre.
Status: BOUNDED INTEGRATION VERIFIED.

Both terrain seeds retain all nine nodes, eight authored physical sources and
three bidirectional paths. Full-width route checks include the inlet and physical
obstacles; both source packages replay. CLI and SDK output match. Independent
checks verify 369 fixed corridor samples and changed surrounding samples.
The full original stage and professional-world gate remain open.
[Example](../../../examples/metadata/protected-terrain-compounds/README.md),
[evidence](../../evidence/2026-11-01-protected-terrain-world-verification.json).

## Verified increment — protected landform detail (2026-11-01)

Allow authored landforms to protect a corridor around their control path from
procedural detail, with a smooth transition to ordinary detail. Landform heights,
paths and units stay authored; terrain outside the transition may vary by seed.
Preserve existing recipes, original-lattice windows, cancellation and charged
work. Verify ridge/valley behavior, deterministic seeds, transition equations,
invalid declarations and shared-window equality. Status: IMPLEMENTED AND BOUNDED VERIFIED.

Authored paths now protect a fixed-radius corridor from procedural detail,
with smooth fade to each landform radius and strongest-mask overlap. Two saved
worlds, independent sample equations, original-lattice window replay, exact work
and cancellation pass. Unprotected prior/current metadata matches exactly. The
original S02.3 and broader world-design requirements remain open.
[Example](../../../examples/metadata/protected-landforms/README.md),
[evidence](../../evidence/2026-11-01-protected-landform-detail-verification.json).

## Current acceptance audit — S02.1 and S02.4 (2026-11-01)

Requalify sampling/domain semantics and terrain-driven hard suitability on the
current Core. Independently distinguish all five surface statuses, genuine zero
height, negative origins, affine gradients and both sampled-cell triangles.
Unavailable samples must retain absent fields, rejected eligibility and absent
scores. Exercise separate water/slope counterfactuals and their combination in
one complete physical declaration: retain exact source geometry and required
travel while eligible area and generated destination change. A perfect weighted
preference must not override a hard rule or an excluded hole. Recheck existing
surface/environment/sampled/whole-footprint/region scopes, public persistence,
source edits, rollback and strict input refusal. Preserve the original task
acceptance, supported two-coordinate scope and remaining stage/world gates.
Status: VERIFIED for the original S02.1/S02.4 technical acceptance; no whole-world design approval is implied.

**Current — S02.1/S02.4 terrain contract acceptance verified (2026-11-01):**
The existing terrain/environment owners distinguish all five query statuses,
genuine zero elevation, absent coverage, negative-origin affine fields, sampled
triangles and mismatched units. Game-authored water/slope rules change usable
area from 240 to 168/144/72 m² and move generated destinations to four distinct
positions while preserving source dimensions and required bidirectional travel.
Perfect preferences cannot override hard fields or exclusions. Eight native
scopes, three saved-JS checks, four exact SDK/CLI packets and independent
sampling/area/source/path/provenance equations pass. Source edits/history and
failed-edit preservation pass; flooded/uncovered requests publish nothing.
The C# generation kernel is unchanged. The original S02.1 and S02.4 technical
acceptances are now verified; other 61 task and all 13 requirement statuses
remain. Broader terrain/domain/world design, browser/live-agent gates and
remaining original acceptance remain open. Godot/artwork deferred.
[Exact acceptance evidence](../../evidence/2026-11-01-terrain-policy-acceptance-verification.json).

**Current — independently generated shared-coordinate world regions (2026-10-31):**
The common metadata pipeline now generates a selected complete regional program,
persists its original-lattice terrain/source/build proof and assembles all declared
regions through the existing physical owners. Ownership polygons are independent
of storage cells. Whole seam-spanning sources retain one semantic owner and closed
neighbor references; different regions can reuse one recipe template/local IDs.
Current whole SDK, whole CLI and reversed saved-request assembly are byte-identical.

The complete10-node/6-source control retains nested members and actual outward/
return regional journeys; its richer edit retains11 nodes/7 sources and the exact
unchanged neighbor packet.4/8/16-region controls retain20/40/80 nodes,12/24/48
physical sources and6/14/30 directed regional journeys. Whole, reversed request,
declaration-order and complete saved replay match. Source dimensions, physical
sizes, counts and budgets remain declared; no grid or streets are imposed.

Eight current native scopes, six saved-JS tests, four CLI successes and seven
expected atomic CLI refusals pass. Independent equations verify2088 heights,
504 shared samples, all six baseline bodies, six directed paths and120 radius/
boundary checks. The same final harness fails on prior Core's missing final
cross-region dependency guard and passes on current Core. One existing test
warning remains; CLI is clean. Source/publisher/history failure attempts stay
preserved. Observed16-region generation allocated632956616 bytes in total and
the process reached192090112 bytes cumulative peak working set; these are
fixture observations, not universal performance or live-allocation ceilings.

Combined world rendering, regional transactional edit history, automatic macro
ownership/coupled repair, independent storage-cell generation, runtime streaming
and the original63-task/13-requirement core/design gate remain open. Godot/artwork
deferred. No whole populated-world or visual acceptance is claimed.
[Current source, commands and limits](../../evidence/2026-10-31-regional-content-verification.json).

**Previous qualification — persisted bounded terrain capture (2026-10-31):**
Metadata generation now opts into saved original-lattice terrain windows through
the existing surface, session and publisher owners. The complete24-node/21-source/
7-program world stores1584 rather than263169 samples, retaining its world extent,
source dimensions, physical widths and budgets. Current full/capture geometry,
authored/network paths, costs and route work match exactly; source edits, saved
replay, checkpoints, rollback, cancellation and tamper refusal pass. Neighboring
captures share144 exact original samples.

Fourteen focused native scopes, seven JS tests, seven saved full-world JS packets
and both Release builds pass. Independent checks verify267057 heights,36 directed
paths,112 path segments and7280 radius/boundary checks. Actual browser inspection
qualifies saved terrain display, site details and SVG export. One existing test
nullable warning remains; CLI is clean. Expected CLI domain/precision refusals
preserve failed outcomes and publish nothing.

Logical membership regions can union across field planes while preserving exact
eligibility and source provenance; terrain-face consumers retain the default.
Original samples and whole-footprint/route checks remain authoritative. Bounded
source scans, early hard-anchor filtering and correctly scoped child dependencies
remove unrelated work; no counts, sizes or budgets were relaxed. The retained
exact-zero datum still rejects an attributed sub-resolution clamp sliver; this is
an explicit limit, not a successful case. Historical attempts remain preserved.

At that qualification independent content generation remained open. The current
generation-region increment above supersedes that gap for its declared bounds;
independent storage-cell/runtime streaming and the full core/design gate stay open.
Godot/artwork deferred.
[Qualified source, commands and limitations](../../evidence/2026-10-31-metadata-terrain-capture-verification.json).

**Previous qualification — window-backed regional terrain queries (2026-10-31):**
The common height-query contract now distinguishes global lattice geometry from
full or bounded storage. Direct regional surface queries generate only required
original-cell windows/halos; immutable restrictions avoid full-field regeneration
and cloning. Both storage variants share interpolation, cell ownership and cuts.
An outside-field authored-platform regression now retains global geometry with
explicitly empty storage; the identical final harness fails on prior Core.

Nine current native scopes pass. Regional cases compare3375 exact samples,12
traces,16 footprints and four full-width routes with identical costs/work/audits,
plus the outside-field route control. Independent checks cover5920 heights,239
planar cost intervals and72 radius/boundary comparisons. Seven saved JS packets
retain24 nodes/21 physical sources and84 exact directed paths; three terrain JS
tests pass. Final Release builds pass with one existing test warning; CLI is clean.
The prior full CLI world stays byte-identical. No source dimensions, counts,
movement widths or budgets were reduced or raised.

At that earlier qualification metadata publication retained complete terrain.
The current capture increment above supersedes that persistence limit; independent
content generation remains open. All63
original tasks/13 requirements and whole-world design gates stay in progress;
Godot/artwork deferred.
[Qualified API, current source identities and limits](../../evidence/2026-10-31-regional-window-verification.json). This does not close the full stage.

**Current — independent terrain-window generation (2026-10-31):**
The existing terrain owner now evaluates requested original-lattice windows with
optional one-cell halos, saved replay and complete order-independent assembly.
Whole/window generation shares the same height evaluator; the old CLI world is
byte-identical. Independent equations cover264258 heights and9522 repeated seam
references. A399-sample request from a263169-sample source allocated101672 bytes
in the measured case; this is not a universal performance target.

Three current native scopes, three saved-JavaScript checks and both Release builds
pass. Tests retain one prior warning; CLI has none. Missing coverage, altered/stale
windows, source/count/work limits and cancellation refuse. Assembly cannot exceed
the source work bound. Source dimensions and work ceilings remain unchanged.
Metadata surface/routing still uses full fields; independent content chunks,
regional terrain consumption and broader world/design acceptance remain open.
The original63 tasks/13 requirements stay in progress; Godot/artwork deferred.
[Qualified API, commands and limits](../../evidence/2026-10-31-terrain-window-verification.json). This does not close the full stage.

**Current optional environment qualification — 2026-10-30:**
Current drainage/water/wind/retained-water and 513×513 field/lake cases pass at
original counts and limits. Independent checks cover every one of 263,169
sample/node coordinates, inputs, incoming flows and graph acyclicity, exact
basin area/capacity, wet-patch union and wind scalar integration. This qualifies
the declared surface/environment cases; it does not add volume, climate
simulation or broad design approval. [Current matrix](../../evidence/2026-10-30-environment-matrix-verification.json).
S02/core remain IN PROGRESS; Godot/artwork deferred.

**Partitioned world and source-history persistence — 2026-10-29:**
The existing publishers/readers now share a streaming JSON codec: ordinary
compact JSON up to 8 MiB, then versioned manifests with ordered SHA256-addressed
1 MiB parts. Per-file and authored-input limits remain 16 MiB; reconstructed
generated documents/checkpoints are bounded separately at 256 MiB/256 parts.
The reader authenticates every part and the complete stream before typed source
validation. No native sample, required member, source history or work rule is
omitted, and no executable cache is loaded from disk.

The captured dense publication failure now succeeds with the same 263169 terrain/
drainage samples, 32768 m² minimum, 32-face cap, original work declarations and
complete source receipts. Its 64037963-byte world uses 62 parts; standalone flow
replay matches the original captured data. A 23982652-byte checkpoint preserves
all 22 accepted requests and 23 full terrain buffers, including the initial source.
A large two-domain/six-node collection retains physical-network and logical
forward/return journeys through the same root/child storage owner.

Strict corruption/order/size/path/cancellation cases, rehashed semantic tamper,
independent byte/source/history/SVG checks and actual browser part loading,
selection/layers/fit/export pass. Human property review is an explicit bounded
preview; stored properties remain complete. [Exact sources, commands and limits](../../evidence/2026-10-29-partitioned-storage-verification.json).

S08/S10 and the professional generator remain IN PROGRESS. The recorded dense
monolithic-file failure is resolved. Next: consolidate broader terrain/content
and structural-diversity qualification against the existing capability matrix,
addressing observed design/scale failures. Transport is not runtime spatial
streaming or whole-game/design acceptance. Godot and artwork remain deferred.

**Dense terrain/environment qualification — 2026-10-29:**
Full 513×513 flat/four-slope/masked/scalar cases retain all 263169 native samples
while shared exact convex-face merging reduces redundant region representation.
Original 32-face/frontier and 200M-work declarations pass; protected holes,
separate terrain/scalar planes, clamp pieces and real one-ULP distinctions remain.
An independent rational oracle verifies every source sample, exact areas, no
positive overlaps and the complete oriented outline. Dense drainage/lake API
output retains four original bed planes and exact area/capacity, with actual boat
traversal. Numeric buffer edits preserve complete source and protected elements.
See [executed cases and limitations](../../evidence/2026-10-29-dense-regions-verification.json).
S02 remains IN PROGRESS; nonmergeable dense fields can still exhaust finite
budgets, and dense drainage file publication awaits partitioned persistence.

**Retained-water/lake-level geometry — 2026-10-29:**
The existing water owner now derives explicitly requested retained bodies from
retained drainage terminals, declared levels/bounds/kinds and strict positive
depth. Connected terrain extraction preserves distinct basins, islands, voids
and dry zero-depth saddles. Bed planes, depth/capacity and source receipts remain
separate from flat occupied water planes. Closed terrain shorelines are required
by default; explicit clipping retains unresolved/deficit witnesses. Rates do not
infer water volume or levels. Source terrain and existing stream provenance remain.

Analytic basin/level/island/capacity checks, actual region/exact-site/boat effects,
level/source edits versus clean generation, wind/route dependency closure,
failed/cancelled-edit rollback, strict publication and source-history restoration
pass. The larger level edit preserves 59 nodes/58 physical members, seven complete
compounds, 16 landscape members and 82 paths with the original 1.8m ridge,
25000 m² minimum, source geometry and budgets. A captured one-ULP contour seam
failure at level 1.17 was corrected using the common generated-event convention
and complete topology rebuild, retaining the requested level and all selected cells.

Seven affected C# scopes, current CLI, four JavaScript checks, actual browser
selection/layer/fit/SVG export and independent saved SVG coordinates pass.
An exact rational native-triangle oracle checks 12 saved cases/13 bodies, including
the full edited world's 52.559833024118575 m² area and 0.7883974953617774m³ geometric capacity.
See [exact source versions, checks and limits](../../evidence/2026-10-29-retained-water-verification.json).

S02/S08/S10 and the professional-generator objective remain IN PROGRESS. Next
qualification must widen terrain/domain/environment diversity and dense-field/
world-scale behavior through the existing shared owners. Dynamic storage/outflow,
broader retained-water policies and world/game design acceptance remain open;
production artwork and Godot are deferred. Technical schematics are not game art.

## Qualified retained-water geometry increment

Extend the existing water owner with explicitly requested retained bodies bound
to declared retained-basin drainage terminals and a game-authored level. Use the
shared terrain-connected region extraction for below-level footprint geometry;
preserve the exact original terrain and record bed planes, depth extrema and
geometric capacity separately from the flat water-surface plane. Rates do not
imply sufficient water volume. Default shoreline policy must refuse open or
unresolved boundaries; an explicit clipping policy may represent bounded water
designs. Disconnected below-level components must not be flooded by proximity.
Reject overlapping retained bodies, missing/wrong sinks, insufficient declared
receipt, invalid levels, unrepresentable geometry and exhausted bounds. Retain
existing stream behavior, source priority, water replay, metadata/session wind
dependency closure and required content. Qualify analytic basin area/capacity,
islands/masks, separated depressions, level edits, land/water traversal and exact
site constraints before claiming support. Godot remains deferred.

**Automatic wind recipe/session integration — 2026-10-29:**
Optional `WindMoisture` now derives after the effective occupied surface.
`wind-moisture` and `environment-input` stages preserve original authored
environment separately and feed existing region/allocation/suitability/path/
chunk consumers. Wind, evaporation, water and terrain edits invalidate the
dependent closure; authored-expression edits reuse wind derivation. Wind
reversal changes actual usable regions without dropping required destinations.

Source-bound receipts and environment markers replay on world/standalone read.
Publication also binds the original recipe's definition and authored environment,
rejecting mismatch before creating a directory. Population compilation preserves
source wind authority. Failed/cancelled proposals retain accepted state/cache;
edited worlds equal clean generation and complete source histories restore.

The larger automatic session retains 59 nodes/58 physical members, seven complete
compounds, 16 landscape members and all 82 directed paths across a wind edit.
Population-source and terrain-demand recomputation, clean equality, publication
and checkpoint restoration pass with the original 1.8m ridge, 25000 m² minimum,
source dimensions and work budgets. Eight affected regression scopes pass on
the integration binary; four are rechecked on the final publication-guard binary.
Current CLI, three JavaScript artifact checks and independent 25-node wind/field
composition checks pass. See [source revisions, checks and remaining gates](../../evidence/2026-10-29-wind-metadata-verification.json).

S02/S08/S10 and the overall generator remain IN PROGRESS. The later retained
water increment above now qualifies bounded lake geometry/levels. Wider domain,
environment, dense-field and scale/design gates remain open. No whole-game or
all-genres acceptance is inferred; Godot and artwork remain deferred.

## Qualified wind recipe/session increment

Acceptance: optional recipe `WindMoisture` derives from the effective occupied
surface and explicit authored environment, with saved original/effective
environment authority. Surface/water/wind/evaporation edits must invalidate the
actual dependent stages; unchanged content and base terrain may be reused.
Validate receipt, source surface, field composition and orphan markers on read
and export. Preserve population-source wind facts and existing constraints.
Require clean-edit equality, required-content retention, failed/cancelled-edit
rollback, finite bounds, standalone/world replay and checkpoint restoration.
Keep the full physical program at original dimensions/terrain/budgets. No wind
capability is required for other worlds. Godot remains deferred.

**Sampled environmental layers and wind-moisture core — 2026-10-29:**
Optional typed scalar grids now share terrain's triangle interpolation and
analytic gradients. The existing expression/clamp, whole-footprint, region,
density and exact-placement owners consume the same values. Distinct frames
and uncovered scalar queries refuse. A hidden wet-pocket test independently
predicts 14.08 m² dry area from a 16 m² domain; exact wet sites reject.

The optional C# wind-moisture grammar derives humidity/rainfall with explicit
direction, boundary humidity, condensation, elevation-rise and arbitrary-kind
evaporation controls. Strict upstream ordering, per-node ledgers, source terrain,
replay and finite budgets are checked. Air spans the terrain rectangle; ground
masks still gate consumers. It is a normalized game grammar, not physical global
atmospheric mass conservation. No ocean or terrain edits are introduced.

The larger sampled-field world passes with 59 nodes/58 physical members, all
seven complete compounds, 16 density-funded landscape members and 82 paths.
Independent barycentric integration matches 3664.285713902405 m² support and
4203.305397515091 mass, including a positive humidity contribution. Nine affected
C# regression scopes, the existing 59/60-node edit/rollback/checkpoint lifecycle,
current CLI and three JavaScript artifact checks pass. See [checks, source snapshots and exact boundaries](../../evidence/2026-10-29-environment-wind-verification.json).

**Historical core-only boundary:** the earlier increment accepted generated
scalar snapshots as authored inputs. Automatic wind derivation, original/effective
environment receipts and stage invalidation are now qualified above. Lake levels,
wider environment/domain variety and scale/design gates remain open; Godot deferred.

## Active sampled environmental derivation increment

Acceptance for the next S02.7 implementation: optional typed sampled scalar
layers share terrain's exact triangle interpolation and frame, retain units and
source provenance, and feed the existing field DAG, clamp, whole-footprint,
region, density and placement owners. Distinct scalar frames refuse until a
joint subdivision implementation exists; no sparse sampling may certify area
constraints. Bound fields/samples/work and preserve cancellation/input snapshots.
An optional directional moisture grammar will process only upstream neighbors
in strict wind projection order, accept explicit boundary humidity/evaporation
and game-authored condensation controls, retain original terrain, and replay
its saved results. No implicit ocean, terrain edits or terrestrial climate claim.
Qualify analytic interpolation/gradients, hidden wet pockets, clamps, varied
wind/ridge/source cases, missing coverage, JSON/tamper, downstream physical
effects and clean-edit equivalence. Recipe wiring and browser/native acceptance
remain unqualified until executed. Godot stays deferred.

Research basis: Mapgen 4's `assignRainfall` processes regions in wind projection
order and aggregates strictly upstream humidity (source checked 2026-10-29 at
https://github.com/redblobgames/mapgen 4/blob/main/map.ts). This is a conceptual
reference; the proposed controls, shared square-field triangulation and explicit
mask/source semantics below are this framework's own game grammar, not a port.

**Physical water-world qualification — 2026-10-29:**
The previously failing larger water-bearing reference now generates, validates,
publishes and strictly reads back: 59 nodes/58 physical members, seven complete
compounds with 14 structures/7 yards/14 caches, 16 terrain-funded landscape
members and all 82 directed paths. The original 1.8m ridge, 25000 m² minimum,
source footprints and work budgets are unchanged. All 140 active plus three
protected samples remain accounted for; two declared wet edges retain 22 patches.

Generated water fragments now share original envelope/terrain coordinate anchors
before their intersections become output geometry. The shared four-ULP operation
convention preserves distinct original coordinates; it is not an exact-arithmetic
proof. Partition trials cache complete-piece mass and integrate only crossed
pieces, avoiding unnecessary centroid/second-moment calculations. Final area,
mass, connectivity and clearance checks remain. The independent route oracle now
handles polygons, with analytic cases and rectangle-distance cross-checks.

Ten affected C# regression scopes pass. The existing dry-world 59/60-node
edit/clean-generation/rollback/publication/checkpoint lifecycle also passes.
An independent rational sweep gives identical 15.492779740504433 m² unions for
retained water patches and the clipped source envelopes. Current CLI and tests
build without warnings. Earlier geometry, work-budget, test-oracle and fixture
assertion failures remain recorded. See [source revisions, checks and remaining gates](../../evidence/2026-10-29-water-physical-verification.json).
S02/S10 and the overall professional-generator objective remain IN PROGRESS;
generated climate/scalars, lake levels and broader variety/scale/design gates
remain open. Godot and production artwork are deferred.

## Water recipe/session qualification and resolved historical failures

**Water recipe/session and schematic integration — 2026-10-29:**
Optional `Water`/`WaterPlan` now preserve original drainage terrain while an
explicit `water-surface` stage supplies every actual terrain consumer. Current
small-reference tests qualify width/source/terrain/threshold edits versus clean
output, required regions/content or explicit refusal, cache invalidation,
failed/cancelled-edit rollback, strict publication/read and source-history restore.
The colored schematic checks saved witnesses, shows flow/width tooltips and
exports exact engine coordinates. Native SVG label bounds correct fit/export
clipping without including transparent hit targets.

Eight affected C# regressions and the existing 59/60-node dry-world lifecycle
pass. Nine JavaScript checks, current CLI packets, actual browser controls/SVG
export and independent saved-surface/SVG checks pass. [Evidence](../../evidence/2026-10-29-water-metadata-verification.json) and
[API/example](../../../examples/metadata/water/README.md) distinguish tested binaries and source revisions.

**Historical larger-reference failures:** the original outlet-only request
correctly refused the protected closed basin at sample 70. An explicit retained
sink preserved that terrain. Subsequent geometry and work-budget failures are
resolved by the qualification above; their original inputs/diagnostics remain in
the earlier evidence. No counts, source geometry, ridge, minimum area or budgets
were relaxed. The small reference alone did not close these failures.

## Current occupied water core qualification

**Occupied water surface core — 2026-10-29:**
Threshold-selected drainage edges now produce declared-width, rounded wet
footprints through the shared envelope/face owners. They clip at field/domain/
void masks, retain original elevation planes and authored overlay precedence,
and preserve flow, widths, clipping and source-edge provenance. Uniform planes
compact to four patches in the straight-stream reference; nonuniform terrain
retains 92 explicit pieces. The shared numerical correction uses symmetric rays
and calculated-event canonicalization; distinct authored coordinates stay distinct.

Independent union area is 2.0391823164166367 m², matching the analytic full-width
band. Real existing region, fixed-footprint placement and land/water traversal
consumers react to the new occupied surface. Curved tributary joins, masks,
raised/ambiguous overlays, negative translation, replay/tamper, limits and
cancellation pass. Eleven affected regressions and the complete existing
59/60-node connected-world edit/rollback/publish/restore lifecycle also pass. [Evidence](../../evidence/2026-10-29-water-core-verification.json)
and [API/example](../../../examples/terrain/water/README.md) own those earlier bounded claims. Water recipe/session wiring and visualization
are qualified by the later increments above; environmental scalars remain open. S02/S10 and the
whole generator remain IN PROGRESS; Godot deferred.

## Current drainage recipe/session qualification

**Drainage recipe/session integration — 2026-10-29:**
Optional recipe `Drainage` now runs as a terrain-dependent stage and is stored as
`DrainagePlan`. Full document validation binds it to the current terrain/frame and
replays the calculation. Population sources preserve existing drainage facts;
chunks keep it as shared world data. Atomic metadata export writes a standalone
plan and summary totals; strict readers replay both standalone and embedded data.
Publication now enforces the reader's existing 16 MiB JSON-file bound before commit.

A four-node/three-landmark framework reference qualifies direct/staged equality,
source/outlet/height edits against clean generation, exact relevant cache
invalidation, protected extent, failed/cancelled edit rollback, publication/read,
changed-terrain/corrupt-plan refusal and full source-history restore. Six affected
C# regressions and actual CLI export pass; independent Python checks corroborate
saved flow/terrain/source/totals. [Evidence](../../evidence/2026-10-29-drainage-metadata-verification.json) and
[example/API](../../../examples/metadata/drainage/README.md) record scope and source identities. Water width/occupied
geometry, derived climate/scalars and drainage visualization remain unimplemented.
S02/S10 and the professional generator remain IN PROGRESS; Godot deferred.

## Current optional drainage core qualification

**Optional sampled drainage core — 2026-10-29:**
The shared C# terrain core now accepts explicit sources and outlet/retained-basin
terminals on the existing heightfield triangle-edge graph. Non-uphill shortest
paths resolve flat terrain without elevation edits; every participating sample
must reach a declared terminal. Complete shared edge masks and overlay profiles
prevent hidden barriers, peaks and troughs from being crossed. Per-edge losses,
flow joins, terminal receipts, source identity and conservation totals replay.

Plane/flat/basin/disconnected inland cases, generated-heightfield replay,
source/outlet edits, input isolation, JSON/tamper, finite bounds and cancellation
pass. Three saved examples also pass independent Python geometry/flow checks;
four affected regressions pass. [Evidence](../../evidence/2026-10-29-drainage-core-verification.json) and
[API/examples](../../../examples/terrain/drainage/README.md) record exact bounds. This is a sampled game drainage
grammar, with explicit directional bias and per-sample rates; metadata/session
wiring, water width/geometry, downstream environmental fields and schematic
consumers remain unimplemented for generated drainage. S02.7 and the overall
world generator remain IN PROGRESS; Godot deferred.

**Generated-heightfield full-program integration — 2026-10-29:** Flat/ridge/valley
terrain now carries all seven required compounds, parts, paths and landscape
through the shared pipeline at unchanged sampling/grade/area/work limits.
Independent sample heights and all triangle gradients are checked. Conservative
shared geometry broad phases remove a reproduced terrain-demand replay bottleneck.
A steeper ridge lacks the original minimum suitable area; another still fails
connected-space synthesis. [Evidence](../../evidence/2026-10-29-area-landforms-verification.json).
This qualifies gentle terrain integration, not blocked-terrain expressive range,
drainage/climate, maximum scale or complete S02. Godot remains deferred.


Plan date: 2026-10-26. This document specifies future work; it is not an implementation-completion claim. Status and evidence are owned by the [master stage tracker](../../ROADMAP.md#master-stage-tracker). Requirements, decisions and shared delivery rules are in the [master plan](../../ADVANCED_WORLD_GENERATION_MASTER_PLAN.md#current-staged-plan).

Dependencies: [S01](s 01-shared-world-contracts.md)

Requirements: W 01, W 02, W 03, W 04, W 07, W 13. Responsible executor: Codex, including explicitly labeled self-review. Accountable product/design owner: Fahad. No external or independent reviewer is assigned or claimed.

## Purpose and current gap

Current authored terrain queries belong to the mockup route. The rejected procedural city path is removed. The replacement needs terrain as an input to planning, with queryable geometry and constraints; it cannot be a color painted beneath a finished grid.

### First common surface slice (2026-10-26)

Implement `WorldSurfaceQuery` in Core as the shared owner for a simple irregular outer boundary, excluded polygon masks and priority-ordered planar terrain patches. Domain boundaries are optional for unbounded source queries, explicitly distinct from terrain coverage. A bounded metadata surface must declare its boundary. Each patch carries game-authored kind, purpose, source rule, origin, elevation and gradients; units belong to the surface and must match the metadata frame. This slice supports two-coordinate surface domains only, not volumes or multiple physical floors.

Acceptance: snapshot/validate polygons and finite coefficients within explicit work budgets; distinguish outside-domain, excluded, uncovered, ambiguous and sampled points; calculate analytic elevation/slope; trace crossings without fixed-distance sampling; check full circular-clearance path envelopes against the boundary/exclusions; prevent metadata placement outside valid surface coverage; persist the exact field snapshot. Convert existing mockup terrain queries to the same owner without requiring mockups in the shared engine. Verify flat/hill/coast/valley, narrow interruption, concavity, exclusions, source provenance, cancellation, malformed/unsupported input, persistence and actual CLI behavior. These queries constrain later planning; they do not yet generate terrain features, routes or buildings.

## Algorithm and design

### Whole-footprint scalar constraints — 2026-10-26

Extend the shared structured sweep to expose closures of planar arrangement faces, edges and vertices with their actual source owner. Derive environmental affine values inside each region; split convex regions at declared clamp thresholds before evaluating downstream fields, preserving correlations rather than combining unrelated interval extrema. Evaluate extrema on region vertices, including both sides of discontinuous authored overlays. Query budgets and explicit failure cap piece growth; no sparse sample fallback may certify a footprint.

Acceptance: analytic plane ranges, correlated fields, extrema created inside a footprint by clamps, off-center water/steep pockets, thin masks, concavity and boundary ownership; point-valid/footprint-invalid cases; cancellation and work/piece limits; scoped metadata publication and replay; exact engine footprint/range inspection. Hard requirements must cover the whole declared polygon. Point preferences remain separately labeled and are not an area-average score. Spatial capacity, collision, access and actual placement remain subsequent gates.

### Active environmental fields and point suitability — 2026-10-26

Add bounded, game-authored scalar fields derived from height, slope, coordinates and other declared fields. Validate the dependency DAG; evaluate typed linear terms with optional clamping, preserving units/purpose/source identities. No dynamic expression execution, implicit terrestrial model, climate simulation or automatic water/road interpretation. Suitability profiles combine explicit terrain-kind restrictions and hard scalar ranges with separately recorded weighted preferences; a soft score cannot override a failed requirement. Uncovered/excluded/ambiguous terrain produces unavailable values and an ineligible point.

Integrate scoped node checks into metadata generation/read/export. A required eligible point rejects publication when its declared profile fails; profiles never silently relocate or discard nodes. Persist the definitions, exact observations and per-rule decisions, then rederive on read/export. Acceptance: analytic values, field dependencies, missing/cyclic references, hard-vs-soft behavior, unknown terrain, deterministic snapshots, count/scope preservation, cancellation/work limits and real CLI audit. These are point decisions only; arbitrary scalar ranges over entire footprints need later range-aware qualification before physical site placement. No whole-footprint or placed-region claim follows from a colored sampled field.

### Active field-footprint scaling — 2026-10-26

Replace the all-triangle-edge arrangement used by area queries with a structured sweep. Keep intersections among authored boundaries, add heightfield column boundaries and authored-edge/triangle crossings as x events, then enumerate field row/diagonal cuts only inside the queried polygon's cross-section. This preserves concave footprints, thin overlays, domain/exclusions and deterministic boundary ownership without a quadratic pair comparison across the field mesh. No compatibility fallback to the removed all-field-edge path.

Acceptance: compare small fields against an independently expanded planar-triangle oracle; qualify whole-area and long diagonal passage queries on 513×513 samples; detect off-center steep pockets, thin forbidden overlays and uncovered field boundaries; preserve priority masks and concave footprint gaps; enforce aggregate work/cancellation and existing authored-geometry limits. Record observations rather than promise universal performance. This query work enables environmental/suitability placement; it does not itself place destinations or complete S02.

### Active generated-heightfield slice — 2026-10-26

Add a bounded, immutable sampled elevation field to the existing surface owner. Use two triangles per sample cell with shared vertex heights and deterministic edge ownership, direct cell lookup and analytic gradients. Sampling cells do not allocate visible regions, streets or structures. Replay a base plane, bounded coordinate-seeded detail and ordered additive/blended radial or polyline landforms; one-point controls form hills/basins, polylines form ridges/troughs, and blending levels selected locations. Preserve operation provenance and regenerate saved fields to reject stale/altered output. Existing authored patches remain explicit overlays, not a second procedural engine.

Acceptance for this slice: analytic plane interpolation/gradients and continuity across shared edges; deterministic seeds and shared-coordinate samples across resolutions; ordered feature editing; malformed input/work-budget/cancellation rejection; common metadata coverage and export/reload; exact height/slope values in colored JavaScript inspection. Route/area queries must include field triangle boundaries or fail explicitly at documented budgets, never treat the field as a flat patch. Test a narrow steep feature missed by endpoints. No hydrology, biome, suitability-placement, volume or full-world claim follows from this slice. Those remain subsequent S02/S03 dependencies.

Introduce or consolidate one terrain/domain owner for extents, holes, elevation, slope, water, barriers, surfaces and game-authored suitability. Support authored/imported fields and constrained procedural field generation. Keep representations explicit: polygon surfaces and sampled heightfields are useful for outdoor worlds; layered floors and volumes need their own capabilities, and topology-only space needs no invented ground. Define interpolation, priority, coverage and resolution semantics. Terrain detail can use constrained reproducible noise, but object placement cannot be arbitrary scattering. Climate/hydrology simulation is optional; authored water and drainage relationships suffice when gameplay does not need simulation.


## Inputs and outputs

**Inputs:** Spatial-domain capabilities, authored terrain controls or licensed source fields, required resolution, world bounds, water/barrier masks and selected gameplay constraints.

**Outputs:** Terrain/domain package with independently inspectable height, slope, water, barrier and suitability layers; query API; explicit coverage/errors; provenance and chunk-boundary sampling rules.

## Code and workflow owners

Review MockupTerrainQuery.cs, MockupWorldDesign.cs, TerrainCoverageTopology.cs, StyledTerrainComposer.cs and existing geometry/triangulation code. Extract common queries into the shared owner and migrate mockup callers rather than retaining a duplicate terrain engine. Add specific field providers in WorldCreator.Worlds only where their generation behavior is implemented.

Named existing owners are inspection targets, not a commitment to keep every current API. Proposed boundaries become concrete in S01. Replace superseded behavior at its current owner, migrate all callers and remove the retired implementation; no compatibility aliases or parallel legacy path. Preserve unrelated changes and immutable historical evidence.

## Ordered implementation tasks

### Optional environmental derivation contract

The [reference transfer review](../../WORLD_GENERATION_RESEARCH.md#transfer-assumptions-to-test) identifies a separate proposed capability beyond existing height operations and scalar expressions. Enable it only for recipes that request generated water/climate relationships. This is game-design control, not a mandatory physical simulation.

1. Declare water sources, boundary/outlet rules, retained basins, environmental parameters, protected terrain and a finite work budget. An inland map must not acquire an ocean merely to initialize drainage.
2. Derive a drainage graph from the shared surface representation with deterministic tie handling. Every participating component must reach a permitted sink or return an attributed unsupported/infeasible result. Preserve acyclicity and account for flow at joins under the declared source/loss model.
3. Keep derived flow separate from authored elevation. Any carving/filling operation requires an explicit recipe policy and provenance; never silently alter protected ridges or sites to make a drainage result pass.
4. Derive optional water geometry and environmental fields from that graph. Water centerlines alone do not establish occupied width or a traversable crossing. Feed the resulting geometry into the existing suitability, placement and route owners.
5. Qualify a flat plateau, closed basin, disconnected domain and inland domain without ocean cells. Change only an outlet or wind parameter; verify dependent outputs and unchanged protected inputs. Reload and incremental regeneration must agree with full generation. The core graph cases now have [bounded evidence](../../evidence/2026-10-29-drainage-core-verification.json); recipe/session invalidation now has [evidence](../../evidence/2026-10-29-drainage-metadata-verification.json); occupied stream footprints and direct region/placement/path effects now have [bounded core evidence](../../evidence/2026-10-29-water-core-verification.json); bounded water recipe/session/schematic derivation now has [evidence](../../evidence/2026-10-29-water-metadata-verification.json); full physical wet-world geometry now has [qualification](../../evidence/2026-10-29-water-physical-verification.json); sampled scalar effects and the wind core now have [qualification](../../evidence/2026-10-29-environment-wind-verification.json); automatic wind recipe/session derivation now has [qualification](../../evidence/2026-10-29-wind-metadata-verification.json); bounded retained-water geometry now has [qualification](../../evidence/2026-10-29-retained-water-verification.json); broader water policies/scale remain open.

Domain-specific derivation belongs to its capability owner; the generic generator selects capabilities through contracts, without a city/genre switch. Preserve geometry-free generation when this capability is absent.

| Task | Work | Acceptance evidence |
| --- | --- | --- |
| S02.1 | Define field sampling, surface topology, holes, units and boundaries, including out-of-domain behavior. | Queries distinguish unavailable data from zero elevation or buildable land. |
| S02.2 | Unify existing authored terrain overlays under the shared query owner. | Mockup and procedural consumers query the same facts without requiring an image. |
| S02.3 | Implement constrained terrain providers and editable feature controls. | Ridges, valleys and shorelines can be held fixed while supported detail parameters vary. |
| S02.4 | Derive suitability layers from game rules and terrain, retaining each contributing rule. | Slope/water restrictions change usable regions; a weighted preference cannot override a hard exclusion. |
| S02.5 | Define layered/volumetric capability tests and refuse unsupported domain operations. | A cave or bridge level is not flattened into a surface-only field. |
| S02.6 | Expose the field stack to the later colored review adapter. | Values, bounds and provenance remain inspectable independently of color. |
| S02.7 | Implement optional drainage/environmental derivation under the contract above. | Inland/no-outlet cases, flow invariants, protected terrain, water footprint effects and dependency invalidation have independent evidence. |

## Failure handling and operations

Validate inputs and required capabilities before expensive work. Apply explicit cancellation and finite work/resource budgets at stage boundaries and inside iterative work. Return attributed unsatisfied constraints instead of silent fallback, missing required content or partial success. Preserve prior accepted output on failure. Use the existing Log/Audit/Notification owners for applicable generation, export and review boundaries; UI consumers display actionable failures and cancellation. Record source/input identities, stage outcome and unresolved dependencies. Retry only after the cause or inputs change, with the same declared reproducibility guarantees.

Recovery is a coherent correction of the current owner and affected inputs, followed by focused checks. Do not restore a retired implementation as a runtime fallback. Artifact approval gates apply only to the relevant visual stages and exact revisions.

## Verification and acceptance cases

### Generated-landform counterfactual

The [reference algorithm review](../../WORLD_GENERATION_RESEARCH.md#algorithm-selection-and-adoption-checks--2026-10-27) motivates this terrain qualification. The [generated-ridge evidence](../../evidence/2026-10-27-world-landform-routes-verification.json) records a bounded positive counterfactual; it does not qualify the optional environmental derivation contract above. The procedure below remains the acceptance specification, not a claim that all S02 cases passed.

1. Fix a non-city exploration program, exact catalog hierarchy/dimensions, endpoints, world extent, seed, movement limits and finite budgets. Author a ridge with a traversable pass using the existing `WorldTerrainGenerator` operation contract. Set these values before observing output.
2. Change only that landform's control path/pass location. Independently check the generated field's height/slope at analytic control locations and verify that the old route violates the changed fixture's declared traversal limits.
3. Require a newly qualified full-width route through the alternative pass, or an attributed infeasibility result with no published world. Preserve all mandatory content and fixed anchors. Where placement is deliberately movable, compare its declared suitability and reservation rather than demand movement for its own sake.
4. Repeat and reload both inputs; retain identical unaffected source definitions and deterministic outputs for each input. Save the changed input property paths, field/route geometry, source hashes, work and rejected cases. Verify that JavaScript displays those coordinates without rebuilding them.
5. Treat partial regeneration and protected edit preservation as S08 gates; full regeneration of this pair alone cannot establish incremental invalidation.

Optional rainfall/drainage is a separate capability. If selected, declare boundary/outlet behavior and test drainage acyclicity, accumulation and downstream invalidation. Neither a colored biome nor a heightfield alone supplies this evidence. Do not impose a climate simulation on an interior or topology-only game.

- Use flat, hillside, coastal and river-valley terrain with the same extents and content demand. Verify materially different suitability masks.
- Check interpolation and slope against analytic fixtures; test holes, discontinuities, overlapping priorities and thin barriers.
- Different sample resolution must respect declared error bounds; neighboring queries must agree at shared boundaries.
- If generated drainage is enabled, test declared outlets and flow invariants. Otherwise mark hydrology simulation unsupported rather than implied.

Run relevant checks with commands, environment, input/source digests and observed outcomes recorded. NOT RUN is not PASS. Existing tests are baseline evidence only. Keep implementation, local technical qualification, browser/native review, owner acceptance and unavailable CI/release gates distinct.

## Risks and boundaries

Heightfields cannot represent arbitrary overhangs. Do not advertise volume/cave support from 3D point coordinates. Terrain source rights and physical units must be known; imported data is untrusted input.

## Exit gate

**Generated-landform counterfactual, 2026-10-27:** [bounded verification](../../evidence/2026-10-27-world-landform-routes-verification.json) retains nine nodes/eight physical members, exact catalog sizes and fixed anchors across a north/south generated-ridge pass edit. The common route search derives bends from the impassable triangle boundary, without supplied route waypoints. All 609 samples per input and six directional swept paths have an independent oracle; exact browser geometry and reload replay pass. Both previous paths fail on the opposite field. Generated/imported fields share the implementation. Direct closed/narrow route refusal, candidate/work limits and cancellation are tested. Complete closed-ridge CLI attempts end at a work budget with no publication; those outcomes are not geometry proofs. This qualifies the controlled positive counterfactual only; larger/complex fields may exceed finite graph budgets. Incremental invalidation and wider terrain/game coverage remain open.

The current supported-core technical exit gate is verified by the
[seven-task record](../../evidence/2026-11-02-s02-core-gate-verification.json).
Current usable-area counterfactuals, source queries, declared control effects,
independent floors, unsupported-frame refusals and optional environment cases
meet the original stage criteria within explicit finite limits. The historical
[surface/domain](../../evidence/2026-10-26-world-surface-verification.json),
[generated-field](../../evidence/2026-10-26-world-terrain-verification.json),
[structured-query](../../evidence/2026-10-26-world-field-area-verification.json),
[environmental-field](../../evidence/2026-10-26-world-environment-verification.json)
and [whole-footprint](../../evidence/2026-10-26-world-footprint-verification.json)
records retain their original incremental scope. Optional physical volumes,
unbounded terrain scale and whole-generator design acceptance are not inferred.

S02 closes when terrain/domain queries drive measurable usable-area differences, current consumers share them, and every advertised domain capability has geometry tests. No visual acceptance is inferred from a heightmap.

When the exit gate is evidenced, update the existing master tracker with actual evidence and the next dependency-ready task. Do not close this stage merely because these instructions or task descriptions exist.

## Owner-supplied implementation references — 2026-10-26

The first replayable landform/heightfield slice is implemented, informed by R 17/R 18. Structured footprint queries, whole-footprint environmental hard constraints and bounded suitability-based placement have focused evidence in the master tracker. The bounded generated-landform counterfactual now has the exit-gate evidence above; dependent edit regeneration and optional environmental simulation remain open. Existing planar patches remain useful authored masks; sampling cells must not impose visible settlement blocks. See [R 16–R 22 and the implementation priority](../../WORLD_GENERATION_RESEARCH.md#owner-supplied-implementation-references--2026-10-26). These source references do not imply adoption of external code.

## Research references

[R 05](../../WORLD_GENERATION_RESEARCH.md#r 05), [R 07](../../WORLD_GENERATION_RESEARCH.md#r 07). These references motivate the proposed design; they do not certify this implementation or imply adoption of external code. The research record distinguishes accessed full text from abstracts and records relevant limits.
