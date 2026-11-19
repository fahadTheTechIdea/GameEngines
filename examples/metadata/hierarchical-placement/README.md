# Hierarchical physical placement

This fixture expands an irregular exploration region into two camps, four stations and four components: 11 nodes and 10 catalog-defined polygons. The camps reserve 20 × 16 meters, each station uses 6 × 5 meters and each component uses 1 × 1 meter. These are authored example dimensions, not measured artwork. Coordinates are calculated by C# from terrain, preferences and separation rules.

Six dependent stages allocate children within their parent's exact polygon. Child feasibility participates in parent backtracking. Required content and source dimensions survive export/reload; JavaScript displays the engine results without packing them again.

Run the CLI `generate-metadata` with this folder's `recipe.json`, `catalog.json` and a new output directory. The [reviewed schematic](../../../generated/hierarchical-placement-reviewed/schematic.html) contains the actual engine packet. [Verification](../../../docs/evidence/2026-10-26-world-hierarchy-verification.json) records focused checks and browser inspection.

This is a geometry fixture. Access, capacity, route collision with child polygons, junctions, production artwork and native Godot acceptance remain open. Parent polygons reserve space; they do not establish solid occupancy or complete functional facilities.
