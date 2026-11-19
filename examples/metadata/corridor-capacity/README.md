# Shared passage capacity

Diagnostic extension of the reuse-aware route example. Two independent providers each deliver
four game-service units through a different logical connection. Both connections use the same
twenty-meter physical trunk. Each logical connection has capacity four; each physical segment
has an explicit shared limit of eight.

The shared trunk consumes 8/8 units, while four other segments consume 4/8. The viewer colors
the full trunk purple and the available segments teal, with exact usage/limit values in titles
and per-consumer segment reservations in the inspector. These capacities are game inputs,
not values inferred from width or real traffic engineering.

Reducing the declared per-segment limit to seven fails before publication. Opposite directions
share the same pool. A delivery chain that traverses one segment twice consumes twice its
quantity there. The existing source/link/segment ledger reserves all dimensions atomically.

Focused tests additionally demonstrate bounded placement reconsideration after physical
capacity failure, persisted replay, tamper rejection and source-vertex precision during reuse.
See [verification](../../../docs/evidence/2026-10-27-world-corridor-capacity-verification.json).

This does not simulate timed queues or congestion, infer capacity, support separate directional
or multiple-unit pools within one group, or certify physically independent alternate routes.
Adaptive regions, world design, production artwork and native runtime remain open.
