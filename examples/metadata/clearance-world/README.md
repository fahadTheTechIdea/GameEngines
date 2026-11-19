# Source footprints and reserved surroundings

This engine fixture places two required catalog sites on a flat 40 × 30 meter domain. Each has an exact 4 × 4 meter solid footprint and a containing 10 × 8 meter reservation. Rotations are 30° and −20°; neither polygon is resized. Named boundary entrances and outward approaches connect the sites through the ordinary route planner.

The C# placement solver checks whole reservations against the environment, parent bounds and sibling separation. The exported JavaScript schematic displays its saved geometry: filled physical footprints and dashed unfilled reservations. Reserved space remains available to circulation; it is not another solid or automatically populated surroundings.

Generate with the current CLI's `generate-metadata recipe.json catalog.json <new-output-directory>` command. The checked packet is `generated/clearance-world-reviewed`; [verification](../../../docs/evidence/2026-10-27-world-clearance-verification.json) records the exact scope. This is an algorithm fixture, not a complete city or game. Directional siting, shared services, physical junctions, art and native runtime remain unfinished.
