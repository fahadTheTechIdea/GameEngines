# Mixed regional layouts and physical crossings

Reusable framework reference for a complete **declared** seven-program scope.
The irregular whole-domain outline contains a planned local grid beside three
irregular allocations. An explicit program-domain coordinate separates the two
regions; the sampling grid does not create streets. A protected clearing stays
excluded. Every parent keeps its 8 × 6 meter source footprint and 10 × 8 meter
clearance reservation, with two separate 1 × 1 meter physical stations inside.
The result contains 22 metadata nodes and 21 physical members, including all
fourteen stations. Three explicit interface connections have qualified forward
and return paths with a 0.3 meter movement radius.

The same catalog is used by all three recipes:

| Recipe | Declared change | Verified behavior |
| --- | --- | --- |
| `recipe.json` | Western polygon prefers two undirected local axes | Alignment stops at the interface; eastern paths remain free-angle. |
| `free-angle.recipe.json` | Remove only the optional alignment preference | Same source geometry and poses; actual route geometry changes. |
| `water-crossing.recipe.json` | Add a 1 × 2 meter water barrier at the prior crossing | The crossing moves from (50, 15.5) to (50, 7.75); the rejected attempt remains in its receipt. |

All recipes pin the same seven parent poses for the counterfactual comparison.
The C# engine calculates the fourteen child placements, mixed allocations,
crossing candidates, full passage envelopes and route costs. Inputs keep the
same solver budgets and required content. Source validation, containment, terrain,
width and protected-area constraints remain mandatory.

Use the existing `generate-metadata` CLI/API with one recipe and `catalog.json`.
Its normal atomic output includes world metadata and a JavaScript schematic;
the viewer offers neutral SVG export. No additional generator or renderer is needed. The executable
fixture is `WorldMixedRouteRegionTests`; `WorldFootprintPredicateTests` compares
the optimized hard-eligibility query with complete reports and analytic cases.

This reference is not an entire settlement, a design-approved world or a claim
of support for every genre. Regional intent, source dimensions and program roles
are authored inputs. Production artwork, gameplay runtime and Godot are deferred.
See the current [master plan](../../../docs/ADVANCED_WORLD_GENERATION_MASTER_PLAN.md)
and [verification](../../../docs/evidence/2026-10-30-mixed-route-regions-verification.json).
