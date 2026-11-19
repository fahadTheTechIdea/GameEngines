# Bent regional boundaries

This framework example uses the shared regional ownership and network owners to
generate four game-authored regions, each containing its complete nested program.
The source requests `Pattern.PartitionSearch.Bend = 0.15`; the engine solves each
weighted area split with a bent boundary while respecting the declared terrain,
source dimensions, access ports, routes and budgets. It does not require an art
profile, a particular game genre or Godot.

`request.json` is the tested input. It generates 20 nodes, 12 physical sites and
three bidirectional regional connections. The saved schematic displays the
actual generated ownership, and source replay rechecks the world.

`Bend` is a signed slope in `[-1, 1]`. Zero uses straight cuts; a nonzero value
produces two connected straight arms at each split. The source's offset choices
and optional seed control the directions. This is a bounded piecewise-linear
grammar: it is not a spline, a map-style preset, or automatic geographic design.
The engine can still refuse an infeasible placement, route or work budget.
The general policy and API are described in
[World metadata](../../../docs/WORLD_METADATA.md#connected-recursive-area-partitions).

`offset-request.json` uses an explicit three-unit `BendOffset` with the same
complete four-region program. The arms meet away from each split's centroid,
producing asymmetric boundaries while the engine still checks area, terrain,
physical content and routes. This is a reusable spatial control rather than a
game-specific map preset.

`alternate-offset-request.json` also declares `AlternateBendOffsets`, allowing
the bounded partition search to choose among the primary and alternate bend
positions. The chosen positions are retained in the world witnesses. Seeded
ordering and physical feedback can reconsider declared positions, subject to
the same search and physical constraints; they do not invent new positions.

`resolution-request.json` is a larger three-region test with a 150-unit bend
offset and a full-height terrain barrier. It retains 48 nodes, 45 physical sites
and both directions of two regional routes. It tests source-edge precision; its
successful world retains the primary bend position and does not demonstrate a
physical retry to an alternate position.

`position-feedback-request.json` is a separate three-region counterfactual with
a barrier at x=200–210. The primary bend position cannot connect the required
journeys; physical feedback selects a declared alternate position and retains
all source members and routes. This demonstrates one bounded fallback, not a
guarantee that another suitable position always exists.

`feedback-request.json` demonstrates a separate three-region world with a
terrain barrier. The primary straight ownership split cannot connect the
required destinations; physical feedback tries the explicitly declared `-0.4`
alternate bend and retains all 48 nodes and both bidirectional regional paths.
The source declares the entire retry policy and budgets. It does not ask the
engine to relocate objects arbitrarily or invent an alternative.

`moved-feedback-request.json` changes only the barrier's position. With the same
content, route rules, allowed bends and budgets, the tested world selects the
straight boundary and reroutes its journeys. Applying this as a regional source
edit and then restoring `feedback-request.json` reproduces the original world.
An edit that blocks the entire domain is refused without replacing the accepted
world. This counterfactual demonstrates one bounded terrain response, not every
possible terrain or settlement style.
