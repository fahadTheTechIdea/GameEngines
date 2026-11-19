# Connected area partition framework inputs

These are reusable framework examples, not a specific game or art profile.
`metadata` exercises required program spaces through the same connected area
owner. `geometry-input.json` and `geometry-plan.json` are bounded geometry API
fixtures: a C-shaped 48m² support cannot use its preferred disconnected split;
a declared alternative produces two connected 24m² strata with complete mass.

`steep-world` and `richer-world` run the full terrain → generated spaces → nested
physical catalog → paths → density-funded landscape → chunk storage pipeline.
Both retain seven compounds, fourteen structures, seven yards, fourteen caches
and 82 directed paths. Source geometry, the 1.8m ridge, 25000m² region minimum,
0.1 grade and work limits remain unchanged. Base density `1+0.002*x` derives 16
landscape members (59 nodes/58 physical); changing only the density coefficient
to 0.003 derives 17 (60 nodes/59 physical). All members follow the original
`floor(mass/250)` rule and catalog minimum 16, without count clamping.
Earlier 18-member statements applied to gentle examples; the unchanged steep
source allocation has mass 4203.242661..., so its original rule yields 16.

The engine tries the declared finite axis list per recursive cut and checks
connected children. Failed later choices roll back provisional leaves/cells.
Rejected/superseded/selected records preserve complete search history and the
selected tree geometry/masses. Optional interior points retain true mass centroids
separately. Nothing is scattered into vacant space. Fixed source members and
all existing passage geometries are preserved through the population phase.

Use the existing `generate-metadata` CLI with each subfolder's recipe/catalog
and a new output directory. Current packets are `generated/connected-area-verified/*`.
[Verification](../../../docs/evidence/2026-10-29-connected-area-verification.json)
records analytic and independent geometry/mass/tree/count checks, full density
edits, rollback, checkpoints, refusals, CLI/browser scope and source identities.
The overall generator remains unfinished: this bounded surface reference does
not establish all-game expressive range, large-world performance, occupied-world
composition or owner design acceptance. Godot/artwork/runtime remain deferred.
