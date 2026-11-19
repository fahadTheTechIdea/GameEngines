# Scoped destination-network fixture

This reusable recipe/catalog exercises the common world engine with four required exploration destinations inside an authored irregular surface. The engine generates five bidirectional connections (ten directed route results), using terrain costs, passage width and a single-connection-failure requirement. Water is excluded by the traversal profile. No city generator is involved.

`recipe.json` declares the surface, layers and scoped network rule. `catalog.json` expands the region and four destinations using an authored path grammar. These anchors and landforms are test inputs, not generated terrain or suitability-based placement. The output explicitly remains `PartialScope`.

Internal reproduction from the repository root, using a new output directory:

```powershell
dotnet run --project src/WorldCreator/WorldCreator.Cli -- generate-metadata examples/metadata/destination-network/recipe.json examples/metadata/destination-network/catalog.json generated/destination-network-review
```

The CLI saves the normal metadata, catalog, recipe, review and JavaScript/SVG schematic packet. The viewer uses engine coordinates and route widths; circles are markers, not structure footprints. Connections may overlap geometrically without becoming physical junctions. The authored root and first destination coincide, so their labels overlap in this diagnostic fixture; no label/composition acceptance is claimed.

The focused C# suite is `WorldCreator.Tests --world-networks <WorldCreator.Cli.dll>`. JavaScript verification is in `tests/javascript/world-network.test.cjs`. [Recorded verification](../../../docs/evidence/2026-10-26-world-network-verification.json) identifies exact local checks and remaining gates. [API and limits](../../../docs/WORLD_METADATA.md#scoped-destination-networks).
