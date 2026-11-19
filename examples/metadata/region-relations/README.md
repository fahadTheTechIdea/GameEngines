# Related regional roles

A framework fixture with a required habitat program and a protected reference
region, not a configured game. A declared anchor-distance interval of 100–140 m
selects the long terrain component anchored at (120,0), relative to the protected
reference point (-6,-23). The independent distance is sqrt(126²+23²). The earlier
preferred square component is too close and cannot satisfy this requirement.

The same complete physical program retains 17 metadata nodes, 16 physical members
and 11 routes, with original source dimensions, descendants and access constraints.
The engine selects the relationship-compatible region in one composition attempt.
Changing the interval to 0–50 m selects the nearer region, where the complete source
program cannot fit; generation refuses instead of dropping contents or moving to
a distant region that violates the rule. An interval of 1000–1100 m has no regional
assignment and is refused without publishing output.

The test suite independently enumerates all feasible ranked assignments for 36
closed intervals over three known component anchors (104 solutions), verifies
exhaustion/exclusion and a greedy trap, and checks simultaneous three-role relations,
protected/fixed-pair constraints, invalid inputs, exact work, cancellation, tampered
proofs and source-edit/rollback/checkpoint replay. The existing CLI exports this
recipe/catalog using `generate-metadata`; JavaScript consumes exact engine output.

These are Euclidean anchor relationships, not physical roads or traversal lengths.
Reference-region geometry does not imply a generated reference structure. Broad
world intent/strategy synthesis, world-scale performance and visual acceptance
remain open; Godot and artwork are deferred.

[Verification](../../../docs/evidence/2026-10-29-region-relations-verification.json).
