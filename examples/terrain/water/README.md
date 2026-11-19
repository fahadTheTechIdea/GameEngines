# Occupied water surface: core framework example

The saved `drainage.json`, `definition.json`, `plan.json` and `surface.json`
demonstrate `WorldWaterPlanner` in `WorldCreator.Core.Spatial`. This is a reusable
terrain capability, without a particular game, city, art profile or Godot.

```csharp
var planner = new WorldWaterPlanner();
WorldWaterPlan water = planner.Plan(drainage, definition, cancellationToken);
WorldSurfaceDefinition wet = planner.Apply(drainage, water, cancellationToken);
```

`Plan` first replays the input drainage plan. It selects every outgoing edge at
or above `MinimumFlow`, and computes its declared width:

`width = BaseWidth + WidthPerRate * outgoingRate`

Widths use the surface's length unit; `WidthPerRate` uses that unit per declared
drainage rate unit. This is an explicit game rule, not a river-discharge model.
An excessive width fails instead of being clamped. A threshold selecting no
edges produces no new wet surface; terminal receipts do not invent lakes/oceans.

The shared 16-direction circular-envelope owner supplies conservative rounded
capsules with radius `width / (2 * cos(pi / 16))`. Their exact stored polygon is
the footprint authority; it encloses the requested circular radius. Caps overlap
at connected graph vertices so bends and converging branches have no gaps.

The occupied shape clips at the sampled field rectangle and shared domain/void
masks. The original width/envelope, retained area and clipped area remain saved
per edge. **Per-edge areas overlap at joins; their sum is not union coverage.**
The independent test's straight stream spans the complete 4×4 reference domain:
its wet union is a band of area `4 * .5 / cos(pi / 16)`.

The structured surface-face query retains the original elevation plane for each
wet piece. A completely covered, proved uniform planar footprint stays one patch;
other planes keep explicit pieces. No elevation field is filled, flattened or
carved. Authored overlay precedence is retained on shared closed boundaries.
Protected voids remain excluded and ambiguous source terrain refuses generation.
The output kind is game-authored (`water` here), so existing suitability,
placement and traversal APIs can apply their own rules.

The qualification test demonstrates two dry suitable components separated by
the water band; an unchanged fixed dry footprint explicitly fails; a separately
authored dry position still fits. A land-only route cannot cross, while a profile
explicitly allowing water can. Additional tests cover an angled tributary join,
92 pieces on nonuniform terrain, sub-sample masks, raised authored overlays,
negative translated coordinates, replay/tamper and finite failures.

The shared envelope now constructs symmetric rays from the same magnitudes.
The shared sweep canonicalizes only computed crossing coordinates within four
ULPs at the coordinate-operation scale. Distinct authored event coordinates stay
distinct, including the two-ULP regression case. Common topology cleanup removes
duplicate seam vertices; unrepresentable positive cells still fail rather than
being dropped. These are numerical geometry rules, not a map layout grid.

`MaximumSegments` defaults to 512 (cap 4096); `MaximumPatches` defaults to 1024,
also constrained by the combined surface's existing patch/vertex limits. Work
defaults to 20,000,000 (cap 200,000,000). Fragmentation can explicitly exhaust
geometry or downstream query budgets. `Priority` must outrank all source terrain
patches; output IDs must not collide. The plan retains source drainage digest,
source sample/edge IDs, rates, widths, patch IDs and threshold exclusions.

`Validate(drainage, plan)` and `Apply` replay both source drainage and water
geometry; plain JSON parsing grants no authority. Drainage replay and shared
surface input validation keep their own existing budgets; reported water work
covers water selection/envelope/face processing and output ordering.

The core is now exposed through optional `WorldMetadataRecipe.Water`, transactional
sessions and colored schematic export; see the [metadata example](../../metadata/water/README.md).
That bounded integration does not qualify the larger physical wet-world case,
whose region-geometry failure is recorded there. Derived climate/scalars remain unfinished.
It does not create bridges, lake levels, braids, erosion or hydraulic simulation.
See [S02](../../../docs/world-generation/stages/s02-terrain-and-spatial-domains.md).
