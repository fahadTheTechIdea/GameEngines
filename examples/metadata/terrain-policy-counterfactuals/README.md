# Terrain policy counterfactuals

These four framework inputs keep one exact 2m × 2m solid destination, its named
entrance and required outward/return travel. The C# engine generates a usable
region and binds the destination to its calculated interior anchor.

| Recipe | Hard terrain change | Eligible area | Generated site position |
| --- | --- | --- | --- |
| open-recipe.json | Level dry ground | 240 m² | (10, 6) |
| water-recipe.json | Water at the western edge | 168 m² | (13, 6) |
| slope-recipe.json | Steep ground at the eastern edge | 144 m² | (6, 6) |
| both-recipe.json | Both restrictions | 72 m² | (9, 6) |

All recipes use `catalog.json`. Region/placement policies allow the declared land
and water kinds, then enforce depth zero and maximum slope 0.3 through explicit
field requirements. The traversal profile independently permits land travel.
Every policy retains its purpose, contributing field and source rule. A perfect
preference with weight one million cannot buy hard eligibility. Ownership and
computational geometry impose no streets or game grid.

Use the ordinary `WorldMetadataGenerator.Generate` or `GenerateFilesAsync` APIs;
the existing `generate-metadata` command produces identical packets. Current
public packets are under `generated/terrain-contract-public/<case>`. Four public
worlds exactly match the SDK/file API and complete persisted source replay.
The both-case source edit equals clean generation; an all-flooded edit preserves
the accepted state, and the complete history restores.

This is a controlled analytic framework example, not whole-world composition or
human design acceptance. Field sampling separately distinguishes unavailable
terrain from zero elevation; unknown data is never implicitly buildable. Art,
browser review and Godot are outside this qualification.
See [S02](../../../docs/world-generation/stages/s02-terrain-and-spatial-domains.md)
and [exact evidence](../../../docs/evidence/2026-11-01-terrain-policy-acceptance-verification.json).
