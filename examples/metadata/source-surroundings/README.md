# Catalog surroundings

This fixture demonstrates a reusable registered-source site program. Its house is an example, not a special generator branch. The C# engine reuses `BuildingSitePlanner` for catalog bands and the common placement solver for the site reservation.

- Physical source: Modern `village-house/cottage`, 8 × 10 meters, unchanged.
- Authored surroundings: front/rear depth 4 meters and left/right depth 2 meters; resulting reservation 12 × 18 meters.
- Source rules: rear garden with 0.25 meter inset, yielding 11.5 × 3.5 meters; source entrance width 1.5 meters and approach length 4 meters.
- Front is negative source Y; all geometry rotates together by 30° and translates to the declared center.

The current packet is `generated/source-surroundings-identity`. The viewer displays saved C# polygons and their rule provenance. An insufficient rear band rejects export instead of dropping the garden. [Verification](../../../docs/evidence/2026-10-27-world-surroundings-verification.json) records the focused checks.

This is a site-grammar fixture with two metadata nodes and no external route, not a complete world. Shared services, context, road policy, boundary assemblies, production artwork and native Godot remain open. Regenerate through `generate-metadata recipe.json catalog.json <new-output-directory>`; existing outputs are preserved.
