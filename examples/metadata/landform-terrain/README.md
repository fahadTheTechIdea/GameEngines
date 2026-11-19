# Generated landform field

This example generates a 33×33 elevation field over a 128×128 meter sampling extent, clipped by an authored irregular polygon. The C# engine evaluates a base plane and seeded detail, adds a curved polyline ridge, lowers a crossing trough and levels a sampled anchor. The single metadata marker identifies the authored anchor; no settlement or structure layout is generated.

`recipe.json` owns the ordered landform operations, their provenance, sampling resolution, semantic layers and explicit incomplete scope. `catalog.json` supplies a generic region. Field samples are generated before metadata validation. There is no city algorithm or visible block-layout requirement.

Internal reproduction uses the normal CLI and a new output directory:

```powershell
dotnet run --project src/WorldCreator/WorldCreator.Cli -- generate-metadata examples/metadata/landform-terrain/recipe.json examples/metadata/landform-terrain/catalog.json generated/landform-terrain-review
```

The packet includes saved recipe/catalog, generated sample data and a schematic with elevation/slope selection. The viewer displays the saved field and embeds its rasterized layer in SVG exports. Colors show triangle means/slopes, not biome, water or suitability classifications. The current terrain kind is `ground`; negative elevation does not automatically mean water.

Edit input operations or detail parameters and generate into a new directory to compare designs. Existing generated output paired with a changed recipe is rejected as stale. Leveling is exact at sampled control-path nodes, with interpolation elsewhere. Features narrower than the sample spacing need higher resolution; no continuous feature-error guarantee is claimed.

[API, numerical limits and current area-query boundary](../../../docs/WORLD_METADATA.md#generated-elevation-fields-and-replayable-landforms). [Local verification](../../../docs/evidence/2026-10-26-world-terrain-verification.json). Environmental layers, suitability-based destinations, connected regions, nested content, art and native Godot remain separate unfinished stages.
