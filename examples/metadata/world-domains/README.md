# Independent world domains and explicit transitions

A framework source definition, not a configured game. The collection composes one
complete seven-node outdoor program and two independent seven-node interior floors.
Both floors intentionally reuse the same recipe, local coordinates and node IDs.
They stay separate worlds with no cross-floor collision merge.

The `entry` transition references the outdoor structure's existing east access port
and the lower floor's existing room-side opening. `stairs` references an opening
on the lower floor's exit room and the upper floor's entry opening. Existing
traversal/wall/solid owners qualify endpoint widths and clearance. Both transitions
are bidirectional. The domain-graph oracle is `entry, stairs` forward and
`stairs, entry` for return. This does not create a continuous 3D stair mesh or
perform runtime scene changes; local journeys remain in each world's route plan.

The current CLI `generate-world-domains` accepts `definition.json` and a new output
directory. It exports one root review index and complete independent per-domain
metadata/schematics. Tests verify unchanged local generation, exact endpoint
coordinates, domain separation, directed reachability, narrow/blocked/missing
refusal, aggregate counts/work, cancellation, source/read replay and atomic output.
A one-way variant fails the required return and publishes nothing. Logical-only
world domains also require no surface, artwork or physical endpoints.

All local coverage remains explicit. Broader world synthesis, maximum-domain and
large-world stress, continuous transition geometry, runtime behavior and visual
acceptance remain open. Godot and artwork stay deferred.

[Verification](../../../docs/evidence/2026-10-29-world-domains-verification.json).
