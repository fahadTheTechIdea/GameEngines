# S05 — Adaptive region layouts, grids, blocks and irregular partitions

## Verified — original supported-core technical gate (2026-11-02)

All six original S05 tasks now have supported technical acceptance. The current
shared allocation owners retain the complete 94-node / 91-source program on
four terrains, with exact suitable-area losses of 64, 108 and 409 square meters.
Region/allocation shapes respond to the terrain; source poses, dimensions and
required full-width journeys remain complete. A roadless resource/interaction
program uses the same contracts. Two attempts repair only the western grid;
the eastern owner and its protected physical anchor remain exact.

Ten named native scopes, 28 independent JavaScript checks, ten public CLI
publications, complete source replay and a protected editing session pass.
Six SDK/CLI worlds match byte-for-byte. Unknown root recipe/catalog JSON members
now reject at loading: renderer fields can no longer be silently ignored.
Existing layout algorithms and source dimensions are unchanged in this increment.
The test build retains one existing CS8602 warning; no viewer code changed and
browser review was not repeated. Source, commands, binary hashes, preserved
failures and task mappings are recorded in
[the verification receipt](../../evidence/2026-11-02-s05-core-gate-verification.json).

This closes the original supported S05 technical gate. Original S04, S06, S07
and S10 acceptance remains open, including broader capability/design/scale
qualification. Free concave/volume modes and owner design approval are not
claimed. Godot and artwork remain deferred.

### Recorded acceptance before implementation

Review all six original tasks through the shared current contracts. Run the
same complete 94-node / 91-source program on four declared terrains, preserving
mandatory counts, source dimensions and access while checking changed ownership
and allocation shapes. Verify a roadless resource/interaction program through
the same allocation contracts. Independently account for gross/suitable/allocated/
residual areas, exclusions, source footprints and full-width journeys. Existing
regular, weighted, nested, terrain-facing, constrained-repair and protected-edit
scopes must pass at their original limits. Keep source geometry in world units;
terrain sample resolution, game allocation cells and downstream presentation
cells are distinct. Unsupported presentation fields at the world-generation
boundary must refuse rather than silently act as no-op controls. Repair actual
failures before advancing the original gate. This does not grant free concave/
volumetric support, world-scale throughput or owner game-design acceptance.

## Verified — physical bend-position counterfactual (2026-11-02)

Run a fixed small matrix of physical world inputs with the same complete program,
actor profiles, source sizes and bounded retry policy. Record every actual
success and refusal for the declared terrain barriers and bend positions. If a
valid physical failure and a successful alternate occur for the same barrier,
verify the engine's bounded feedback can move between those declared positions,
retain all content and bidirectional journeys, and replay the saved world. A
numerical-resolution or budget failure is not a physical-design refusal.

On Windows/.NET 10.0.14, `dotnet run --project tests/WorldCreator.Tests --
--world-offset-physical-study generated/offset-physical-study-20261102`
recorded all 12 combinations of three full-height barrier positions and four
declared bend positions. Ten generated; the barrier at x=200–210 exhausted
physical component choices at offsets 0 and 150. With the same barrier, offset
-500 generated. The saved `study.json` retains all outcomes, and each case has
its exact request and result.

The follow-on source changes only the original offset-0 request by declaring
-500 as an allowed alternate. Actual feedback rejected the first physical
journey, selected a new partition with offsets `[-500,0]`, and accepted the
next context after a component retry. The generated world retained all three
regions, 48 nodes, 45 physical sites and two bidirectional regional journeys;
publication and source replay passed. With `WORLD_OFFSET_PHYSICAL_STUDY` set
to the study directory, `node --test tests/javascript/world-offset-physical-study.test.cjs`
passed three independent checks of the complete case record, unchanged source
apart from the alternate, saved feedback witness/tamper refusal, all source-size
footprints and path clearance against the barrier. The CLI world at
`generated/offset-physical-fallback-cli-20261102` matched SDK output byte-for-byte,
SHA-256 `BD24C326B02358CA6FBDA9D283B07BDE295F8F4A1AFAD74F08221E39FBB23077`.
Study source SHA-256 `2FAE1E95294A5431B9B9C4AC72D132383AC3D31B6B9F2F688B15B7D98EC8AE10`,
independent check `676CA1E9FEA79FB85B6328886AAC1758169960C2CFF7F52614CB62119E0ECFB9`,
Core DLL `FCFA0E0CCC5150F4662F0FDC0C28151B7435CBF5C2E0AC8378AB402FA1B8D0C0`.
The build retained the existing CS8602 test warning. This qualifies one
game-neutral physical counterfactual, not arbitrary layout search, human design
acceptance or runtime behavior. Godot remains deferred. The reusable source is
[`position-feedback-request.json`](../../../examples/metadata/bent-regional-world/position-feedback-request.json).

## Verified — preserve source edges at bend splits (2026-11-02)

Reproduce the 150-unit bend-position failure with saved source and geometry
diagnostics. Trace any computed endpoint back to its source edge, preserve exact
source coordinates while solving the tangent split, and retain refusal of real
sub-resolution areas. Verify the actual physical world, local/weighted/rotated
area partitions, protected holes, replay, independent geometry and CLI parity.

The saved reproduction `generated/offset-resolution-reproduction-20261102`
refused before generation: its recorded strip lay between `y=448` and
`y=447.99999999999994` along the authored boundary. The area partition owner now
uses its source-edge-preserving intersection calculation for both tangent and
final cut projections. It retains the exact axis-aligned segment coordinate
only when the computed solution is on that segment and within arithmetic
roundoff; it does not discard small polygons or weaken geometry refusal.

On Windows/.NET 10.0.14, `dotnet run --project tests/WorldCreator.Tests --
--world-offset-resolution generated/offset-resolution-edge-preserved-20261102`
generated and replayed the formerly refused world with three regions, 48 nodes,
45 physical sites and two bidirectional regional routes. Its accepted partition
retains the primary 150-unit offsets; the separate counterfactual above verifies
physical feedback to another offset. `WORLD_OFFSET_RESOLUTION_ROOT` pointing at that output with
`node --test tests/javascript/world-offset-resolution.test.cjs` passed independent
ownership-area, pairwise non-overlap, exact boundary, retained source footprints
and full-height barrier route-clearance checks. `--world-bent-regions`,
`--world-bent-regional-world`, `--world-area-geometry` and
`--world-region-clamp-matrix` passed; the latter retains a real narrow-strip
refusal. Six saved-witness tests also passed against the new local and regional
regressions plus prior feedback and seed fixtures. CLI generation to
`generated/offset-resolution-cli-20261102` matched the SDK world byte-for-byte,
SHA-256 `51744D9EC2B8924B328E039F5913EB8BA9B39BE3AFC3ED3AF3BC1C85D1E6229F`.
Core source SHA-256 `5187752698E4414BBA86998CA69633BFF2ADB062D8584A20E2574D606C94D00E`,
independent check `13C4490964480349CAC4FA4959EBD563027120AA5879FA82AB6ED6F43EFDBC8C`,
Core DLL `FCFA0E0CCC5150F4662F0FDC0C28151B7435CBF5C2E0AC8378AB402FA1B8D0C0`.
The test build retained the existing CS8602 warning. Broad numerical robustness,
broader physical fallback, visual design and Godot remain open. The reusable input is
[`resolution-request.json`](../../../examples/metadata/bent-regional-world/resolution-request.json).

## Bounded verification — alternative bend positions (2026-11-02)

Allow bounded game-authored alternative bend offsets alongside declared axes and
bend slopes. Region search and physical feedback must reconsider only declared
choices, retain each actual offset in selected/rejected witnesses and replay,
and obey existing work and attempt bounds. A seed may order alternatives but
must not add them. Straight cuts do not duplicate equivalent offset candidates.
Verify asymmetric terrain/hole fallback, selected source and physical content,
saved witness validation, seed, cancellation and exhaustion; Godot is deferred.

`AlternateBendOffsets` declares one to eight additional finite distinct positions.
The area search orders combinations with the declared axes and bend slopes,
retains each actual position in partition attempts, and avoids multiple
equivalent positions for a straight cut. The regional physical-feedback frontier
can reconsider declared offsets; switching back to a straight cut resets its
irrelevant offset to the primary value. World seeds may target the partition
when bend-position alternatives are the only variable. Saved schematic validators
check selected offsets and the frontier's bounded candidate accounting.

On Windows/.NET 10.0.14, `--world-bent-regions
generated/alternate-offset-local-final-20261102` passed a protected-hole case
that rejected offset 0 and selected a declared alternate, independent area and
analytic geometry, seeded positions, mixed straight/bent deduplication, direct
frontier enumeration, invalid input, bounds/cancellation and nested world
replay. `--world-bent-regional-world
generated/alternate-offset-regional-final-20261102` generated/replayed four
complete regions, 20 nodes, 12 sites and three bidirectional connections.
`--world-offset-seed generated/alternate-offset-seed-final-20261102` used a
world seed to select declared positions and replayed the complete world.
`--world-bent-feedback generated/alternate-offset-feedback-final-20261102`
passed physical retry with alternate offsets present, but its successful retry
changed bend slope, not position. Direct queue checks cover the latter
enumeration; the separate physical counterfactual above verifies a position retry.

At that earlier build the probe with a 150-unit primary offset refused with
`WORLD_REGION_GEOMETRY_RESOLUTION` before physical feedback; 10 and -500-unit
probes did not trigger the intended physical retry. These are not counted as
successful position-fallback evidence; the source-edge correction above now
generates the 150-unit case without requiring a position fallback. `WORLD_BENT_LOCAL_ROOT`,
`WORLD_BENT_REGIONAL_ROOT`, `WORLD_BENT_FEEDBACK_ROOT` and
`WORLD_BEND_POSITION_SEED_ROOT` set to the four outputs above with `node --test
tests/javascript/world-bent-partitions.test.cjs` passed six saved-witness,
footprint, feedback and seed checks, including refusal of a forged position.
The regional CLI output matches the SDK world byte-for-byte, SHA-256
`0E2478FD6DFE71C369E3CBFA18141DEAFC4B2C8974629948324FF90BB0D94702`.
CLI build had zero warnings; test build retained existing CS8602 at
`WorldGroundUseCapacityTests.cs:133`. Core DLL SHA-256
`34DB9224ABFC116BA635F95B8F8EB971CE121806A6C3A83070520F587119383D`,
area source `2393E52E9E4B1659F5D128DE6884EADE053D1711ADED6477B791F41071BD92D5`,
frontier source `D3B4427AFAD04EF5D818F45B51FD6EC0329503220A9E09412AE8417D1E32EF5C`,
test source `B735C54801C273A49D3E72BBBAE1173CB10D3AF13414C59792EFA7F1B700948B`.
Broad geographic design, that physical-fallback case and Godot remain open.

## Verified — asymmetric region bends (2026-11-02)

Let a game-authored partition policy move its bend along the cut's tangent,
without forcing it through the region centroid. Preserve source area, declared
weights, connected components and all terrain and physical qualification.
Validate the signed offset and replay it in saved schematic witnesses. Check
analytic positive and negative offsets, rotations, holes, exact mass and an
actual nested-content world; default zero must preserve existing behavior.

`WorldAreaPartitionSearch.BendOffset` now moves the meeting of the two arms
along each selected split's signed tangent in surface units. The normal area
integrator solves the shifted cut; it retains connectivity, terrain, physical
content, work budgets and source replay. The default zero retains centered
bends. Invalid/nonfinite offsets, or a nonzero offset without any declared
nonzero bend, refuse. This is a finite authored rule, not automatic natural
geography or a requirement that a whole world use a grid.

On Windows/.NET 10.0.14, `--world-bent-regions
generated/asymmetric-bend-final-20261102` passed analytic positive and negative
two-unit offsets, independent expected cut vertices, rotated cuts, balanced area,
protected hole, invalid inputs, cancellation and a complete nested 11-node,
nine-site world with saved replay. `--world-bent-regional-world
generated/asymmetric-bend-regional-20261102` generated and replayed four
regions with a three-unit offset, retaining 20 nodes, 12 physical sites and three
two-way connections. Existing area geometry and clamp-matrix checks passed,
including genuine narrow-area refusal. The test build retained the existing
CS8602 warning in `WorldGroundUseCapacityTests.cs:133`.

With `WORLD_BENT_LOCAL_ROOT` and `WORLD_BENT_REGIONAL_ROOT` pointing to the two
new outputs and `WORLD_BENT_FEEDBACK_ROOT` to
`generated/bend-feedback-explore-20261101`, `node --test
tests/javascript/world-bent-partitions.test.cjs` passed four saved-witness,
content, footprint and feedback checks; changing a saved offset was rejected.
CLI local generation and `verify-metadata` passed and the CLI world matched the
SDK world byte-for-byte (`E7F40EE43AF6ED9866B10E120490FF4FF549FF68EE22E0C76746D38AA1040E1F`).
The regional CLI also matched its SDK world
(`A7D4C9BC9DB18A205815DDA2864CDFAE678D02B76F1D43EACF3E048DE0A65615`).
Core source SHA-256 `65357D769092D246EDC70C6B84BC2C1C58FA9B13C3B0F3968F29E8E2767AC10A`,
schematic source `CB2C96299C5577253FFDE78E9A98A4F844F395ABAC6B071C1407E0518DAB2AA6`,
test source `0E46D29ECC83D114228CD2978BE314CA724F373D44B7AF0787FE18CA4EBE4BD8`,
Core DLL `254373315D7A84863E30D4E0F4028A3BB17664483D154F2734C70A4DAE5EDD10`.
The first local/CLI files had differing implementation IDs because the engine was
rebuilt between runs; both were regenerated/compared against the same build.
Visual design, broad world variety and Godot remain unverified.

## Bounded verification — terrain edits with adaptive boundaries (2026-11-02)

Hold the complete regional programs, route profiles, bend alternatives and work
limits fixed while moving a terrain barrier. Derive and publish each world from
the authored source, verify actual ownership/route changes and complete required
content, then restore the original source. Compare incremental and clean output,
replay the saved session, and verify an infeasible edit preserves the accepted
world. This is a framework counterfactual, not a game or art approval. Godot is
deferred.

Moving only the full-height barrier from x=150..160 to x=115..125 changed the
selected split bends from [-0.4, 0] to [0, 0], changed actual ownership and
regional paths, and retained three regions, 48 nodes, 45 physical sites and two
bidirectional regional connections. The declared maximum work remained 20 million;
observed ownership work was 2,212,181 and 1,832,004 respectively. Clean generation
matched the edited session; restoring the source reproduced the original world.
A fully blocked edit produced no publication and preserved the accepted state;
the complete successful session history replayed.

Four saved-world JavaScript checks independently tested the full-width barrier
clearance of all 30 local and regional directed path segments across the initial,
moved and restored states, source-node preservation and the edge lengths of all
45 moved sites. CLI output for the moved source exactly matched the session world.
Evidence: `--world-bent-terrain-edit generated/bent-terrain-edit-20261102`,
`WORLD_BENT_TERRAIN_EDIT_ROOT=generated/bent-terrain-edit-20261102 node --test
tests/javascript/world-bent-terrain-edit.test.cjs`, and CLI output
`generated/bent-terrain-cli-moved-20261102`. Both inputs are in the
[framework example](../../../examples/metadata/bent-regional-world/README.md).
Core SHA-256 `6DC568A7798B8C0FE22C76A2BBC71FE2CB7679461D3E85964FF8583021CFE629`;
test build retained the existing CS8602 warning in `WorldGroundUseCapacityTests.cs:133`,
and CLI build was clean. This single barrier and authored program do not qualify
arbitrary terrain, player experience or world-scale performance.

## Bounded verification — bent-boundary alternatives (2026-11-01)

Allow a game-authored area policy to declare a primary bend and finite alternate
bends. Search the combined angle/bend choices within existing budgets, retaining
each attempted and selected value. A seed may choose among declared values; it
must not add geometry or relax content constraints. Regional feedback must
retain and reconsider the same complete choices. Test the protected-hole case
where one bend disconnects a region but an explicitly authored alternative
succeeds, plus seeded variation, complete physical regions/routes, source replay,
schematic witness validation and hard limits. Godot remains deferred.

The shared partition policy now accepts one to eight distinct alternate bends.
The normal bounded split search evaluates declared angle/bend pairs and records
the actual bend for each attempt. Seeded ordering chooses only from those pairs;
regional physical-feedback search can also reconsider a bend while retaining
the full cut assignment and existing work/attempt limits.

In the analytic protected-hole case, a bend of 1 would disconnect a region;
the declared 0.5 alternative succeeds with both 136-unit areas and the hole
unchanged. A separate three-region physical case initially cannot connect its
required journey across a terrain barrier. Physical feedback changes one
declared split from 0 to -0.4, retaining all 48 nodes, 45 physical sites and
both bidirectional regional journeys. Two world seeds using one axis select
different declared bends and retain a separate complete 20-node world each;
one design source-replays. Saved-witness, source-content, footprint/overlap and
schematic checks pass. The feedback example's CLI world exactly matches the
C# result. Eight straight fixed-seed plans remain byte-identical to their prior
results. This is a finite piecewise-linear search, not complete natural geography
or proof that every declared alternative is feasible.

Evidence: `--world-bent-regions generated/bend-alternatives-local-20261101`,
`--world-bent-regional-world generated/bend-alternatives-regional-20261101`,
`--world-bent-feedback generated/bend-feedback-explore-20261101`,
`--world-bent-world-seed generated/bend-world-seed-final-20261101`,
`--world-seeded-partitions generated/bend-alternatives-zero-regression-20261101`,
and `tests/javascript/world-bent-partitions.test.cjs` with the four matching
output-root environment variables. CLI output is
`generated/bend-feedback-cli-20261101`. The tested Core SHA-256 is
`EAA74148F8D598813823642694D34B1B5F748AABA0EA0696218F080794D4ABA6`;
the later Core rebuild changes a source comment only and its local/alternative
smoke and five JavaScript checks passed using
`generated/bend-alternatives-current-20261102` (Core SHA-256
`6DC568A7798B8C0FE22C76A2BBC71FE2CB7679461D3E85964FF8583021CFE629`). Test build reported the
existing CS8602 warning in `WorldGroundUseCapacityTests.cs:133`; CLI build was
clean. The reusable [framework inputs](../../../examples/metadata/bent-regional-world/README.md)
are not a particular game. Broader world-design, throughput and the professional
generator gate remain open.

## Bounded verification — bent boundaries in the shared area allocator (2026-11-01)

Add an explicit, bounded piecewise-linear bend to the existing area partition
policy. Split source geometry at the bend before solving the exact weighted
mass cut; retain connected regions, holes, source and route constraints, bounded
work and replay. Zero bend retains straight cuts. Exercise positive and negative
bends, density, irregular/hole terrain, actual physical content, invalid input,
budget, cancellation, saved schematic and source replay. This is a reusable
spatial strategy, not a city rule or a claim of arbitrary organic geography.
Godot remains deferred.

The shared area owner now supports an explicit signed `Bend` slope in [-1, 1].
It splits each source piece at the tangent, solves mass over both linear arms,
and retains the ordinary connectivity, terrain, capacity and work checks. An
11-node nested world and a four-region, 20-node world generate and source-replay;
the latter retains 12 physical sites and all three bidirectional connections.
Its four ownership polygons have 6, 5, 6 and 5 non-collinear corners. Independent
triangulation/intersection and area arithmetic find no positive-area overlap or
lost area in that rectangular-domain example. Native and CLI world documents
match; the schematic checks the actual bent split witness and rejects a forged
straight policy. Zero-bend output for eight existing fixed seeds is byte-identical
to the prior qualification.

Analytic tests cover both signs at slopes 0.5 and 1, rotated cuts at 30, 45 and
90 degrees, four seeded bent layouts, weighted mass, a protected hole, bounds,
cancellation and exact work limits. The extreme +/-1 cut across the tested hole
would disconnect an allocation and correctly refuses; it is not repaired by
moving or filling the hole. This is a finite piecewise-linear grammar, not a
smooth curve, coastline generator or proof of complete natural world design.
Evidence: `--world-bent-regions generated/bent-regions-extremes-20261101-03`,
`--world-bent-regional-world generated/bent-regional-verified-20261101`, CLI
output `generated/bent-cli-regional-20261101`, and
`tests/javascript/world-bent-partitions.test.cjs`; source inputs are in the
[framework example](../../../examples/metadata/bent-regional-world/README.md).
The existing straight partition/seed regression passed. CLI build had no warnings;
test build retained the existing CS8602 warning at `WorldGroundUseCapacityTests.cs:133`.
Core SHA-256 `DD14DD31CD4487B04CBFDF22D0B726FA896B7483C7E7FF7C3B146064B06CEB26`.
Wider varied terrain, scale, visual design and whole-generator gates remain open.

## Verified — physical adaptation with weighted local intent (2026-11-01)

Exercise existing bounded area-rotation adaptation with an unequal-weight
local program and required physical structures, access and route. Record the
initial rejected layout, accept an explicitly permitted alternative only if all
hard constraints pass, and preserve the exact weights and source dimensions.
Test clean generation, full source replay, edits and failed-edit rollback,
limits and schematic validation. Retain unsuccessful more extreme weighted
cases instead of changing required geometry. This verifies existing adaptation;
it does not claim unconstrained search or whole-world completion.

The 11:10 fixture records one rejected physical orientation followed by one
accepted authored alternative, retaining both weights, seven nodes, two sources
and the required route. Source replay, clean/edit equality, checkpoint restore,
attempt-limit rollback, SDK/CLI equality and schematic tamper checks pass.
A separate 2:3 request exhausted 13 declared orientation choices without a
feasible placement; it remains an explicit refusal, not proof of global
infeasibility. The existing adaptation owner is unchanged. Broader physical
redesign and whole-world qualification remain open.
[Example](../../../examples/metadata/weighted-layout-adaptation/README.md),
[evidence](../../evidence/2026-11-01-weighted-layout-adaptation-verification.json).

## Verified — local program allocation weights (2026-11-01)

Allow game-authored `AreaBalanced` program intents to choose a positive relative
allocation weight separately from their hard minimum area, as region intents can.
Use the shared area owner, retain density semantics, nested layouts, interfaces,
physical source geometry, route constraints, budgets and omitted-weight results.
Validate uniform and density-weighted layouts, nested/interface composition,
infeasible minimum and input refusal, complete physical generation, edit/replay,
CLI and schematic witnesses. This does not make a global grid or claim the
original mixed-strategy stage is complete.

Uniform 1:3 areas, mixed defaults, density mass, a shared interface and nested
allocations pass, as do a complete seven-node/two-site physical world, edits,
source replay, failure rollback, CLI/SDK equality and schematic tamper checks.
The tightly constrained physical fixture refuses 2:3 and 3:2 because the
required approaches become blocked; 11:10 succeeds with unchanged sources.
Those refusals do not prove every such ratio is infeasible. Existing default
program-space regression passes. This is a bounded capability, not complete
mixed-strategy/world-design acceptance; source-bound output may need regeneration.
[Example](../../../examples/metadata/weighted-local-programs/README.md),
[evidence](../../evidence/2026-11-01-local-allocation-weights-verification.json).

## Verified — regional ownership weights in a complete world (2026-11-01)

The regional ownership request already has a `Weight` separate from hard
minimum area, with a capacity-aware multiplier when capacity is declared.
Exercise it on a fixed six-region world with unchanged terrain, programs and
routes. Compare ownership and physical routing against equal weights, preserve
all required sources, validate minimum-area refusal, edit/replay and public
consumer output, and document the distinction from per-intent allocation
weights. Record actual failures instead of changing the source program to pass.

The existing regional owner generated a six-region weighted counterfactual
on the same irregular terrain, retaining 30 nodes, 18 physical sources and
seven bidirectional journeys. All six ownership boundaries and seven forward
paths changed; independent area proportions and every source footprint edge
length pass. Source edit, saved session replay, invalid-weight and impossible-
minimum rollback, SDK/CLI equality and schematic witness checks pass. The
existing regional weight applies across whole programs; local intent weights
apply within a component. Capacity policies change the effective regional
weight as documented. This qualifies one fixture, not broad world design.
[Example](../../../examples/metadata/weighted-regional-world/README.md),
[evidence](../../evidence/2026-11-01-weighted-regional-world-verification.json).

## Verified — separate regional allocation weight from minimum area (2026-11-01)

Allow a game-authored region intent to specify a positive relative allocation
weight independently of its hard minimum area. Partition the existing suitable
component by those weights, continuing to enforce every original minimum,
clearance, source and route requirement. An omitted weight uses the prior
minimum-area ratio; a weight without partitioning is rejected as inapplicable.
Verify independent equal/unequal-weight area equations, infeasible minimums,
mixed defaults, source replay, schematic checks and a complete physical world.
This is a reusable layout control, not a city preset or complete world design.

Independent uniform-area and density-mass equations, mixed specified/default
weights and a protected hole pass. An 11-node/nine-site physical world retains
its required network through reversed weights, source replay and failed-edit
rollback. CLI/SDK output, invalid public input, the existing region/seed
regressions and schematic tamper checks pass. Weights are local to each occupied
component and do not relax hard minima; broad design acceptance remains open.
Source-bound packets may need regeneration after Core changes.
[Example](../../../examples/metadata/weighted-region-intents/README.md),
[evidence](../../evidence/2026-11-01-region-allocation-weights-verification.json).

## Active — seeded local partition orientation (2026-11-01)

Allow a game-authored seed to order explicitly permitted partition-axis choices
independently for each part of the existing area-split tree. Preserve the same
region/area/terrain constraints and candidate set, source content, work limits,
replay and authored fixed choices. An omitted seed keeps existing results.
Verify deterministic changed shapes across seeds, selected axes, irregular
boundaries and holes, exact areas, required structures and routes, edits and
source replay. Failure remains explicit; straight partitions are not a claim
of organic settlement design or a world-wide grid. Godot remains deferred.

Bounded verification: eight seeds produced six distinct layouts for a rectangular
three-region fixture, preserved exact region areas, reproduced each plan and
obeyed exact work limits. A holed fixture preserved its area. An 11-node,
nine-site world retained connected routes after a seed and irregular-boundary
edit; clean generation, saved session replay and invalid-edit rollback passed.
The example's selected directions include a 45-degree authored offset, and
the CLI matched the SDK and verified its source replay. Schematic checks reject
invalid seeds and altered split witnesses. Wider seed, geometry and genre
coverage remain open; this does not complete the stage.
[Example](../../../examples/metadata/seeded-regional-world/README.md),
[verification](../../evidence/2026-11-01-seeded-region-qualification.json).


## Active varied-capacity regional integration (2026-11-01)

Qualify complete varied-capacity catalog programs through source analysis,
regional ownership and bidirectional connections. Distinct authored variants
must contribute their own source dimensions and capacities before region space
is allocated; no member may be dropped or resized. Verify exact demand areas,
ownership weights, complete nested physical content and source replay. This
qualifies a bounded mixed program, not all game-world design. Status: BOUNDED VERIFIED.

Three regional programs retain 15 nodes, nine physical sources and two
bidirectional journeys. A separate three-source program demonstrates that
unplaced demand retains both distinct variant polygons and capacities. The
regional and mixed programs match CLI output; saved source replay, independent
area/ownership equations and generated schematic checks pass. Broader game-world
design and original stage acceptance remain open.
[Example](../../../examples/metadata/regional-mixed-capacity/README.md),
[evidence](../../evidence/2026-11-01-regional-mixed-capacity-verification.json).

**Current — constraint-driven local area allocation (2026-11-01):** The
acceptance below is verified at the existing local layout owner. Its composition
loop is replaced by one constraint-qualified partial implementation. The same
interface calculator serves search and output; existing anchored strategies retain
their clipping algorithms. Area cut candidates qualify actual minimum areas,
shared spans and downstream/nested layout constraints. Rejected future prefixes
do not constrain a retry. Source/input, numerical, work/cell/attempt and cancellation
failures stay hard. The existing32-layout boundary passes without a lower cap.

The analytic principal split offers12m against an18m requirement; the allowed
alternative offers24m. The final harness refuses on prior Core and passes currently.
Same/cross-layout, weighted minimum area and nested fixed-anchor cases pass.
A complete seven-node world retains two84m² structures, two192m² reservations and
four declared station metadata children, without inventing physical station sizes.
Its35m interface requirement chooses a40m span; editing the requirement to20m
chooses a30m span while preserving full sources and both travel directions.

Eleven native scopes and17 saved-JS checks pass. Two SDK/CLI world states and full
edit history match; impossible edit/direct requests publish nothing. Independent
checks cover38 analytic layouts/43 allocations, four full shared spans,32 nested
layouts, four bodies/16 vertices, four directions/160 swept edges and eight interface
leg joins. Four original programs on both prior/current Core yield24 comparisons
with unchanged geometry/cost/source/provenance. Only explicit qualification/replay
work counters change. Four public correlations contain ten terminal operations.
[Workflow](../../../examples/metadata/local-interface-search/README.md),
[exact command/source/failure record](../../evidence/2026-11-01-local-constraint-search-verification.json).
One existing CS8602 test warning remains; CLI is clean. This does not close the
full professional world-design/variety/scale gate or establish continuous grammar
completeness. All63 task/13 requirement statuses remain; Godot/artwork deferred.


**Acceptance before constraint-driven local area layouts (2026-11-01):** Required
shared interface spans and actual minimum program areas must qualify candidate
strata before an `AreaBalanced` layout commits. Later/nested layout failures may
reconsider earlier declared area cuts; all participating strategies retain their
current geometry owner. Use one shared interface calculation for search and saved
output. Preserve full source dimensions, required members, holes, finite attempt/
work/cell bounds, cancellation, canonical ordering and unconstrained geometry.
Verify a prior refusal beside a feasible forced-axis control, same/cross-layout
interfaces, nested constraints, weighted minimum area, current32-layout limit,
complete physical world/edit/replay/refusals and public SDK/CLI parity. No new
city-first compiler, arbitrary placement, artwork or Godot work is introduced.

**Current — relationship-driven region search (2026-11-01):** Required region
distances now enter the common area qualifier so declared cuts can revise a failing
layout. Later occupied components can reject an earlier cut before it commits;
protected seeds and forced component assignments remain hard. All original
dimensions, required members, work/attempt and geometric constraints remain.
[S03 owns the bounded design/qualification](s03-gameplay-topology-and-regions.md),
[workflow](../../../examples/metadata/region-relationship-search/README.md),
[evidence](../../evidence/2026-11-01-region-relation-search-verification.json).
All stage/design scopes remain; Godot/artwork deferred.


**Current — fixed destinations in generated partitions (2026-11-01):** The
acceptance below is verified in the current region-intent/area owners. Exact fixed
points qualify against both original components and generated leaves. Both directed
sides of declared axes can be considered within existing attempt/work limits;
failure, required area/clearance, density, source counts and original dimensions
remain explicit. A rotated negative-coordinate case exposed the canonical-side
limitation and now passes with retained cut receipts. Unpinned output is unchanged.

Eleven named native scopes and17 saved-JS checks pass. The complete11-node/9-source
program retains all local/network travel through a boundary edit, clean generation,
publication and full history replay. Two SDK/CLI worlds and the edited checkpoint
match exactly; blocked edit/direct requests publish nothing. Independent geometry
verifies six analytic regions,18 source bodies/72 vertices,14 physical region cells,
20 directed journeys and896 swept obstacle-edge checks. Eight original unpinned
region plans and12 complete world parts remain exact. Four public correlations
contain ten terminal operations.

The area regression also exposed a pre-existing independent-oracle assumption:
explicit fixed growth candidates were parsed as direction-derived candidates.
The same failure occurs on prior Core. Its oracle now verifies actual zero-distance
anchor relations for those candidates, retaining source, containment, link-distance,
generated support-gap and route checks. The complete223-node/222-source/194-area-
member regression and connected-growth scope pass. One existing CS8602 test warning
remains; CLI is clean. [Workflow](../../../examples/metadata/protected-region-partitions/README.md),
[exact commands, source identities and retained failures](../../evidence/2026-11-01-protected-partitions-verification.json).
No continuous feasibility, every-game support or complete professional world design
is inferred. All63 task/13 requirement statuses remain; Godot/artwork deferred.


**Acceptance before protected anchors in shared-component partitions (2026-11-01):**
Extend the current region-intent/area owners so an existing `RequiredPosition`
can constrain a partitioned region instead of triggering an unsupported-capability
refusal. Every selected point must be strictly inside its actual connected region,
meet the intent and pattern clearance, and remain suitable with positive declared
density. Candidate/component and partition-leaf qualification must use the same
signed boundary-distance owner. Declared cut alternatives may revise the surrounding
regions; they must never move a fixed point, drop an intent or relax work/area/source
constraints. Preserve rejection witnesses, ordinary unpinned geometry, ordering,
physical nested content, routes, source edits, clean replay and rollback. Verify
the old refusal before implementation, actual SDK/CLI publication and independent
point/edge/area/source checks. Godot/artwork remain deferred.

**Bounded qualification — boundary-qualified contact spans (2026-10-31):** The authored
pose counterfactual rotates one complete compound20 degrees and grows every
shelter from4 to5 meters. Its unchanged contact directions/gap can fit the larger
content; the earlier face-center-only candidate missed a valid tangential offset.
The shared growth owner now derives contact translation intervals from
actual source supporting faces and boundary edge/vertex events. Refine only an
out-of-bound face-center attempt; retain the declared normal gap and positive
face span. Qualify the complete envelope with the existing boundary/terrain/
collision/access owners. Charge events, ordering and retained candidates to the
original budgets. Keep all requested sources, dimensions, controls and directions;
no scatter, extra grid, alternate solver or source resizing. All original sources,
dimensions, controls and request budgets are retained. [S08 owns the complete
authored-control qualification and evidence](s08-deterministic-chunks-and-persistence.md#bounded-qualification--authored-per-object-placement-controls-2026-10-31);
broader continuous layout/design gates remain open.

**Current — connected-program capacity (2026-10-31):** The unchanged40-case matrix
now yields39 worlds and the intentional blocked-terrain refusal. All16 interiors,
including all four24-room programs, retain their complete original content and
request budgets. [S10 owns the capacity/search and consolidated qualification](s10-validation-diversity-and-core-gate.md#bounded-qualification--consolidated-matrix-and-connected-program-capacity-2026-10-31).
Broad world-design/variety/scale gates and all original task statuses remain open.

## Bounded qualification — physical feedback into ownership partitions (2026-10-31)

Complete packing, authored/network travel and internal-cut/pin controls now pass,
including source edits, restore, refusal preservation and exact rollback. Coupled
habitat/count/program search changes ownership after a required journey fails,
then retains every final terrain-required original member. Independent checks
cover six worlds/86 nodes/60 bodies/60 directed paths and 248 segment/obstacle
comparisons, including exact 2.4-meter nested markers and unchanged habitat
equations. Ten native scopes, 47 JavaScript checks, two SDK/CLI exact worlds,
four typed no-output refusals and actual browser export pass. The SVG preserves
12 bodies/96 coordinates and six regional directions/24 coordinates.
[Inputs](../../../examples/metadata/physical-ownership-redesign/README.md),
[retained failures, commands, revision and limits](../../evidence/2026-10-31-physical-ownership-redesign-verification.json).
Only declared finite axes are searched. Full world design, variety/scale, object
editing and streaming remain open; no original task/requirement status is closed.

Acceptance recorded before implementation: when complete physical generation or
a required regional journey fails, the existing physical-feedback owner must
search other declared ownership-axis assignments, including internal cuts of a
multi-region partition. Use one shared area solver and finite semantic cut keys;
do not rotate an already generated world or add a city-specific strategy.
Component choices belong to one exact ownership context and must be discarded
when its masks change. Complete required content, original dimensions, pins,
terrain, access and travel constraints remain mandatory.

Reproduced with the retained qualified Core: the same two complete programs fail
with the initial division and succeed with the declared 90-degree alternative,
both for an impassable terrain cut separating a required journey and for a
70-by-10-meter complete source requiring a wider region. Receipts are
`generated/partition-redesign-before-{network,packing}.result.json` and
`generated/partition-redesign-forced-{network,packing}.result.json`.

Retain each requested/selected cut assignment, exact ownership context, rejected
planning/physical witness and actual work. Distinguish finite exhaustion from
attempt/work/geometry limits and cancellation; source-only impossibility must
remain available to program-choice pruning, not become a geometry retry. Reuse
normal source/edit/history/publication/reporting owners. Verify ordinary and
coupled program composition, protected origins, multi-cut alternatives, replay,
rollback, JavaScript inspection, CLI and independent original geometry/routes.
No whole-world design/variety/scale acceptance follows from these controls.
All 63 task/13 requirement statuses and Godot/artwork deferral remain unchanged.

Coupled acceptance exposed an existing composition-enumeration limit: the full
12-parent terrain demand spends its 64-analysis cap revisiting already proven
exhausted single-choice descendants before reaching a feasible parent mix.
Retain that exact source, quantities and cap. The shared choice cursor may skip
only descendants whose exact cached choice domain is already exhausted; unknown
alternatives, changed contexts and hard work/cache/selection limits remain real.
Both complete alternatives with all twelve original children now enumerate with
exact Cartesian-product ranks inside the unchanged twenty-analysis test cap.
The original coupled world passes within its unchanged 64-analysis cap. Known
fixed-count single-offer domains are exhausted by their solved unique vector;
unknown or changed domains are not skipped. Other work/cache/selection limits
remain mandatory. Exact quarter-turn axes avoid residual slivers without rounding
other angles. Hard route-candidate failures on later partitions remain hard
failures, not proofs of exhaustive infeasibility.

## Bounded qualification — terrain-qualified ownership partition search (2026-10-31)

The unchanged initial request fails on the retained prior Core,
while its authored90-degree alternative succeeds. The shared area owner now
qualifies actual terrain inside partition search and reuses selected anchors.
Five complete controls retain50nodes/30physical sources under terrain, irregular
boundary, pin and larger-source changes; native read/history/rollback and hard
limits pass. Current controls, complete-component, physical-feedback, ordinary
area, nested-source and coupled-world native regressions pass: seven scopes in
total. Forty-four JavaScript acceptance and mutation checks pass on the current
source. Independent checks qualify15ownership
queries, five actual rejections, ten selected terrain areas,20exact selected
hashes, all30directed paths and120segment/solid comparisons. Two SDK/CLI worlds
are byte-identical; four typed CLI refusals publish nothing. Actual browser
selection shows rejected/selected qualification counts; downloaded SVG retains
six whole sources/48 coordinates and both regional paths/eight coordinates, with
zero console warnings/errors. See the [reusable inputs](../../../examples/metadata/terrain-qualified-partitions/README.md)
and [commands, retained attempts and limits](../../evidence/2026-10-31-terrain-qualified-partitions-verification.json).
Later physical packing or global-route failures do not yet drive repartitioning;
the search covers declared finite axes, not arbitrary continuous world designs.
No full-stage or whole-world completion is claimed.

Acceptance before implementation: the current weighted area owner must qualify
each complete regional program's candidate ownership against its actual terrain
profile, required suitable area, source-derived clearance and explicit origin pin
before committing that partition. An unsuitable first division must trigger the
existing bounded authored-axis search, rather than aborting before another valid
partition is considered. Reuse one area/component/intent owner; do not invent
empty-cell placement, change source sizes/counts, drop journeys or force streets.

Retain actual rejected/selected ownership, terrain-intent witnesses, semantic
region identity and exact geometry/qualification work without double charging.
Component assignment retries must use the same deterministic selected ownership;
never reuse component ordinals across changed masks. Preserve work/attempt/input/
geometry/cancellation distinctions, source/history replay, failed-edit retention
and rollback. Full local/global physical qualification remains mandatory after
the complete partition. Geometry and anchor qualification alone do not certify a
world, or supply physical-route-driven global partition/source/layout redesign.

Reproduce a source whose first division lacks sufficient suitable terrain while
an existing alternative supports all complete programs. Qualify terrain/profile,
source-size, irregular-domain and pinned-intent counterfactuals; test budgets,
exhaustion, replay, SDK/CLI, neutral inspection and independent area/clearance/path
checks. Keep all63task/13requirement statuses and the full world objective active;
Godot/artwork remain deferred.

## Bounded qualification — complete component assignments (2026-10-31)

The standalone physical-feedback scope now qualifies second-endpoint and
two-location redesign, fixed program preservation, complete source geometry,
source edits/restore/rollback and distinct exhaustion/limits. Three controls
retain35nodes/21bodies and22directed paths. Nineteen JS checks, two byte-identical
SDK/CLI worlds, four typed no-output refusals and214independent path/solid checks
pass. Shared cut-plane refinement preserves representable intersections without
dropping tiny cells; genuinely tiny requested geometry still refuses.
Existing physical/count/source-composition regressions also pass; see the
[evidence](../../evidence/2026-10-31-complete-component-assignments-verification.json).
Full world design, source/layout/partition redesign, scale, selective locks and
streaming remain open. All63tasks/13requirements keep statuses; Godot deferred.

## Bounded qualification — nested upstream habitats in ownership (2026-10-31)

Explicit spatial context now delegates nested regions, source-demand minima and
local layouts to its one boundary-inheriting population source. Initial catalog
measurement marks source spatial qualification deferred; actual ownership analysis
qualifies complete upstream choices/layouts before exact counts and independent
consumer content. Source-only receipts preserve honest zero independent ranks/work.

Five controls retain184nodes/164bodies,30nested regions/30allocations,74discoveries
and110directed journeys. Source space-demand edit grows required discoveries21to32.
Native source/physical/history replay, exact rollback,33JSchecks, two byte-equal
SDK/CLI worlds, four typed no-output refusals and3278independent segment/solid
checks pass. Actual SVG preserves50bodies/400coordinates and22directed paths.
[Evidence and retained attempts](../../evidence/2026-10-31-nested-source-habitats-verification.json).

Shared-face outline identity now preserves source cells/area and exact loop
closure under the existing geometry predicate. A declared longest initial edge
policy follows accepted boundaries while all source/count/spacing/inset and
physical constraints remain hard. This is a technical composition qualification;
broader joint redesign, world design/variety/scale, developer locks and streaming
remain open. All63tasks/13requirements retain statuses; Godot/artwork deferred.

## Bounded qualification — qualified upstream program ownership (2026-10-31)

Actual upstream composition now qualifies its full source before downstream
expansion. Selected mixes/regions/layouts remain source-owned, preserving every
member, entrance, source dimension and journey. Consumers independently choose
only added content; analysis retains original source identities, failed/accepted
choices and exact habitat-driven quantities under captured cache/dependency limits.

Four controls retain48nodes/32bodies/28directed journeys and all10discoveries.
Native replay/edit/history rollback,37JSchecks, two byte-equal SDK/CLI worlds,
three typed no-output refusals and304independent segment/solid checks pass.
[Evidence and retained attempts](../../evidence/2026-10-31-source-program-ownership-verification.json).

This qualified the upstream prerequisite; the current qualification above
combines it with ownership-relative source contexts. General joint redesign, world design/variety/scale,
developer editing/locks and streaming remain open. All63tasks/13requirements and
statuses retained; Godot/artwork deferred.

## Bounded qualification — nested programs in actual ownership (2026-10-31)

Explicit purpose/source context now binds boundary-free terrain-intent templates
to exact computed ownership. Initial catalog analysis explicitly defers unknown
spatial selection; the shared joint owner rebuilds real region choices, demand
and complete local programs before accepting every physical body and journey.
Area layouts and relative frames reuse existing owners without source resizing,
arbitrary scattering, a mandatory world grid or reinstated city code.

Five controls retain165nodes/135bodies,45nested regions/45allocations and170directed
journeys. Independent checks cover3060segment/solid comparisons,135dimensions,
90original module offsets,177region pair intersections and15source-demand equations.
Two source edits/history restores produce exact original-world rollback. Native,
23JSchecks, two byte-identical SDK/CLI worlds, four typed no-output refusals and
actual browser/SVG geometry checks pass.
[Evidence and retained attempts](../../evidence/2026-10-31-nested-ownership-verification.json).

That context checkpoint required coupling/composition/capacity and excluded
population-source/terrain-demand phases. The current qualification above combines
those phases; broader joint redesign, complete world design/scale/editing, locks
and streaming remain open. All63tasks/13requirements and statuses retained; Godot/artwork deferred.

## Bounded implementation — ownership-relative nested spatial programs (2026-10-31)

Acceptance before implementation: an explicit purpose/source context may bind a
boundary-free intent-region template to its exact computed ownership polygon.
The source cannot override terrain, origin, ownership boundary or seed locations.
Reuse existing terrain intent partition, source-sized regional demand, local
layout/region-frame and complete physical content/journey owners. Relative local
frames and automatic area layouts remain game-authored strategies, without a
mandatory world grid or source resizing.

Initialization may measure complete catalog minima with region selection explicitly
deferred; it must never claim an unknown habitat or bind a fake region rank. The
existing joint owner must then reanalyze actual candidate ownership, bind all real
region choices and qualify every physical member and local/regional journey.
Accepted analysis cannot remain deferred. Require existing finite coupling,
composition and capacity policies; malformed/source/dependency/cancel/budget
failures stay hard and rejected branches remain visible.

Verify multiple complete nested programs under terrain, source-size and ownership
orientation edits; exact source dimensions, region/allocation containment, required
routes, source history/rollback, SDK/CLI/JS persistence and independent geometry.
Population-source habitat inheritance remains its separate current contract;
combining independent nested contexts with source-phase habitat redesign is not
silently inferred. Broad world design/scale, locks and streaming remain open.
All63tasks/13requirements retained; Godot/artwork deferred.

## Bounded qualification — spatial analysis and accepted source allocation (2026-10-31)

Direct surfaced recipes now analyze complete catalog choices and real terrain
region alternatives in ordinary generation order. Failed content-dependent region
analyses retain actual choices/reasons/work; exact bound generation reproduces
region/demand plans. Adapted source habitats are physically qualified by the
existing generator, and downstream quantities/layout use the accepted allocation.
Original sources, required members, entrances and journeys remain intact.

Four controls retain47nodes/34bodies and36directed paths. Independent checks cover
326segment/solid comparisons, three source-envelope equations, eight original
source dimension checks and all five required discoveries.440m²/430m² allocations
require three/two discoveries. Native source edit, hard-limit failure preservation,
history restore and exact rollback pass. Two SDK/CLI publications match byte for
byte; three typed CLI refusals publish nothing;14JSchecks pass.
[Evidence, tested binaries and retained failures](../../evidence/2026-10-31-spatial-program-analysis-verification.json).

Automatic ownership still forbids nested recipe Regions/LocalLayouts/RegionDemands;
an explicit ownership-relative context is the next integration. Unbound upstream
composition, broader redesign, locks, streaming and full design/scale gates remain
open. All63tasks/13requirements and statuses retained. Godot/artwork deferred.

## Bounded qualification — joint habitat, program and physical search (2026-10-31)

Acceptance before implementation: when both existing Coupling and regional
ProgramComposition policies are declared, enumerate complete program alternatives
against each actual candidate habitat, then feed their full source requirements
back into ownership allocation. Counts can change nested mix decision topology;
never reuse a minima-only prefix as if it were complete for a later habitat.
Accept only matching space requirements and exact complete choice bindings, then
qualify every physical member, entrance and required journey through the current
generator. Retain source history, failed branches, parent/iteration links, actual
count/habitat/analysis hashes and all work. Bound search with the existing
composition attempt, coupling depth, physical feedback and aggregate work limits.
Cycles and source infeasibility are explicit failed branches; malformed data,
dependency drift, cancellation and exhausted budgets remain hard failures. Reuse
actual terrain-component feedback when declared, reanalyzing after its effects;
no source resizing, missing members, arbitrary scattering or genre-specific code.
Terrain-only source contexts must also work without a fabricated source building
or placement stage when they request no placement anchors. They still inherit
the actual surface/origin/boundary, retain the exact source habitat, and refuse
authored overrides or requested anchors without a source placement owner.
Verify a dynamic nested-choice count change, physical preferred-program rejection,
terrain/source counterfactuals, source edits/replay/rollback, exact work and hard
limits, SDK/CLI/JS persistence and independent source/count/body/path geometry.
The full professional generator, broad design/scale, locks and streaming remain
open; Godot/artwork deferred.

Research checked2026-10-31: [PCG book, representations for search](https://www.pcgbook.com/chapter09.pdf)
separates representation from its evaluation; [SideFX PDG introduction](https://www.sidefx.com/docs/houdini/tops/intro.html)
describes dynamic work generated from actual upstream results. Design inference:
rebuild complete choice frontiers after habitat changes and evaluate actual
physical output, rather than treating one initial rank vector as a universal
representation. Existing engine owners remain responsible; no new dependency.

Observed qualification:

Actual habitat measurement, exact terrain-derived mix quantities and complete
catalog choices now share the existing regional owner. Candidate habitat changes
rebuild the full nested decision topology; source-invariant failures prune only
their owning choices. Stable counts and source requirements still require full
physical placement and all local/regional journeys. The original work, attempt,
depth, cache and stage limits remain hard. Invalid data, dependency drift and
cancellation never become successful retries. Terrain-only sources inherit the
exact qualified habitat without a fabricated placement/building.

Four complete controls retain140nodes/131bodies,43discoveries/43nested markers
and90directed paths. The compound preserves48original base nodes/45bodies and
all source-phase nodes/routes; original marker sizes and0.4mactor widths remain
intact. Nine current-Core native scopes,7JSchecks, SDK/CLI byte equality, three
typed no-output refusals and independent count/source/body/path/SVG checks pass.
Source sessions retain both accepted edits and exact initial-world rollback.
[Evidence and retained failures](../../evidence/2026-10-31-regional-joint-verification.json).

The simultaneous search is bounded to declared supported inputs; consumer intent
region/source adaptation analysis and broader physical/partition redesign remain
open. The component policy qualifies a successful joint candidate; this increment
does not independently exercise joint failed-component repair. Selective locks,
streaming, world variety/scale/design acceptance and the full stage/core gate stay
IN PROGRESS. All63tasks/13requirements retain their statuses; Godot/artwork deferred.

## Bounded qualification — editing worlds with complete regional program choices (2026-10-31)

Acceptance recorded before verification: exercise the existing regional session
owner with the complete program-choice request, retaining original catalog facts,
required children/functions and every local/regional journey. A source catalog
edit must reselect the preferred newly feasible program; unchanged regions may
reuse only matching private packets. An explicit rollback must restore the exact
original world while retaining both accepted edits. Publish and restore source
history through the public SDK and CLI, without saved executable caches. Verify
stale bases, inconsistent source/selection receipts, infeasible content, original
work limits, cancellation, existing output and tampered checkpoint failures leave
accepted state and previous publications intact. Inspect actual generated bodies,
routes, choices, operation outcomes and hashes independently. This qualifies the
combined edit workflow only; it does not close simultaneous terrain-count/mix,
selective authoring locks, streaming or full-world design requirements.

Observed: original, preferred-program and rollback states each retain12nodes,
9bodies,12required function units and ten directed paths. Only alpha regenerates
after its complete preferred program becomes feasible; beta/gamma packets remain
reusable. Two accepted edits replay exactly, and rollback restores original world
bytes. SDK source/explicit-definition routes and fresh-process CLI yield identical
worlds/checkpoints in all three states. Stale, source/receipt tamper, infeasible
required payload, work, cancellation and existing-output refusals preserve accepted
state and prior publication. [Current evidence](../../evidence/2026-10-31-regional-source-history-verification.json).

## Bounded qualification — complete program choices before regional allocation (2026-10-31)

Acceptance recorded before implementation: reuse the existing ranked program-mix
chooser for finite unplaced candidate analysis. Bind an exact full rank prefix
through an explicit composition selection contract; bound generation must not
silently advance to another choice. Regional search must measure each selected
complete program's actual envelopes, allocate its required space and qualify full
physical content/routes. Only typed physical infeasibility advances to the next
catalog-authorized complete program. Preserve all capacities, required children,
source dimensions, preference order, counts/constraints and original budgets;
never resize a source or reserve only the easy alternative. Retain source catalog,
selected prefixes, failed attempts and exact generation/analysis provenance;
rederive original source/selection at native read/export. Validate nested chooser
backtracking, lock mismatch/exhaustion, input/dependency/cancel safety, work/attempt
limits, SDK/CLI/JavaScript, source preservation and complete multi-region geometry.
No city/genre branch or mockup/Godot prerequisite. Terrain-count/mix simultaneous
solving, intent-region alternatives and full world-design acceptance remain open
unless actually implemented and qualified by this change. Original63tasks and
13requirements remain intact; Godot deferred.

Observed qualification: the shared chooser analyzes complete bound programs and
keeps nested decision order; native required-prefix generation preserves every
physical member and route. Caller capture, versioned-dependency mutation, callback
cancellation and exact bound-analysis work controls pass. Three complete regional
controls retain12nodes/9bodies and all five round trips each. Physical source
witnesses reject the owning alpha/beta/gamma16mhost choice before declared24m/28m
hosts qualify without changing their18mpayload,4mstorage or four required function
units. Enumeration and candidate work remain inside the original cap; baseline
work is219390. Component feedback, native source replay, canonical order, exact
work/one-below, rank/receipt tamper and typed no-output CLI failures pass.
Eight current-Core native scopes and28JavaScript checks pass. Independent checks
cover27bodies,30paths,180segment/solid comparisons, nine actual source witnesses
and36function units. The actual browser SVG preserves72body coordinates and ten
directed paths. [Evidence](../../evidence/2026-10-31-regional-program-choices-verification.json).
Regional source-history edits now have the bounded qualification above.
Simultaneous terrain-count/mix solving and full stage/core/design acceptance
remain unqualified. Godot/artwork deferred.




## Bounded qualification — regional physical connection feedback (2026-10-31)

Acceptance recorded before implementation: an explicit bounded feedback policy
must qualify the complete candidate world, retaining every body/source/journey.
A typed failed regional connection may reject only its owning region's selected
terrain-component assignment. Reuse the existing intent candidate/exclusion
owner; do not scatter anchors, resize sources, relax counts, change route widths
or invent roads. Keep all rejected/selected component receipts, candidate/world
hashes and measured planning/physical qualification work under existing limits.
Replay the source and whole feedback trace at native read/export boundaries;
malformed data, stage budgets, cancellation and invariant source failures do not
become layout retries. Qualify a connected complete world after an initially
unreachable component, no-policy failure, finite attempt/component exhaustion,
exact budget, preserved unrelated regions, SDK/CLI/browser persistence and
independent actual path/source geometry. This addresses regional connection
feedback; general physical redesign, composition and the full objective remain
open. Godot stays deferred.

Observed: two full48-node/45-body controls retain all five round trips, complete
source/surroundings programs, original0.4mactor width and85%open-ground rules.
The owning alpha component changes after an exact terrain disconnection proof;
unrelated region inputs and component choices remain unchanged. Larger64×50m
source compounds are retained. A62-node coupled-count world qualifies in one
physical attempt with every14derived target and all19round trips. Original
work caps remain: planner ledgers are1,763,583 for each disconnected control and
11,791,004 for the coupled control. Exact planner limit/one-below checks, finite
attempt/component exhaustion, canonical order, native saved replay, source/tamper/
cancel checks, seven current-Core native scopes and32JSchecks pass. SDK/CLI output
is byte-identical; two typed CLI failures publish nothing. Independent checks
cover149bodies,58paths,3,116segment/obstacle comparisons and two exact disconnection
witnesses; actual SVG matches360body coordinates and ten directed paths.
[Evidence and scope](../../evidence/2026-10-31-regional-physical-feedback-verification.json).
General physical redesign and full-stage/core/design acceptance remain open.




## Bounded qualification — coupled regional ownership and terrain counts (2026-10-31)

Acceptance recorded before implementation: optional explicit finite iteration under
one existing aggregate work budget. Initialize from unchanged catalog minima;
measure terrain-derived counts on each actual candidate regional surface and
boundary through the shared analysis owner, then recompute complete source
capacity. Accept only a deterministic fixed point of required area, clearance
and weights. Keep compact input/output and habitat receipts for every attempt;
refuse cycles, exhaustion, infeasible geometry, invalid context and work excess.
Bind a source habitat boundary only through an explicit context declaration;
refuse authored source overrides. No policy means existing behavior. Native
replay must authenticate final analyses and the complete derivation. Verify
complete portable compounds, nested content, original source sizes, protected
terrain, open-ground requirements and all original journeys with added measured
content. Check source-size/terrain counterfactuals, canonical ordering,
persistence, tampering, cancellation and original static regressions. Physical
placement remains an additional mandatory generation gate. This increment does
not close the 63-task/13-requirement objective. Godot/artwork remain deferred.

Observed qualification: four complete62-node/59-body worlds converge in two
iterations each. Every original source, surroundings domain,85%open-ground rule,
protected560m²area and all19round trips/world are retained. The terrain exclusion
edit changes actual target poses and paths; larger complete source envelopes and
reversed spatial demand change actual ownership. Six current-Core native scopes,
35JSchecks, exact SDK/CLI publication, typed count/exhaustion no-output refusals,
independent area/count/body/route checks and actual browser/SVG inspection pass.
[Evidence](../../evidence/2026-10-31-regional-coupling-verification.json).
Cycle refusal is implemented but no deliberately cyclic physical fixture has been
qualified. Source composition/adaptation analysis, physical-feasibility-driven
regional redesign, broader domains/design and stage/core completion remain open.




**Previous qualification — resolved-terrain program analysis (2026-10-31):**
The existing engine now prepares actual source habitats and expands their complete
terrain-derived programs before consumer placement. Root envelopes can be measured
without generating population poses. Native generation, original sources and
journeys remain mandatory. Bound-context repair receipts now attribute preparation
work before attempts; the viewer checks the unchanged aggregate budget. See the
[S07 owner](s07-environment-and-gameplay-population.md#bounded-qualification--resolved-terrain-program-analysis-2026-10-31)
and [bounded evidence](../../evidence/2026-10-31-terrain-program-analysis-verification.json).
Bounded ownership/count fixed points are qualified above; this stage's complete exit gate remains open;
Godot/artwork remain deferred.

## Bounded qualification — density-aware regional ownership (2026-10-31)

Extend the current ownership owner with an explicit optional ownership environment,
selected by the existing population pattern's density field. Use the original
terrain planes and field/clamp pieces in the shared mass-partition kernel, not a
flat surrogate. A measurement-only surface omits exclusions so the complete outer
mask remains owned; actual placement and journeys keep every original exclusion.
Unknown terrain refuses rather than acquiring invented density. Uniform requests
retain their existing route and serialized contract when the option is absent.

Acceptance: analytical integrated mass and weighted shares on affine, clamped,
derived and piecewise terrain fields; changed density changes ownership/anchors;
complete 48-node/45-source connected programs retain original dimensions, access,
open ground and ten directed journeys. Source-size/content variants, independent
request order, saved replay and source-history edits must use ordinary generation.
Missing/unused fields, negative/zero mass, incomplete terrain, infeasible geometric
capacity, tampering, finite work and cancellation refuse. Qualify public SDK/CLI,
JavaScript interpretation and exact schematic geometry, with immutable evidence.
No cap increases or reduced content/dimensions are authorized to pass these checks.
This advances S05.5; all 63 tasks/13 requirements remain open at their current scope.
Godot/artwork stay deferred. See the density-measure decision in the research owner.

[Qualification evidence](../../evidence/2026-10-31-ownership-density-verification.json)
now records seven current-Core native scopes,18 JavaScript checks, three CLI
publications/four typed refusals and source/history replay. Seven complete outputs
retain354 bodies,288 required content members and76 directed paths under independent
checks. The additional concave control preserves extent and complete content.
Exact SVG ownership masks,84 physical bodies/672 body coordinates,18 source-domain
masks,72 pins and4 regional paths match native data. Source size, counts, actor
widths, budgets and open-ground requirements were not relaxed. This qualifies
explicit density ownership; terrain-dependent program analysis, arbitrary topology,
coupled repair, streaming and broad world-design gates remain open.

**Previous qualification — portable complete population programs (2026-10-31):**
Explicit source context now inherits the caller's actual surface, origin and named
root anchors through the existing population owner. Static analysis retains both
source and consumer programs, source facts, relative layouts, access, connections
and installed provenance. Regional capacity measures complete root envelopes;
ordinary generation still qualifies every nested member, surroundings domain,
required open ground and original local/regional journey. No copied world poses,
source overrides, recursive sources or arbitrary placement are introduced.

The connected 336×224m control retains 48 nodes/45 physical sources and ten directed
journeys; the larger declared program retains 87/84 and sixteen journeys. Terrain
and source-size changes keep complete content. A one-region equipment-intent edit
preserves unrelated complete packets and replays source/history checkpoints.
Nine native scopes, 22 JS checks, public SDK/CLI equality/refusal, independent geometry
and actual browser/SVG inspection qualify this bounded complete composition.
[Reusable inputs](../../../examples/metadata/portable-population-world/README.md), [commands, source hashes and limits](../../evidence/2026-10-31-portable-population-verification.json).

This does not close broad world-design acceptance. Density-aware ownership,
terrain-dependent count/composition analysis, coupled repair, selective locks,
independent streaming and broad complete-world qualification remain open. All
original 63 tasks/13 requirements stay IN PROGRESS; Godot/artwork remain deferred.

**Previous qualification — complete program analysis and demand-derived ownership (2026-10-31):**
The public `AnalyzeProgram` API and ordinary generation now share one catalog
expansion implementation. Analysis retains complete nested membership, authored
relative coordinates, capacity/variation/mix choices, connections and exact source
and installed dependency provenance without inventing unbound coordinates.
Analysis is explicitly not a placed or physically validated world.

Optional regional `Capacity` measures actual expanded root envelopes using the
existing placement/footprint owner. The declared maximum coverage derives area;
source radius derives anchor clearance. Authored minima remain hard and authored
weight is a bias multiplied by required area. Nested sources are retained and are
not double-counted. Ordinary generation verifies the analyzed members and source
choices, then still performs full placement, access and route validation.

The source-size edit derives240→360 required square units and proportional
ownership while preserving15 members,9 physical sources and10 directed local/
regional journeys in both worlds. Independent generation, CLI byte equality,
source-history replay and failed-edit preservation pass. Analysis also qualifies
platformer/space/dungeon hierarchy, relative layouts, complete capacity/mix/
variation choices, repeated root bindings, rotated registered source clearance,
capture/dependency changes, exact work, size bounds and cancellation.

Nine focused native scopes plus the strengthened analysis rerun,20 JavaScript
checks, two CLI generations and independent whole-source/path/terrain equations
qualify this increment. Browser inspection and downloaded SVG retain15 members,
9 whole source polygons and10 physical paths. One pre-existing test warning remains;
CLI is clean. [Exact commands, source hashes, failures and limits](../../evidence/2026-10-31-program-ownership-demand-verification.json).

Uniform-area ownership and simple regional polygons remain bounded capabilities.
The newer portable-population qualification adds static source/consumer analysis; terrain-demand/composition coupling and placed SourceWorld analysis still refuse.
Density-aware ownership, complete ground-content design, coupled repair, selective
locks, streaming and broad complete-world design qualification remain open. All
original63 tasks/13 requirements and the full core/design gate remain IN PROGRESS.
Godot/artwork remain deferred.

**Previous qualification — derived generation ownership and program anchors (2026-10-31):**
Complete regional program templates can now omit ownership/domain polygons, root
origins and selected root anchor coordinates. The common area kernel derives
weighted full-domain ownership; the existing terrain region owner derives actual
suitable anchors. The current regional generator still expands/places every nested
program and qualifies all local and regional journeys. Excluded terrain stays
owned without becoming playable. Ownership does not impose streets or storage cells.

Complete rectangular, weighted, moved-void and concave controls retain15 nodes/
9 sources each.4/8/16-region controls retain20/40/80 nodes and12/24/48 sources at
the same extent, source dimensions, widths and budgets. Independent requests,
source edits, clean equality, saved source history and refusal preservation pass.
The identical small-unit harness fails on the prior engine's implicit1-unit²
threshold and passes now, preserving a complete0.25-unit² metadata domain.

Nine native scopes,22 JS checks, three CLI successes/two expected refusals and
actual80-node browser/SVG inspection qualify this bounded increment. Independent
checks cover seven complete physical packets:120 whole source polygons,40 derived
anchor clearances,14597 source-height equations,146 directed paths and12544 solid-
edge clearances. The final SVG retains80 nodes,48 whole sources,62 physical paths
and80 labels. One pre-existing test warning remains; CLI is clean.
[Exact source, commands, retained failures and limits](../../evidence/2026-10-31-derived-ownership-verification.json).

Initial ownership uses explicit uniform-area weights and simple outer boundaries.
Automatic source-demand weights, density-aware ownership, complete ground-content
generation, coupled repair, selective regional locks, streaming and broad complete-
world design qualification remain open. All original63 tasks/13 requirements and
the full core/design gate remain IN PROGRESS. Godot/artwork deferred.

**Previous qualification — catalog-driven region capacity (2026-10-31):**
Region demand now comes from actual expanded root program envelopes through the
existing placement/source owner. Game-authored coverage derives area minima;
source envelopes derive conservative anchor clearance. Larger sources enlarge
their derived region without losing required nested members. Content-dependent
region refusal advances bounded program selection and retains request/work counts.

The complete baseline, larger-source edit and selected-alternative worlds each
retain11 nodes,9 physical sources and10 directed physical paths. Clean/SDK/CLI
data, saved history, source invalidation and failed-edit preservation qualify.
Eight native scopes,11 JS checks, four CLI successes/two expected refusals and
actual browser/export inspection pass. Independent checks cover4752 source-height
equations,27 whole source polygons,30 directed paths and1056 solid-edge clearances.
The prior no-policy world is identical except two expected build-provenance IDs.
One pre-existing test warning remains; CLI is clean.
[Exact source, commands, failed attempts and limits](../../evidence/2026-10-31-region-demand-verification.json).

These are conservative capacity requirements, not packing or full ground-content
proofs. Automatic generation ownership, complete declared ground coverage, coupled
repair, selective regional locks, streaming and broader complete-world design
qualification remain open. All original63 tasks/13 requirements and the full
core/design gate remain IN PROGRESS. Godot/artwork deferred.

**Previous qualification — terrain-derived shared-component regions (2026-10-31):**
Several required semantic region intents can now share suitable connected terrain
through the common region and connected area owners. Hard minimum area/clearance,
protected seeds, holes and explicit distance relationships remain enforced.
The seedless framework example derives three regions, places three complete nested
programs and connects all destinations:11 nodes,9 physical sources and10 directed
paths. An outer-terrain edit retains all content and matches clean generation;
saved replay and failed-edit preservation pass. No source size, movement width,
terrain lattice or budget was reduced.

Ten focused native scopes, five saved-JS cases, two CLI successes/two expected
refusals and actual browser/SVG inspection qualify this bounded increment.
Independent equations check3168 saved heights,18 whole source polygons,20 directed
paths and768 segment/solid-edge clearances across the two complete packets.
One pre-existing test warning remains; CLI is clean.
[Source, exact commands and limitations](../../evidence/2026-10-31-region-partition-verification.json).

This is semantic subdivision with one normalized suitability profile and declared
split grammars. It does not derive independent generation ownership or completed
ground content; relationship rejection retries component assignments rather than
enumerating every feasible split. Automatic program-driven macro ownership,
coupled repair, selective locks, streaming and broad complete-world design remain
open. Original63 tasks/13 requirements and the full core/design gate remain
IN PROGRESS. Godot/artwork deferred.

**Current — fixed-budget interior size/shape range (2026-10-30):**
The existing shared solver now checks metadata-supplied contact constraints against
already selected current-stage sites. Search and prescribed modes share predicate
acceptance; complete assignment, walls, openings and routes still revalidate all
contents. This prevents candidate-budget exhaustion caused by waiting until an
entire sixteen-room assignment is built before rejecting an impossible contact.
The two sixteen-room loop cases now generate at the unchanged2048 candidate cap.

Historical2026-10-30 result, superseded for connected-program capacity by currentS10.
All sixteen declared size/shape attempts are retained. Twelve complete rectangular/
hexagonal cases at6/12/16 rooms generate; four24-room requests refuse the unchanged
16-demand searched-stage limit. Count errors report actual expansion and mode.
No cap, source shape/size, required fixture/edge or movement width was changed.
Independent checks cover272 physical sources,130 openings,260 directed routes and
385792 segment-to-solid edge clearances. Eight initial successful physical/graph
sets remain identical. The prior engine fails the new boundary-loop regression;
the current engine passes, including saved replay and full-width geometry.

The existing mixed-world24-case corpus remains23 generated/1 intentional terrain
refusal, with all23 prior successful physical/graph/progression sets identical.
Native capacity, prescribed scale and interior-population regressions pass; both
Release builds have zero warnings/errors. Seventeen size/shape and25 mixed-world
saved-JavaScript checks pass. Actual timing/allocation/peak observations are
retained without claiming a universal performance or whole-world design result.
[Declared inputs](../../../examples/metadata/generated-interiors/size-shape-range/study.json) and [commands, prior failures and source identities](../../evidence/2026-10-30-interior-size-shape-verification.json).
The63-task/13-requirement gate stays IN PROGRESS; Godot/artwork remain deferred.

**Current — connected contact-span layout grammar (2026-10-30):**
The common physical growth owner now derives staggered candidates from two already
placed graph neighbors. Their support faces must share a declared normal plane;
the intersection of their source-face translation intervals supplies the child
position. Both graph links and the direction remain in provenance. All ordinary
source, spacing, doorway-width, collision and swept-path checks remain required;
no source resizing, extra room, new grid, scatter or budget change is used.

Both previously unresolved branch-plus-loop cases now generate complete six-room,
six-fixture interiors with six actual doorways each. All eight controlled cases
pass. The full24-case corpus produces23 worlds and keeps the intentionally blocked
terrain refusal. All21 prior successful physical/graph/progression sets remain
identical; placement work/proofs change. Independent checks cover96 exact source
polygons,44 shared-boundary openings and88 directed routes; both new contact-pair
midpoints/gaps have source-face projection witnesses. Regression passes at0/23°,
fails on the isolated prior engine, and full-width wall/obstacle checks plus saved
replay pass. Connected/mixed-growth regressions and25 saved-JavaScript cases pass;
Release builds have zero warnings/errors. Finite grammar/scale and broader whole-
world design acceptance remain open; this does not prove all possible layouts.
[Commands, exact comparisons and retained limits](../../evidence/2026-10-30-contact-span-verification.json).
The63-task/13-requirement gate stays IN PROGRESS; Godot/artwork remain deferred.

**Current — mixed growth and fixed physical world intent (2026-10-30):**
The common placement owner now lets a connected growth subset coexist with
independently anchored physical sources in the same stage. Growth membership is
its root and declared link endpoints; disconnected link components refuse.
Other demands require consistent positive-weight exact pins. They place first,
then all grown bodies must satisfy original source, overlap, terrain, link and
feedback constraints around them. No arbitrary candidates, resized sources,
dropped members, larger budgets or second placement solver are introduced.

The exact same full indoor/outdoor input previously refused as a disconnected
growth graph. Current regional-route/network cases retain 27 nodes/26 physical
sources each: two real outside structures with ports, six rooms and original
fixtures, all twelve new equipment members and the metadata root. Source room,
wall, opening, entrance and regional/network paths remain exact after population.
Station-intent edits, clean equality, checkpoint replay, blocked-root rollback and
cancellation pass. Twelve independent candidate/rectangle oracles and saved-vertex
checks qualify eight forward/return journeys across four packets. Existing pure
connected branching/rotated/compact geometry is unchanged.

Actual current API/CLI worlds/checkpoints match. Seven affected native regression
scopes, four embedded-JavaScript packet checks and clean Release builds pass.
[Sources, commands, retained attempts and limitations](../../evidence/2026-10-30-mixed-growth-verification.json).
[Reusable inputs](../../../examples/metadata/mixed-growth-world/README.md).

This advances coherent physical composition, not whole-world design, arbitrary
volumes, all genres or unlimited scale. The 63-task/13-requirement core audit stays
IN PROGRESS. Godot/artwork and runtime playability remain deferred/unqualified.

**Source-derived boundary qualification — 2026-10-30:**
Explicit disconnected ordinary allocations preserve all original cells, boundary
loops and summed area when AllowDisconnected is requested. The split reference
retains both 48 m² components against its original 80 m² minimum. Default connected
ownership, area-balanced intent and regional seed policies retain their checks.
[Sources, commands and bounded limits](../../evidence/2026-10-30-generated-boundaries-verification.json).
Stage/core remain IN PROGRESS; Godot/artwork deferred.

**Acceptance before explicit disconnected allocation ownership — 2026-10-30:**
The boundary-surroundings case exposes a C-shaped supported region whose eastern
allocation consists of two islands. Existing power/area allocation owners correctly
refuse disconnected ownership. Add an explicit AllowDisconnected policy to ordinary
local program declarations; preserve connected ownership by default. The shared
component owner must retain every clipped component, original cells, exterior/hole
edges, total area, seed membership and original minimum-area requirement. One
program seed may own that explicit union; do not silently assign omitted pieces,
move anchors into excluded terrain, lower the 80m² program minima, create bridges
or relax physical source/terrain/placement constraints. Area-balanced intent and
regional seed policies remain unchanged. Qualify opt-in success, default refusal,
source/publication/edit replay and explicit boundary-loop selection on the actual
split allocation. This is region ownership, not physical/gameplay connectivity.


**Current — joined grid/irregular regions with complete physical programs (2026-10-30):**
The shared engine now qualifies one irregular-boundary program containing a local
grid beside irregular allocations, all seven parent sites and fourteen physical
stations, and three explicit bidirectional interfaces. Regional alignment ends
at the shared boundary. At fixed catalog, node poses, source shapes and budgets,
removing guidance changes route geometry; adding water moves the selected
crossing from (50, 15.5) to (50, 7.75) and retains the rejected attempt.

The combined case exposed duplicate probe/final edge qualification and expensive
domain checks before hard terrain/solid rejection. The common planner now caches
exact directed point pairs within each request and qualifies terrain/solids first.
Both constraints remain mandatory. The existing suitability owner adds exact
boolean whole-footprint checks; full placement/report decomposition is preserved.
The same water input that exhausts the prior binary's budget now succeeds without
increasing budgets. Exhausted approach work identifies its route and interface.

Twenty-four C# scopes pass after a stale provenance assertion was reproduced on
the prior binary and replaced with explicit relation-construction/tamper checks.
The original failure remains recorded. Fifty-three analytic/report-equivalent
predicate cases, four JavaScript tests, three actual CLI publications and the
final Release build pass (zero warnings/errors). Independent checks cover all
18 directed journeys and 58 conservative passage envelopes. Actual browser
selection/focus and downloaded SVG retain 22 nodes, 21 physical source polygons,
seven reservations and all source paths/crossings.
[Exact sources, commands and limits](../../evidence/2026-10-30-mixed-route-regions-verification.json).

[Reusable inputs](../../../examples/metadata/mixed-route-regions/README.md) distinguish
allocated program area from built content: 7,823.5 m² allocated, 336 m² physical
footprints and 560 m² clearance reservations. This is the complete declared
reference scope, not an entire settlement. S04.5 bounded technical acceptance is
qualified; the S04 mixed-settlement design gate, S04.7 policy audit and the full
63-task/13-requirement core audit remain open. Godot and production art are deferred.

**Coordinated source and required population — 2026-10-30:**
The earlier spacing failure is resolved in the common C# pipeline. Source
layouts remain provisional until full consumer placement passes. A witnessed
source-root/area-member conflict creates a finite attributed pose nogood; source
placement is reconsidered while terrain, environment, region, allocation, root
and source-envelope dependencies stay fixed. Prescribed future centroids, source
dimensions, counts and required clearance are not changed. Accepted source nodes
and all nineteen source connections then remain exact.

Both original density references now pass: baseline has 248 nodes/247 physical
members and all 219 required population members; richer density has 270/269 and
all 241 members. Each uses three provisional source layouts, accounting for
25235930/25957340 placement work within the original 200M ceiling. Independent
linear-density integrals, weighted centroids, complete source-sized polygons,
one-meter root-envelope spacing and all 38 directed full-width paths pass.

Replayable receipts preserve original inputs and rejected/accepted source
identities. Provisional caches are isolated; only the accepted stage cache is
adopted, and terminal chunk identity includes the accepted receipt. Attempt/work
refusal, missing/tampered proof, multi-pose constraints, unsupported-socket refusal,
region/program-composition selection, successful demand edits, stale-guard removal,
no-op reuse, failed-edit rollback and checkpoint replay pass. Six focused C#
scopes plus extended session checks, three actual CLI generations, expected atomic
CLI refusal, three JavaScript cases and actual browser/SVG geometry have
[versioned evidence](../../evidence/2026-10-30-population-coordination-verification.json).

This qualifies invariant root-area spacing coordination, not a general solver
for source-source combinations, nested/moving parents, route/contour-derived
population or route-blockage repair. Those failures remain explicit; no arbitrary
repair, count reduction or global-optimality claim is introduced. Next: extend
shared coordinated constraints to those dependencies and continue the S10
structural/terrain/domain matrix. S05–S10 and the professional generator remain
IN PROGRESS. Godot and artwork remain deferred.

**Historical source-contact snapshot — 2026-10-29 (population conflict resolved 2026-10-30):**
The shared ordinary placement search now derives finite candidates from semantic
terrain/region boundary vertices and exact resolved source-envelope vertices.
Reservation geometry takes precedence when declared. Mesh tessellation edges do
not multiply this contact family; the existing suitability, union containment,
separation, access, dependent-stage and work/candidate limits remain mandatory.
No object is resized, dropped, randomly scattered or moved after source freezing.

The original source now keeps all six compounds, both forest research compounds,
29 nodes, 28 physical members and nineteen bidirectional routes. Five analytic
cases cover rotation, reservations and both coordinate limits; exact work,
cancellation, impossible packing and typed/file replay checks pass. Seven current
C# command scopes, actual source CLI generation, two JavaScript cases and
independent source/route arithmetic qualify this increment. These are finite
vertex contacts, not a complete packing or continuous optimization solver.
[Exact sources, commands and remaining limits](../../evidence/2026-10-29-source-contacts-verification.json).

**Full population remains unqualified:** the unchanged large terrain-demand test
now reaches prescribed population placement, retaining all 219 demanded members.
Independent geometry finds one 0.70045146045m reservation gap to a frozen research
compound where 1m is required. CLI refusal publishes no full-world metadata.
Do not lower counts, move a frozen source, relax clearance or change centroids as
an unrecorded repair. Next: coordinate upstream provisional source choices with
downstream population/clearance constraints before source commitment, preserving
the same complete inputs and bounded work. S05–S10 and the professional generator
remain IN PROGRESS. Godot and artwork remain deferred.

**Diverse physical programs and source-footprint anchors — 2026-10-29:**
The shared allocation-anchor owner now checks the actual rotated catalog source
footprint/reservation against the complete generated region union. When its
preferred anchor does not fit, bounded boundary/source contact candidates supply
verified alternatives. It does not shrink sources, fill protected holes, scatter
objects or claim a complete continuous packing solver. Early placement checks
reuse the physical-access owner to reject blocked approaches before descendants.
Constant-terrain classification queries avoid irrelevant outside edges; geometry
extraction and heightfield queries retain their original event sets and limits.

Two complete bounded examples retain seven different kind hierarchies: workshop,
depot, habitat, commons, field laboratory, outpost and lookout. Each contains 61
nodes, 60 physical members, 42 connected interactive destinations, 114 directed
journeys and 18 density-derived landscape members. Exact source sizes, required
counts, 25000m² minimum and original work budgets are preserved. Nine current C#
scopes, two actual CLI generations, four JavaScript cases, independent geometry/
source checks and browser selection/layers/fit/SVG export pass. This is technical
metadata qualification, not owner visual approval or complete-game acceptance.
[Exact sources, commands and limits](../../evidence/2026-10-29-diverse-programs-verification.json).

**Open observed failure:** the unchanged larger terrain-demand program still
refuses at root placement. The same failure reproduces with the prior delivery
binary: its finite representatives leave only three forest candidates for two
required research compounds, with no separated assignment. Measurement/count
checks pass before this failure. Do not lower counts, resize sources or raise
budgets to report success. Next: source-footprint/terrain-feature-derived site
candidates in the shared placement owner, then rerun this exact large program.
S05–S10 and the professional generator remain IN PROGRESS; Godot/artwork deferred.

**Connected landscape partitions and frozen-source preservation — 2026-10-29:**
The shared area owner now supports declared finite axis alternatives at each
recursive mass cut. Both child supports stay connected; later semantic failures
roll back provisional leaves/cells. Saved attempts distinguish rejected,
superseded and selected cuts, retaining weighted mass and source geometry.
Population layout adaptation rejects candidates that differ from its accepted
source plan before reanchoring frozen compounds; source scope/replay validation
remains mandatory.

Two complete bounded reference worlds now generate: 59/60 nodes, 58/59 physical
members and 16/17 landscape members, preserving all seven compounds, fourteen
structures, seven yards, fourteen caches and 82 directed paths. The protected
1.8m ridge, 25000m² minimum, 0.1 grade, source dimensions and work ceilings stay
fixed. Counts follow the original density rule; the earlier 18-member assumption
for this steep case was incorrect and belonged to the gentle examples. The base
steep support mass is 4203.242661..., giving `floor(mass/250)=16` without truncation.
Analytic C-shaped/rotated/unequal-mass tests, a 32-attempt/three-superseded rollback
case, full edit/clean equivalence, failed-edit rollback, checkpoint replay, three
CLI packets and independent JavaScript geometry/tree/source checks have
[evidence](../../evidence/2026-10-29-connected-area-verification.json). UTF-8 viewer symbols are corrected and guarded.
This qualifies the declared bounded cases, not general connected division,
maximum-scale throughput, all-game expressive range or owner composition approval.
S05/S07–S10 and the full generator remain IN PROGRESS; Godot/art/runtime deferred.


**Bounded interior anchors and steep-terrain source program — 2026-10-29:**
Allocation anchors and area representatives now accept an explicit interior-
clearance policy through the existing shared region search. Area integration keeps
the true mass centroid separately; it never moves mass or discards disconnected
pieces. Stored point/clearance/upper-bound/evaluation witnesses replay through the
engine and are checked by the schematic. Unresolved layout exceptions retain all
rejected settings and consumed work. A holed 104m² support independently matches
its analytic largest-circle bound. The full steep source program now preserves
43 nodes/42 physical members/82 directed paths at unchanged source sizes, 25000m²
minimum, 0.1 grade and budgets, accepting the 20th declared layout setting. Five
existing geometry/adaptation/population regressions pass. CLI/Node checks and
source hashes are recorded in [evidence](../../evidence/2026-10-29-interior-anchors-verification.json).
At that checkpoint the landscape phase remained unqualified. The connected
partition qualification now closes this bounded failure, while full professional
world-generator acceptance remains open.
S05/S06/S07–S10 and the overall goal remain IN PROGRESS; Godot/art/runtime deferred.

Current next boundary: expressive range and scale across distinct authored world
programs. The connected-partition qualification now completes the bounded steep
landscape case. Preserve all source/density/terrain obligations while qualifying
larger worlds and different structural programs; finite axis search is not a
universal topology solver or owner design acceptance.

**Generated terrain integration and required-space orientation — 2026-10-29:**
The existing layout adaptation owner now supports declared `AreaRotation` steps
for `AreaBalanced` programs, with signed nearest-change history, unchanged hard
intent/density/source geometry, finite work/attempt bounds and replay. Conservative
edge/cell bounding-box broad phases replace exhaustive geometry comparisons at
the existing shared owners. Twelve independent triangulated topology/perimeter
cases cover rotations, holes, disconnection and point contacts. Three complete
61-node/60-physical-member flat/ridge/valley worlds retain 82 directed paths at
unchanged counts, source sizes, 25,000 m² region minimum, 0.1 grade limit and work
budgets. A previously failing identical heightfield input now publishes. Three
orientation cases, seven C# regression scopes, six CLI packets, explicit refusal,
eight Node checks and initial browser orientation rendering are qualified in
[the evidence](../../evidence/2026-10-29-area-landforms-verification.json). Steeper ridge layout/centroid failures remain unresolved;
the gentle cases do not establish terrain-responsive expressive range or occupied-
world design acceptance. S02/S05/S07–S10 and the full generator remain IN PROGRESS;
Godot/artwork/runtime remain deferred.

**Terrain-derived required program spaces — 2026-10-29:** `AreaBalanced` now
accepts required program intents without local coordinates. The shared area owner
derives density-weighted spaces and centroid anchors, allocating mass in proportion
to each minimum-area requirement. Minima remain hard constraints; ordinary
environment population retains equal-mass behavior. Canonical source and generated
area/weight provenance stay separate. [Evidence](../../evidence/2026-10-29-program-spaces-verification.json) records analytical uniform,
unequal/odd-weight, linear-density and holed geometry, nested scopes, invalid-
centroid/input/budget/cancel refusal, and exact work limits. Four seven-node/two-
structure physical programs retain source dimensions and a required interface
route; density changes positions while content identities, terrain and regions
survive. Strategy selection, source/tamper replay, clean/edit equality, rollback,
checkpoints, four CLI exports/refusal and fifteen Node checks pass. Existing area
geometry, six-compound/194-member/19-route population, terrain-demand, anchor and
joint-layout regressions pass. S03/S05/S07/S08/S09/S10 and the full generator remain
IN PROGRESS. This is a bounded intent-to-space grammar, not autonomous game-purpose
design or proof of occupied-world coverage. Godot/artwork/runtime and browser/
design acceptance remain deferred/unqualified.

**Generated anchor partitions — 2026-10-29:** `AnchorBisection` now derives a
recursive cut tree from protected required-program anchors. Callers supply no
cut tree or grid. The current local owner computes principal anchor spread,
chooses balanced separable groups and retains the generated tree beside unchanged
canonical source. Existing terrain/parent clipping, connectedness, minimum areas,
interfaces and full source-sized placement remain mandatory. [Evidence](../../evidence/2026-10-29-anchor-partitions-verification.json)
records analytical three-program areas/spans, holes/concavity, rotation/translation,
single/nested scopes, ID collisions, a 32-leaf bound case, disconnected/resolution/
budget/cancel refusal and source/tamper replay. A seven-node/two-structure program
retains its physical content and interface route, supports strategy selection,
edits/rollback and checkpoint replay. Direct/alternative CLI packets, refusal,
four new and six existing-strategy Node checks pass. Existing binary/cut/strategy
regressions and the six-compound/194-member/19-route area population pass. S05/S08/
S09/S10 remain IN PROGRESS. Anchors and mandatory game content remain authored
intent; this does not synthesize whole-world purposes, roads or arbitrary layouts.
Godot/artwork/runtime and browser/design acceptance remain deferred/unqualified.

**Bounded recursive cut adaptation — 2026-10-29:** The existing local adaptation
owner now supports `PartitionOffset` with an explicit `CutId`. It tests declared
normal offsets in the current nearest-change order and shared work/attempt ledger.
Tree topology, angles, required content, minimum areas, protected anchors and
source dimensions remain fixed during this search. [Evidence](../../evidence/2026-10-29-cut-adaptation-verification.json) records four
complete seven-node/two-structure cases: positive, reversed-normal, signed-negative
and oblique movement. Each retains two rejected settings before accepting the
third; exact work boundaries, regional translation, infeasible/invalid targets,
range/budget/cancel refusal, clean/edit equality, rollback and checkpoint replay
pass. Four CLI exports and fourteen current Node checks pass, including existing
strategy regressions. The direct partition, strategy and joint-layout C#
regressions pass. One CLI rename access denial recurred; its isolated rerun passed,
with the cause still unconfirmed. S05/S08/S09/S10 remain IN PROGRESS. Cut trees and
permitted search ranges are authored reusable intent; arbitrary topology synthesis,
world-scale throughput and browser/design acceptance remain open. Godot deferred.

**Recursive local cut partitions — 2026-10-29:** The shared local layout owner
now supports `BinaryPartition` alongside grid and weighted strategies. A bounded,
provenanced cut tree allocates every required leaf exactly once, using arbitrary
cut angles against existing terrain or parent geometry. Existing connectedness,
minimum-area, source-footprint and interface checks remain mandatory. The optional
regional frame resolves cuts and anchors together. [Evidence](../../evidence/2026-10-29-binary-partitions-verification.json) records
analytical diagonal areas/spans, holes and concave terrain changes, malformed/
cyclic/shared/unreachable tree refusal, disconnected-space refusal, budgets and
cancellation. A seven-node/two-structure physical program preserves source sizes,
supports strategy selection, translation, edits/rollback and checkpoint replay.
Actual CLI exports and ten Node checks qualify current packet consumption;
browser/design acceptance is NOT RUN. A Windows publication access denial recurred;
the isolated rerun passed without a code retry. Its cause is still unconfirmed.
S05/S08/S09/S10 remain IN PROGRESS. Cut topology is authored reusable intent, not
automatic topology synthesis, roads, walls or whole-world grid generation.
Godot remains deferred.

**Joint layout qualification (2026-10-29):** One adaptation owner now coordinates
multiple declared scopes under one ledger and 64-combination cap. A complete
13-node/four-structure/two-route world needs two changes together; either alone
fails. Numeric/numeric and numeric/strategy cases, independent four-state/area/
interface/source/anchor checks, exact work/attempts, invalid scopes/Cartesian cap,
source-edit/rollback/checkpoint replay, CLI and sixteen Node checks have
[evidence](../../evidence/2026-10-29-joint-layout-verification.json). Shared route-
binding normalization fixes unsorted multi-interface feedback replay. Affected
single-owner/route/frame/strategy regressions pass. Stage remains IN PROGRESS;
maximum-scope/nested combinations, broad synthesis/scale and visual acceptance
are unqualified. Godot remains deferred.

## Active increment — coordinated adaptation of local layouts (2026-10-29)

Bounded implementation locally qualified; the stage remains IN PROGRESS. The
current adaptation owner coordinates the primary
layout with up to three explicitly declared additional scopes. Each scope uses
the current numeric-dial or strategy-option contract; keep one shared work/attempt
ledger and a maximum 64 complete combinations. Try combinations by summed declared
deviation with stable declaration-order ties. Qualify all contents and routes as
one complete assignment; do not accept independent partial repairs or reset budgets
per owner. Preserve source sizes, anchors, minimum areas, hierarchy and unrelated
layouts. Save every scope setting and replay the joint search on read/export.

Acceptance: a complete world needing two simultaneous changes, while either owner
alone fails; independent expected combination order and area/interface oracles;
strategy/numeric modes, malformed/duplicate scopes, Cartesian cap, exact shared
attempt/work limits, cancellation and tamper refusal; edits/rollback/checkpoints,
CLI and current JS witnesses plus single-owner/frame/strategy regressions. This
does not close world-scale or autonomous design/visual acceptance. Godot deferred.

**Regional relationship qualification (2026-10-29):** Optional inclusive
minimum/maximum Euclidean anchor distances constrain required regional roles in
the existing bounded assignment owner. Protected anchors, whole components,
profiles/minima and complete physical programs remain intact. Independent 36-case/
104-assignment oracles, three-role constraints, exact work/refusal, complete 17-node/
11-route integration, edits/rollback/checkpoints, CLI and six Node checks have
[evidence](../../evidence/2026-10-29-region-relations-verification.json). Current
geometry, physical-feedback and local-strategy regressions pass. The stage remains
IN PROGRESS; these relations do not create roads or certify traversal distance,
autonomous design synthesis, world-scale throughput or visual acceptance. Godot deferred.

**Declared strategy qualification (2026-10-29):** Baseline-first ordered grid/
weighted options now share the existing adaptation work/attempt limits and physical
owners. Source validation runs for all declarations before candidate-dependent
geometry. Both strategy directions retain complete seven-node programs, and a
17-node/11-route case exhausts local options before changing regions. Independent
area/interface/source-size/anchor oracles, exact budgets, malformed unused option,
source-edit/rollback/checkpoint replay, CLI and nine Node checks have
[evidence](../../evidence/2026-10-29-layout-strategies-verification.json). Current
fixed/dial/frame regressions pass. The stage remains IN PROGRESS; broad strategy
synthesis, world-scale performance and visual acceptance remain open. Godot deferred.

## Active increment — declared local strategy selection (2026-10-29)

Bounded implementation locally qualified; the stage remains IN PROGRESS. The
current adaptation owner accepts ordered, bounded
alternative declarations for its selected local layout. Try the baseline, then
authorized complete grid/weighted alternatives, retaining program identity, purpose,
anchors/frame, minimum areas, hierarchy, residual purpose and interface obligations.
Validate every alternative with the shared source validator before physical search;
malformed unused alternatives cannot hide behind a successful earlier candidate.
The existing common layout/placement/route owners qualify each full program.
Exhaust local choices before the existing composition cursor advances region or
program decisions. Preserve all original budgets, cancellation and source replay.

Acceptance: a complete program that fails its initial strategy and fits a different
strategy without moved anchors or resized sources; opposite strategy directions,
declaration order and all-infeasible refusal; malformed unused alternative and hard
contract-change refusal; exact attempts/work, cancellation, stale receipts and
checkpoint replay; actual CLI and JS witnesses, plus fixed/dial/frame regressions.
This realizes authored strategy options, not autonomous world intent, continuous
optimization, whole-world scale or visual acceptance. Godot remains deferred.

**Region-relative content qualification (2026-10-29):** Original local program
and grid offsets can follow the computed regional seed. Placement consumes explicit
allocation-anchor bindings; source geometry, counts and protected absolute inputs
remain intact. Four complete grid/weighted terrain cases, nested relative/fixed
scopes, two 17-node/11-route reselections, independent coordinate/area oracles,
rotation/width adaptation, source-edit/checkpoint replay and bounded/invalid refusal
have [evidence](../../evidence/2026-10-29-regional-frames-verification.json). Three
CLI packets and twelve Node checks pass with current fixed-layout regressions.
The stage remains IN PROGRESS; this is authored relative grammar realization,
not autonomous intent/strategy synthesis, world-scale or visual acceptance.
Godot stays deferred.

## Active increment — region-relative local content (2026-10-29)

Bounded implementation locally qualified; the stage remains IN PROGRESS. An
explicit frame on a local layout interprets its
program anchors and grid origin as offsets from the selected region seed. Keep
the original relative declaration in the plan; resolve translation only inside
the current layout owner, without moving fixed world anchors or rotating/scaling
sources. Add root placement anchor bindings to computed allocation program anchors
through the existing placement owner. No arbitrary point search or empty-space
fallback. Existing region, local partition, physical placement, adaptation and
composition owners retain hard constraints and budgets.

Acceptance: one unchanged complete content program follows terrain-derived region
anchors across translated terrain; both weighted and local-grid strategies, nested
allocations and mixed fixed/relative declarations; independent expected coordinates,
area/source-size/count/route checks; region reselection and layout adaptation;
malformed, bounded, cancellation and stale-plan refusal; source/checkpoint replay,
actual CLI/JS packets and master tracker evidence. This does not imply autonomous
selection of roles/strategies or complete game/visual/world-scale acceptance.

## Active increment — bounded local orientation search (2026-10-29)

Extend the existing adaptation owner with a `GridRotation` dial in degrees.
Rotation applies to the selected local grid about its declared origin; protected
program and placement anchors, dimensions, counts, interfaces and unrelated owners
remain unchanged. Reuse the existing finite nearest-step search, work/cancellation
budgets, complete physical qualification and saved replay. Do not infer a mandatory
world grid, rotate source structures implicitly or add arbitrary position search.

Acceptance: a complete source-sized world whose baseline allocation cannot contain
its required contents, but an authorized rotation can; independent rotated-edge
and area oracles; both rotation directions; exact anchors/counts/source dimensions;
physical interface paths; invalid range/target, bounded refusal and tamper replay;
actual CLI export and current JavaScript receipt checks. **Local checks pass:** both directions, source geometry,
checkpoint rollback/restoration, actual CLI refusal and seven Node checks have
[evidence](../../evidence/2026-10-29-layout-rotation-verification.json). The stage
remains IN PROGRESS. This increment does not close general strategy synthesis,
large-world performance or visual acceptance. Godot remains deferred.

## Active increment — physical feedback into region-component choice (2026-10-29)

**Bounded local qualification:** [Evidence](../../evidence/2026-10-29-region-feedback-verification.json)
records all six independent ranked two-role/three-island assignments, exact
exhaustion and charged exclusion proofs; complete mixed, region-only, protected-
anchor and local-adaptation fixtures with 17 nodes/11 routes after two regional
attempts; exhausted regions then an alternate full 9-node/5-route program after
four attempts. Required source geometry/parts/access, protected anchors, original
and cumulative proof budgets, invalid/static refusal, chunks, file/source-history
edits/rollback and five CLI/fifteen Node checks pass. Current region/mix/layout/
adaptation regressions pass. This does not finish S05 or establish autonomous game
intent, general strategy synthesis, throughput or visual acceptance.

Extend the existing composition cursor with ranked region-assignment decisions
after catalog-program decisions. The region owner enumerates its current complete
lexicographic assignments by excluding earlier component vectors; it retains all
profiles, minima, explicit seed claims and source geometry. Only typed downstream
physical rejection advances a configuration. Exhaust regional assignments before
advancing an earlier program mix; support region-only composition without requiring
an arithmetic mix. Do not create another placement/composition implementation.

Qualify protected local-layout anchors as well: a globally eligible anchor outside
the current region is a candidate-region conflict, not malformed source data.
Keep that anchor fixed and project context-dependent geometry outcomes through the
existing metadata layout adapter. Static validation, numerical errors and budgets
stay terminal; adaptation dials retain their own bounded owner.

Charge every regional proof, including exhausted assignment enumeration, to both
its original region budget and one cumulative composition-region budget. Share
selection-request, cache-byte, receipt-byte and attempt limits with composition;
bound alternatives separately. Invalid/ambiguous inputs, cancellation and all
budgets remain terminal. Keep original recipes unrefined; save selected region
plans and exclusion proofs in attempt receipts, and regenerate the complete search
on read/export. Lost receipts must not permit an excluded assignment to pass as an
authored initial choice. Chunk source and session semantics remain unchanged.

Acceptance: independent complete assignment-order/exhaustion oracle; a higher-
clearance component physically unable to hold the full declared program, followed
by a lower-clearance valid component; region-only composition; exhausted regions
backtracking to a different complete program mix; protected seeds, unchanged source
counts/sizes and physical paths; all-infeasible/invalid/cancelled/bounded cases;
file/source-history/edit rollback, required chunks, CLI refusal and JS receipts.
This is bounded joint realization of authored programs/region roles, not autonomous
world intent or aggregate optimal design. Broader strategies and scale remain open;
Godot stays deferred.

## Active increment — terrain-derived region intent and interior anchors (2026-10-29)

**Bounded local qualification:** [Evidence](../../evidence/2026-10-29-region-intent-verification.json)
records rectangle/triangle/holed-square and changing elevation-contour oracles,
all 49 two-role/three-island matching graphs, scarce-profile backtracking, protected
seeds, point contact, exact ownership/area, equivalent decomposition, minima/
precision refusal and exact aggregate work/evaluation/reference/state limits.
Two complete terrain variants retain all 17 nodes/16 physical members/11 routes;
source geometry, bindings, source-history edits/checkpoints, content-stage reuse,
CLI refusal and six Node checks pass. Existing region/layout/adaptation/composition/
demand regressions pass, with one parallel region-export access-denied trial and
a passing isolated serial rerun preserved. The sharing cause is unconfirmed.
This does not finish S05 or establish broad world/design/throughput acceptance.
That increment used declared area/clearance and profile ownership alone; the later
joint-composition work above adds bounded physical feedback. General region/content
strategy synthesis remains open.

Extend the existing region owner with required named intents rather than authored
seed coordinates. Each intent retains purpose/source, suitability profile, minimum
area and minimum interior clearance. Reuse the existing shared atomic terrain/field
partition and positive-edge component topology. Choose complete connected suitable
components, preserving concavities/holes. Existing explicit seeds remain protected
claims in the same owner; an intent cannot consume their cells or another intent's
positive-area ownership. Backtrack across profile/component alternatives rather
than assigning greedily or splitting a component arbitrarily.

Compute a component's interior anchor by bounded signed-distance branch-and-bound
over its real exterior/hole edges. Search rectangles are computational work units,
not gameplay grids. Retain attained clearance and a global upper bound within the
declared precision. Never certify a minimum using only an upper bound; ambiguous
minimum-clearance thresholds require finer precision or explicit refusal. Rank
candidate witnesses by attained clearance, area and canonical geometry order; the
assignment is the first complete lexicographic intent solution, not joint geometric
optimization. Preferences do not override hard suitability.

Bound atomic cells, candidate references, anchor evaluations, assignment states and
aggregate spatial work; cancel and refuse exhausted proof budgets. Save request,
candidate/assignment/clearance witnesses and selected seeds; rederive on read.
Resolve declared region-anchor bindings into the existing root placement request
without authored point coordinates, keeping the original template for replay.
Do not overwrite explicit anchors, remove required programs, resize sources or
scatter content. Existing composition and physical owners qualify complete worlds.

Acceptance: independent rectangle/triangle/holed-domain clearance oracles; eligible
components/point-contact topology; scarce-profile assignment that defeats greedy
selection; exact ownership/area/minimum checks; equivalent terrain decomposition;
invalid/ambiguous/limited/cancelled requests; full unchanged program requirements
on two terrain variants with derived anchors, physical access/routes, file/session/
checkpoint and CLI/JS witnesses. This removes explicit regional seed coordinates
for this strategy. Automatic game intent, local repartition/topology selection,
larger world throughput and broad visual acceptance remain open; Godot deferred.

Research: https://github.com/mapbox/polylabel (retrieved 2026-10-29) explains signed
distance and priority bounds for a polygon interior point. Our C# union-region
adaptation uses rectangular subdivisions and existing terrain geometry; no source
code, assets or dependency are imported. Source claims are not our performance
or correctness evidence.

**Heterogeneous regions, 2026-10-28:** Root seeds may declare individual terrain
and field suitability profiles. A shared atomic partition counts suitable land
once and rejects selected positive-area ownership conflicts. Local allocation,
placement and interface approaches resolve the actual root region's profile.
Irregular lowland/highland/woodland terrain counterfactuals and cross-profile
interfaces have [bounded evidence](../../evidence/2026-10-28-region-composition-verification.json).
Generated interior walls constrain traversal without changing base-region replay.
This does not supply automatic region intent or broad partition strategies;
S05 remains IN PROGRESS.

Plan date: 2026-10-26. This document specifies future work; it is not an implementation-completion claim. Status and evidence are owned by the [master stage tracker](../../ROADMAP.md#master-stage-tracker). Requirements, decisions and shared delivery rules are in the [master plan](../../ADVANCED_WORLD_GENERATION_MASTER_PLAN.md#current-staged-plan).

Dependencies: [S01](s01-shared-world-contracts.md), [S02](s02-terrain-and-spatial-domains.md), [S03](s03-gameplay-topology-and-regions.md), [S04](s04-terrain-aware-connections.md)

Requirements: W01, W02, W03, W04, W06. Responsible executor: Codex, including explicitly labeled self-review. Accountable product/design owner: Fahad. No external or independent reviewer is assigned or claimed.

## Purpose and current gap

### Active terrain-connected region work — 2026-10-27

Implement named, seed-selected connected suitable regions before local subdivision. The shared piecewise-affine environmental evaluator clips real terrain faces against hard field limits. Positive-length eligible interfaces connect faces; point contact does not. Region geometry retains concavities and holes as a union of convex pieces, with only exterior edges displayed. These pieces are an internal decomposition, not gameplay blocks, roads or render tiles. Multiple required seeds in one component are a conflict, not an implicit partition policy. Required seeds and minimum areas must survive; unclaimed suitable and ineligible area remain explicit.

Feed named regions into existing source-sized placement and replay them at read/export. Reject footprints or reservations that cross a region boundary or cover a hole. Acceptance: analytical sloping contours, diagonal exclusions and holes; disconnected/point-contact components; preserved required seeds and area accounting; terrain edits changing shape; unchanged shape under equivalent sampling resolution; deterministic replay and tamper rejection; bounded work/cancellation; placement respects generated regions; actual CLI and colored schematic evidence. This is the first region-shape capability, not completion of regular/irregular/mixed allocation, local interfaces, the core gate or the artwork chain.

Latest slice: [hierarchy evidence](../../evidence/2026-10-26-world-hierarchy-verification.json) adds dependent allocation inside exact parent polygons and contained local subregions, sharing work/attempt budgets across parent reconsideration. Region shape generation, adaptive partition strategies and access/capacity are still unfinished.

The engine must retain useful grids/blocks without treating them as the shape of every world. Existing parcel constraints also leave excessive residual land in some examples. Partition geometry must follow terrain, purpose, source capacity and access.

## Algorithm and design

### Continuous-domain nested layouts — bounded qualification, 2026-10-27

Whole-program review found that one layout per connected suitable terrain region
prevents adjacent different strategies on continuous ground. The earlier mixed
fixture uses disconnected river banks. Extend the existing WorldLocalLayoutPlanner
with optional ParentAllocationId: a child layout clips to that complete parent
allocation, preserving the root RegionId. One layout owns each root region or
parent allocation. Resolve dependencies deterministically before geometry;
reject missing/cyclic/cross-region parents and duplicate ownership. Existing
cell/work/program limits bound the entire hierarchy. Coordinates remain global.

Qualify a continuous irregular region with a macro partition, an ordered child
layout beside an irregular child layout, complete nested source-sized physical
programs and full-width connected travel. Require exact parent/child area
accounting, no artificial terrain split, declaration-order invariance, a shoreline
counterfactual, oversized/missing-content refusal, saved replay, session edits,
chunk references and exact JavaScript output. The renderer must interpret scoped
area receipts without double-counting ancestor and descendant allocations.
This shared capability has [focused implementation evidence](../../evidence/2026-10-27-world-mixed-program-verification.json): inland/coastal programs each retain 43 nodes, 42 physical members and 82 directed paths, with declaration-order invariance, invalid hierarchy/source-size refusal, saved replay, incremental equality and exact CLI/browser output. Independent arithmetic checks confirm all source footprints, child containment, 42-node physical connectivity and full path clearance. The example is a seven-compound integration fixture; broad composition/gameplay acceptance and the full S05 gate remain IN PROGRESS.

The common placement owner now supports explicit zero-gap boundary contact for adjoining source-sized floor/site programs; positive-area overlap and clearance-reservation overlap still reject. This is a placement policy, not a new partition strategy or automatic connection. [Interior evidence](../../evidence/2026-10-27-world-interior-verification.json) covers shared edges/corners, concave and rotated shapes, the unchanged default disjoint policy, positive spacing and a complete room/corridor program. S05's larger adaptive-layout gate remains open.

### Active bounded repartition work — 2026-10-27

The initial finite-parameter capability is now [locally exercised](../../evidence/2026-10-27-world-adaptation-verification.json), including grid and weighted-partition repairs, source-size/access constraints, unrelated-owner preservation, fatal limits, replay and actual CLI/browser output. Arbitrary partition topology, jointly changing multiple owners and broad game-world design acceptance remain unqualified.

Add an explicit adaptation policy for one owning local layout. Authorized finite dials adjust power-diagram program influence or oriented-grid cell width/height; they cannot move anchors, change programs, reduce minimum areas, remove interfaces or resize source content. Enumerate the baseline first and then bounded combinations by increasing dial distance with stable ties. Each candidate recomputes local allocation and runs the existing complete placement/access/route feasibility pipeline. Keep rejected geometry/placement outcomes and per-attempt work. Unrelated layouts remain fixed.

Use a single aggregate work budget across allocation replay and every placement attempt. Invalid declarations, numerical failures, cancellation and exhausted budgets are terminal; only explicit spatial infeasibility permits another candidate. Persist the original layout/placement declarations, policy, selected settings and attempts; rederive them at read/export. Acceptance requires a source-size/access failure repaired by actual boundary change, both supported policy families, unchanged content/anchors/other layouts, finite all-fail behavior, fatal budgets/malformed inputs, replay/tamper and actual schematic evidence. This is finite parameter search, not arbitrary topology redesign or global optimality.

### Active physical interface work — 2026-10-27

The first implementation now has focused [API/CLI/browser evidence](../../evidence/2026-10-27-world-interfaces-verification.json) and a [mixed-layout fixture](../../../examples/metadata/local-interfaces/README.md). It qualifies entrance envelopes during individual candidate selection, then the crossing before either approach leg. Invalid site positions and blocked crossing attempts remain observable. Full-width search candidates and exact bounding-box rejection of remote region edges keep hole detours within explicit work limits. This advances S04/S05/S09; adaptive repartition is the next unimplemented local-layout capability.

Extend the existing metadata route rules with explicit connection-to-local-interface bindings. Reuse physical access endpoints, the full-width terrain traversal and bounded route search. Each direction consists of a source-allocation leg, a perpendicular crossing of a declared continuous span, and a destination-allocation leg. Whole swept envelopes, including entrance approaches and joins, must fit the corresponding allocation union and region profile. Do not constrain only centerlines or treat visible boundary contact as connectivity.

Search a finite ordered set of crossing centers on qualifying spans; preserve candidate failures and shared work limits. Domain boundaries supply route search features, not placed objects or roads. Replay the local layout and physical route composition at read/export, integrate failures into existing placement feedback, and leave all source content intact. Acceptance: analytical width and endpoint checks, holes/solid obstacles, a blocked midpoint with valid alternate crossing, reverse costs, allocation containment, entrance approach rejection, joined height continuity, deterministic replay/tamper, budget/cancellation, actual CLI and exact browser geometry. This does not implement bridges, multilevel connections, adaptive repartition or complete world content.

### Active local allocation work — 2026-10-27

Add game-authored local programs inside selected generated regions. Each program retains an ID, purpose, source, protected anchor and minimum area. Per-region policies are an explicitly oriented grid or a weighted nearest-anchor (power diagram) partition, clipped to the region's true terrain-derived union. The former may leave explicitly attributed residual land; the latter partitions the whole region. Neither implies streets. Different policies may coexist in different regions. Reject missing, disconnected, undersized or anchor-losing allocations instead of dropping islands or scaling content. Reuse the region geometry owner for connectivity and exterior/hole boundaries.

Declare required shared interfaces separately, with a continuous straight span requirement; expose their actual segments and selected location. A shared boundary is not a physical doorway, bridge or traversable route. Bind existing catalog-sized placement to allocation IDs, retain region/profile and hole checks, replay at read/export, and record assigned/residual area against the original parent region. Acceptance includes analytical grid and weighted partitions, concave/holed clipping, mixed policies, exact source dimensions, shared-interface continuity across terrain decomposition, deterministic identities, edits, insufficient-area/content rejection, budgets/cancellation, actual CLI and colored browser evidence. Adaptive repartition search and physical interface realization remain later work.

Use a shared spatial allocation contract with explicit boundary, reserved masks, content demand, interfaces and strategy parameters. Implement useful variants: regular/rotated grids and chunks; oriented subdivision; irregular polygon/frontage allocation; and region/graph layouts appropriate to supported interiors or open terrain. Select and blend strategies per region, never by a global city/genre switch. Supply true polygons/volumes and adjacency, not bounding boxes masquerading as occupied space. Allocate source-sized content and routes jointly through bounded redesign requests. Fixed programs, traversable space, purposeful grounds and unresolved land are separate measurements. County/country and settlement extents are authored or suitability-driven domains, not tiled city blocks. Partition only those regions needing allocation. A grid district can end at an irregular boundary while surrounding streets and terrain remain organic.


## Inputs and outputs

**Inputs:** Region boundaries and terrain masks, route envelopes, content/source requirements, permitted layout strategies, access obligations and partition budgets.

**Outputs:** Region/site allocations with exact geometry, adjacency and interface records; strategy provenance; complete allocation/remaining-space report; bounded redesign diagnostics.

## Code and workflow owners

Reuse UrbanBlockExtractor for its actual street-face responsibility and ConvexParcelGeometry/BuildingEnvelopePlanner for applicable geometry. Generalize only primitives with demonstrated non-urban consumers. Implement regional frontage policies through the new world allocation owner where requested; the rejected city parcel planner is removed. Extend registered layout contracts beyond point-only output when area allocation is required; migrate callers coherently.

Named existing owners are inspection targets, not a commitment to keep every current API. Proposed boundaries become concrete in S01. Replace superseded behavior at its current owner, migrate all callers and remove the retired implementation; no compatibility aliases or parallel legacy path. Preserve unrelated changes and immutable historical evidence.

## Ordered implementation tasks

| Task | Work | Acceptance evidence |
| --- | --- | --- |
| S05.1 | Separate terrain sampling grid, gameplay grid, generation partition and render tile grid in contracts. | Changing tile resolution cannot silently redesign physical content. |
| S05.2 | Implement reusable grid/block allocation for arbitrary region programs. | Blocks may contain rooms, resources or other game content, without requiring roads. |
| S05.3 | Implement irregular and terrain-aligned allocation with holes/exclusions where supported. | Coasts and cliffs remain real boundaries instead of being flattened into rectangles. |
| S05.4 | Compose different strategies within a world and join their interfaces. | An ordered district and an organic hillside region can coexist without global rotation tricks. |
| S05.5 | Feed source-size, density and access feasibility into allocation before placement. | Adding roads cannot satisfy density merely by shrinking the measured denominator. |
| S05.6 | Preserve protected anchors and revise only the owning layout on failure. | No scaled buildings, arbitrary relocation, silent clipping or legacy fallback is used. |

## Failure handling and operations

Validate inputs and required capabilities before expensive work. Apply explicit cancellation and finite work/resource budgets at stage boundaries and inside iterative work. Return attributed unsatisfied constraints instead of silent fallback, missing required content or partial success. Preserve prior accepted output on failure. Use the existing Log/Audit/Notification owners for applicable generation, export and review boundaries; UI consumers display actionable failures and cancellation. Record source/input identities, stage outcome and unresolved dependencies. Retry only after the cause or inputs change, with the same declared reproducibility guarantees.

Recovery is a coherent correction of the current owner and affected inputs, followed by focused checks. Do not restore a retired implementation as a runtime fallback. Artifact approval gates apply only to the relevant visual stages and exact revisions.

## Verification and acceptance cases

- Identical content demand on four terrains produces different valid partition shapes or an explicit infeasibility explanation.
- A grid-based tactical region and an irregular town region use the same allocation contract with different policies.
- Validate containment, overlap, holes, access, source dimensions and area accounting independently of the solver.
- Test mixed strategy boundaries, acute corners, thin strips, edits, work limits and stable unaffected identities.

Run relevant checks with commands, environment, input/source digests and observed outcomes recorded. NOT RUN is not PASS. Existing tests are baseline evidence only. Keep implementation, local technical qualification, browser/native review, owner acceptance and unavailable CI/release gates distinct.

## Risks and boundaries

Voronoi, BSP and skeleton subdivision each have domain limits. Support must be explicit; do not advertise all concave/volumetric geometry from a convex implementation. Full land partition is not full content coverage.

## Exit gate

S05 closes when regular, irregular and mixed layouts are produced and verified under shared contracts, with terrain-sensitive geometry and honest residual-space accounting.

When the exit gate is evidenced, update the existing master tracker with actual evidence and the next dependency-ready task. Do not close this stage merely because these instructions or task descriptions exist.

## Owner-supplied implementation references — 2026-10-26

R18 supports terrain/suitability-led region and destination selection. R20 provides geometry-tooling candidates for clipping, offsets, tessellation and skeleton-based local allocation; evaluate pure library boundaries rather than add a Unity runtime. Regional geometry remains irregular and game-dependent; parcels are an optional local specialization. See [R16–R22 and the implementation priority](../../WORLD_GENERATION_RESEARCH.md#owner-supplied-implementation-references--2026-10-26). This records a design decision, not integration or runtime evidence.

## Research references

[R01](../../WORLD_GENERATION_RESEARCH.md#r01), [R03](../../WORLD_GENERATION_RESEARCH.md#r03), [R05](../../WORLD_GENERATION_RESEARCH.md#r05), [R06](../../WORLD_GENERATION_RESEARCH.md#r06). These references motivate the proposed design; they do not certify this implementation or imply adoption of external code. The research record distinguishes accessed full text from abstracts and records relevant limits.

## Initial region allocation qualification — 2026-10-26

[Placement evidence](../../evidence/2026-10-26-world-placement-verification.json) adds fixed polygon demands allocated inside an authored search region, exact region containment, pairwise overlap/separation rejection and bounded backtracking. Sampling triangles supply candidates without becoming visible parcels. This begins S05; generated/adaptive partitions, staged hierarchical allocation, full constituent geometry and capacity/access remain unfinished.

## Terrain-connected region qualification — 2026-10-27

[Verification](../../evidence/2026-10-27-world-regions-verification.json) records analytical contours, dependent clamps, holes, disconnected/point-touching components, terrain edits, equivalent planar sampling resolution, exact seed identity, minimum-area failure, numerical resolution/budget/cancellation rejection, full catalog footprint/reservation containment, deterministic replay and export. Current CLI succeeds and rejects insufficient area without publication. Four JavaScript checks pass. The browser's two fills and all 28 exterior segments match 16 saved engine pieces exactly; no internal cell seams are drawn. A historical pre-hierarchy packet failed the current viewer's existing format checks; its inputs were regenerated through the current CLI for regression, without adding a compatibility path. S05 remains IN PROGRESS. Next: regular/irregular subdivision and explicit interfaces, retaining required content and honest area accounting.

## Mixed local-layout qualification — 2026-10-27

[Verification](../../evidence/2026-10-27-world-local-layouts-verification.json) records analytical regular/weighted areas, holes, attributed residual land, minimum-area/anchor/disconnected-allocation failure, continuous shared interfaces, rotated negative-origin geometry, terrain resolution independence, bounded work/cancellation, exact catalog placement, replay and CLI failure/nonpublication. The browser shows seven programs under two policies; all 29 convex pieces, 64 exterior segments and two selected interface spans match the engine export. A long source footprint fails instead of shrinking or omitting content. Seven sites and fourteen constituent metadata points are retained.

S05 remains IN PROGRESS. Partition area is reserved program space, not populated-world coverage. Physical interface realization, adaptive repartition after source/access failure, wider regional strategies and complete grounds/content acceptance remain open. The next step is to qualify actual connections across declared interfaces using the existing access/route owners, without treating every partition boundary as a road.
