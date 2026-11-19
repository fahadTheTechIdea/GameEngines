# Drainage in a framework world recipe

This example uses the shared metadata pipeline, without a specific game, art
profile or engine runtime. It contains four required metadata nodes (one region
and three landmarks), two logical trail connections, a 5×5 inland heightfield,
explicit inflows, one outlet and deterministic chunk references.

`WorldMetadataRecipe.Drainage` optionally selects the shared terrain calculation.
The canonical `surface` stage precedes `drainage`; content and other stages stay
independent until they have an actual water/environment consumer. Chunk output
depends on drainage and keeps it as shared world facts. The current graph is not
occupied river geometry and its edges are not automatically game roads/trails.

```csharp
var generator = new WorldMetadataGenerator();
WorldMetadataDocument world = generator.Generate(recipe, catalog, cancellationToken);
WorldMetadataGenerator.ValidateDocument(world, token: cancellationToken);
WorldDrainagePlan? flow = world.DrainagePlan;
```

The existing `generate-metadata` CLI accepts these recipe/catalog files. Its
atomic packet includes `world-metadata.json`, `drainage-plan.json`, the ordinary
review/schematic and `summary.json` with explicit flow units/totals. The schematic
does not yet draw drainage. `ReadAsync<WorldMetadataDocument>` validates the world
and its drainage surface binding; `ReadAsync<WorldDrainagePlan>` replays a saved
standalone plan. Plain JSON deserialization alone remains insufficient.

Drainage requires the same bounded two-coordinate/unit frame as other metadata
terrain. Omitting it keeps topology-only and dry-world generation available.
Population source worlds preserve any existing drainage plan unchanged. A
population phase may add drainage when the source does not declare it; it cannot
silently remove or replace the source's existing flow facts.

`MetadataGenerationSession` uses the existing transactional stage cache. In the
qualification test, source and outlet edits recompute exactly `drainage` and
`chunks`; a height edit also invalidates `surface` and its existing dependents.
Unrelated content stays reused. Results equal fresh generation. Failed drainage,
protected edits and cancellation retain the prior accepted world/history/cache.
Published checkpoints replay source history rather than trusting saved geometry.

Generated documents use compact JSON up to 8 MiB, then a versioned manifest
and verified 1 MiB parts. Each file and authored input remains bounded at
16 MiB; complete generated documents are bounded at 256 MiB. Typed reads
reassemble and verify every byte before source replay. Generation's in-memory
resource bounds remain separate. Existing destinations are not overwritten.

See [core semantics and limits](../../terrain/drainage/README.md). Water width,
occupied geometry, placement/traversal effects, environmental scalar fields and
drainage visualization remain future work. This example does not certify a
complete playable world or Godot integration.
