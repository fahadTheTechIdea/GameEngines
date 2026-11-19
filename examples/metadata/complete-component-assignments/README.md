# Complete terrain-component assignments

These reusable framework controls exercise physical redesign across qualified
terrain components. They are technical fixtures, not a game or completed world
design. Godot and artwork remain deferred.

The network and authored-link requests contain two complete location programs
in a 100×80m world. A protected horizontal band disconnects northern and southern
ground; another exclusion leaves the first location only a northern component.
The second location initially prefers southern ground. The generator must keep
the first location and move the second to its qualified northern alternative.

The 300×80m three-location request requires two locations to change components.
The third has only northern ground. A four-attempt complete-assignment search
finds the northern connected design, preserving all 15 nodes and nine physical
sources. The two-location controls each retain ten nodes and six sources.

`PhysicalFeedback` uses the current shared component frontier and intent owner.
It rejects complete assignments, never a viable local component in isolation.
Receipts retain qualified domains, context hashes, requested/actual vectors,
failed authored/network evidence, search work and accepted geometry. Finite
attempt limits are distinct from exhausted complete domains; invalid inputs,
work limits and cancellation are never physical retries.

Use `generate-derived-regional-world` with one of this folder's request files
and a fresh output directory. The JSON/SVG schematic contains actual engine
coordinates. Native read/export replays the source and complete proof; source
sessions preserve failed edits and exact rollback. See the
[contract](../../../docs/WORLD_METADATA.md) and
[bounded qualification](../../../docs/evidence/2026-10-31-complete-component-assignments-verification.json).

This finite component search does not implement continuous anchor optimization,
joint partition/source/layout redesign, all genres, independent streaming or
developer selective locks. A qualified technical fixture is not a playable-game
or visual-art acceptance.
