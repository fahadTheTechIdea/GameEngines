# Reuse-aware exploration passages

This diagnostic fixture declares four destinations and two bidirectional journeys. The first
connects `(10,20)` to `(90,20)`. The second connects `(40,25)` to `(60,25)`.

The selected route rule declares `Reuse.NewPassageCostPerMeter = 5`. The second connection
therefore uses two five-meter connectors and twenty meters of the established trunk. Its
physical distance is 30m, reused distance 20m, new-passage cost 50 and physical terrain cost 30.
The direct alternative would cost 20 + 20*5 = 120. The chosen bounded candidate costs 80.
The return direction can reuse the established bidirectional journey without new passage.

With reuse disabled, the second connection stays direct. A small new-passage cost also keeps
the direct route. This is an explicit gameplay/design control, not a universal preference for
concentrating every path. Source paths are requalified against current terrain and must match
the requested traversal profile. No city blocks or global grid are generated.

The common corridor pass realizes five physical segments and six surface junctions. The
viewer draws those saved coordinates once. See [verification](../../../docs/evidence/2026-10-27-world-route-reuse-verification.json).

This is deterministic sequential routing over a bounded candidate graph, not a globally
optimal network or an accepted world design. Shared physical capacity, independently resilient
alternatives, adaptive regions, production artwork and native game runtime remain open.
