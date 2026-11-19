# Water in the reusable world pipeline

This framework reference declares two required landmarks, two dry regions and a
bidirectional crossing. An explicit source/outlet produces a threshold-selected
stream. It is a small technical fixture, not a complete game or city.

`WorldMetadataRecipe.Water` optionally uses `WorldWaterDefinition`; it requires
`Drainage`. `WorldMetadataDocument.WaterPlan` stores the resulting provenance and
geometry. The original terrain stays in `DrainagePlan.Surface`; `world.Surface`
is the effective wet surface. Both remain validated rather than treating the
painted result as the original drainage terrain.

The stage graph is `surface → drainage → water → water-surface`. All actual
terrain consumers depend on `water-surface` when requested: source positions,
regions, allocation, access, environment, interiors, networks, routes, corridors
and chunks. Content expansion remains independent where its inputs are unchanged.
When water is absent, consumers use the ordinary `surface` stage.

```csharp
var generator = new WorldMetadataGenerator();
var world = generator.Generate(recipe, catalog, cancellationToken);
WorldMetadataGenerator.ValidateDocument(world, token: cancellationToken);
```

The existing `generate-metadata` command publishes the usual atomic packet plus
`drainage-plan.json` and `water-plan.json`, with wet segment/patch totals in the
summary. Strict world reads replay drainage, water and complete effective-surface
composition. A standalone water plan needs its context:

```csharp
new WorldWaterPlanner().Validate(world.DrainagePlan!, standaloneWaterPlan, token);
```

Sessions qualify width/source/height/threshold edits against clean generation,
explicit dependency invalidation, unchanged required nodes, failed/cancelled-edit
rollback, protected extents and source-history checkpoint restore. Required dry
regions or land-only crossings can make a water edit infeasible; the engine does
not remove them, move them arbitrarily or create a bridge. Existing population
source water facts must survive unchanged.

The schematic draws actual engine patches in blue, exposes flow/width tooltips
and retains ordinary terrain/region/route inspection controls. Its bounded
witness checks catch inconsistent source edges, widths, area ledgers, patch
references and effective-surface composition. Full numerical and digest replay
remains the C# engine's responsibility. Fit/export use native SVG label bounds
without including invisible hit rectangles or changing world coordinates.

The larger 7-compound/14-structure/7-yard/14-cache reference now passes
generation, independent clearance checks for all 82 directed paths, publication
and strict read-back. Its 59 nodes/58 physical members include 16 terrain-funded
landscape members. All original footprints, the 1.8m ridge, 25000m² minimum and
budgets are retained. The field has 140 active and three protected samples;
two wet edges retain 22 patches. An explicit retained sink respects the ridge's
closed basin without filling terrain. Independent rational union measurement
matches the original clipped channel envelopes: 15.492779740504433m².

Shared original coordinate anchors resolve the generated seam. Mass-only trial
integration avoids repeated full moments while retaining final topology and
accounting checks. The independent path oracle now accepts polygonal water and
retains analytic/rectangle checks. The dry-world edit/rollback/checkpoint
lifecycle and ten affected regression scopes pass. These bounded results and
earlier failures are recorded in [current evidence](../../../docs/evidence/2026-10-29-water-physical-verification.json).

See [water geometry semantics](../../terrain/water/README.md) and the current
[S02 tracker](../../../docs/world-generation/stages/s02-terrain-and-spatial-domains.md).
Generated climate/scalars, lake levels, broad world-design/scale acceptance,
production artwork and Godot remain outside this qualified increment.
