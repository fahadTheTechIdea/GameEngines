# Placement with required access and connections

This small algorithm fixture uses the common game-world pipeline, with no city generator or art dependency. A required 4 × 4 meter solid site prefers (10, 5), retains a child marker, and needs a north-facing entrance and a route to the authored destination at (35, 25). An impassable strip divides the terrain at y=14–16.

The engine rejects blocked entrances and disconnected route candidates before committing positions. The current deterministic result is (10.5, 22.75), after 53 rejected complete candidates. Its entrance approach is (10.5, 16.75), on the reachable side of the strip. Nothing is resized or omitted. Coordinates and attempt history are computed in C#; JavaScript displays the saved data.

Generate through `generate-metadata` with the adjacent recipe/catalog. The reviewed packet is `generated/placement-feedback-final/schematic.html`. The inspector exposes rejected reasons, accepted attempt and aggregate work. Read/export replays the stored context and attempts.

This is a bounded feasibility demonstration, not a generated town or a finished game world. Capacity, full catalog siting, adaptive regions, shared junctions, artwork and native runtime remain open. See [verification](../../../docs/evidence/2026-10-26-world-feedback-verification.json).
