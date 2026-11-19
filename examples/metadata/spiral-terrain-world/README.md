# Spiral program on changing terrain

The flat and sloped recipes keep the same four solid spiral sites and required
bidirectional journey. They differ only in terrain gradient, so the saved route
cost reflects the changed terrain while the source footprints remain fixed.

The redesigned recipe additionally protects a terrain area and explicitly moves
the spiral center clear of it. The original center is rejected when that area
is protected. This is an authored design edit, not automatic center selection.
A separate steep-terrain check also rejects an untraversable required entrance.
The CLI reports the observed rejected demand and exclusion when a required
placement has no eligible candidate; the designer can use it to revise inputs.

An engine caller can instead authorize specific alternative centers. The first
valid choice retains the same physical content and route constraints:

```csharp
var selector = new MetadataPopulationSelector(generator);
var original = blockedRecipe.Placement!.Bindings[0].Population!;
var clear = original with { Spiral = original.Spiral! with { Center = [40, 40] } };
var result = selector.Select(blockedRecipe, catalog,
    new MetadataPopulationTarget(MetadataPopulationTargetKind.Root, "site"),
    [new MetadataPopulationChoice("clear", clear)], cancellationToken);
// result.Recipe and result.World are the matching sources for publication.
// result.Attempts records which authored choices were tried.
```

This only tries the original grammar and the explicitly supplied alternatives;
the same API also accepts other population grammars, such as paths.

For a replayable handoff, create a `MetadataPopulationSelectionRequest` with the
same recipe, catalog, target and choices, then call
`MetadataPopulationSelectionPackage.PublishAsync` with an operation reporter
and a new output directory. `ReadPublishedAsync` verifies the selection history
and the selected world together. The package does not establish who authored
the inputs or approve the world for a particular game.

The same package can generate alternatives from a bounded movement rule. For
example, supply `Alternatives: null` and a translation policy with vectors
`(-1, 0)` and `(-10, -10)` and `MaximumSteps: 1`. It tries the original, then
the two translated layouts through the full placement and route checks. The
policy and chosen result are saved for replay; the vectors express developer
intent and do not impose a grid.

`translation-selection.json` is a complete framework request for that rule.
The CLI's `select-population` command publishes it to a new directory, and
`verify-population` replays the selection and saved world from that directory.

`integrated-recipe.json` declares the same movement rule directly on placement.
The ordinary metadata generator tries it during placement, records the attempts
and publishes the selected world with the original recipe for source replay.

`coordinated-recipe.json` and `coordinated-catalog.json` demonstrate two
independent populations that must both move to respect protected terrain. Their
joint result retains eight structures and two required two-way routes.

`rotation-recipe.json` demonstrates rotating a population around an authored
pivot. `mixed-rotation-recipe.json` uses `coordinated-catalog.json` and combines
rotation in one population with translation in another; the 90-degree option
is refused and the 180-degree option succeeds without moving protected areas.
`rotation-selection.json` demonstrates the same rotation through the separate
selection-package workflow.

`rigid-recipe.json` combines a 90-degree turn and a translation for one
population; protected features reject the original and either change alone.
`rigid-selection.json` carries the same rule through the selection-package
workflow. Neither example requires an art profile or a game engine.
`joint-rigid-recipe.json` uses `coordinated-catalog.json` to combine that rule
with a separate translation for a second population, retaining all eight sites
and both required two-way journeys.

The C# `WorldMetadataGenerator` and `generate-metadata` CLI accept the recipes
and catalog. This small framework example is not a complete game or runtime.
