# Whole-footprint study

Four authored observation locations share a declared 16×16 meter footprint over generated irregular terrain. C# evaluates the full polygon against game-authored water-depth and slope requirements; JavaScript displays the exact polygons and decisions. Location 0 passes at its center but its footprint extends outside the domain. All four footprints remain visible as report-only failures; no required content is moved or omitted.

The square here is an authored test footprint, not a world grid. The reusable API accepts simple polygons with 3–64 vertices, including concave shapes, and nested catalog programs remain independent of this test shape.

Generate with the `generate-metadata` CLI using this folder's `recipe.json` and `catalog.json`, and a new output directory. The saved qualified packet is [generated/footprint-study-qualified](../../../generated/footprint-study-qualified/schematic.html). [Verification](../../../docs/evidence/2026-10-26-world-footprint-verification.json) records build, analytic tests, replay, CLI failure publication/audit and browser evidence.

This is an environmental constraint fixture. Purposeful placement, collision/access/capacity, structure composition, physical simulation, artwork and game runtime remain unfinished.
