# Optional sampled drainage

These are framework terrain examples, without a game, settlement, art profile or
Godot dependency. Each folder contains the source `surface.json`, authored
`definition.json` and a replayable `plan.json` from `WorldDrainagePlanner`.

| Example | Declared design | Expected result |
| --- | --- | --- |
| plateau | Flat inland 5×5 samples, one outlet, two inflows, 50% loss per outgoing edge | 25 participating samples; input 24, loss 22.5, received 1.5 |
| retained-basin | Closed quadratic basin with an explicit central retained terminal | Original heights preserved; input and received both 24 |
| disconnected | Narrow protected barrier between columns, one terminal in each component | All 25 samples retained; edges cannot cross the barrier; input and received both 24 |

```csharp
var planner = new WorldDrainagePlanner();
WorldDrainagePlan plan = planner.Plan(surface, definition, cancellationToken);
planner.Validate(plan, cancellationToken); // also after deserializing a saved plan
```

The API is in `WorldCreator.Core.Spatial`. The planner snapshots the shared
`WorldSurfaceQuery`, including replay of generated heightfield terrain. Sources
and terminals reference exact zero-based heightfield columns/rows; excluded or
out-of-range references fail. Source rates are explicit per-sample inflows in the
declared `RateUnit`, not rainfall per square meter. A retained basin is a terminal
receipt, without implied lake level, storage capacity or water footprint.

The graph follows the existing heightfield triangle edges: horizontal, vertical
and lower-left to upper-right diagonal. Its sampling lattice does not prescribe
roads, districts or objects. Reverse shortest paths select non-increasing height
edges to the nearest reachable declared terminal. Equal-distance choices use
stable sample indices. Positive path distance supplies a strictly decreasing
potential on flat terrain. This convergent routing grammar cannot represent
braided rivers, and its directional sampling bias is explicit. It is not a
steepest-descent or hydraulic simulation.

Every participating sample must reach a declared terminal, including samples
with no local source. An unresolved basin or disconnected component fails with
`WORLD_DRAINAGE_UNREACHABLE_SAMPLE:<index>` and diagnostic counts/work; no ocean,
fill, carving or partial successful graph is invented. Shared domain/exclusion
geometry qualifies every edge, including masks narrower than the sample spacing.
Overlay height profiles are checked across the complete edge; hidden peaks,
troughs and discontinuities cannot be passed by endpoint-only tests. Non-uphill
comparisons are strict; numerical ambiguity may conservatively refuse a route.
This is sample-graph coverage, not continuous catchment-area coverage.

At a nonterminal node, local input plus incoming flow loses the declared fraction;
the rest reaches its downstream node. Terminals retain the full received amount
without another edge loss. Receipts, per-node incoming/outgoing/lost/retained
amounts and a global conservation residual are saved. Surface digest, canonical
source/terminal ordering and full replay detect altered plans. Call `Validate`
explicitly after deserialization; generic JSON parsing is not validation.

`MaximumSamples` defaults to 16,384 and can be 4..263,169; the whole heightfield
counts toward this bound, including masked samples. `MaximumWork` defaults to
20,000,000, capped at 200,000,000. The reported work covers graph sampling,
edge qualification, reachability and flow accumulation; surface/terrain input
validation retains its own existing bounds. Cancellation is checked throughout.

The core is now exposed by optional metadata recipe `Drainage`, document
`DrainagePlan`, strict saved-plan validation and transactional sessions; see the
[metadata example](../../metadata/drainage/README.md). Water width/occupied
geometry, climate/wind fields, placement/traversal effects and schematic display
remain unimplemented for generated drainage. Existing authored water and other
world capabilities remain separate. See the current [S02 plan](../../../docs/world-generation/stages/s02-terrain-and-spatial-domains.md).
