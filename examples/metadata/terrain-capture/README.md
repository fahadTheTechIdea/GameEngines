# Persisted terrain capture

This framework input retains two game-authored region policies, seven nested
physical programs, fourteen solid stations and their required journeys. The
irregular world boundary, source dimensions, required counts, physical widths
and budgets remain declared. It is an engine example, not a configured game.

`recipe.json` opts into `Surface.TerrainCapture` and logical region
`Representation: Membership`. `catalog.json` owns the complete nested program.
The original513×513 lattice covers more than the declared world. C# calculates
and saves just44×36 original samples, with their global offsets and source/build
identity, through the existing metadata generator, session and publisher.

```csharp
var generator = new WorldMetadataGenerator();
await generator.GenerateFilesAsync(
    "examples/metadata/terrain-capture/recipe.json",
    "examples/metadata/terrain-capture/catalog.json",
    "generated/my-new-capture-output");
```

The ordinary `generate-metadata` CLI accepts the same files and a new output
directory. Published JSON, review and JavaScript/SVG inspection retain every
member. A saved capture replays on read; edits must create new exact samples,
and failed/cancelled changes preserve accepted state. Full-field generation
remains the default without `TerrainCapture`. Global drainage/wind/water need
complete fields. Captures are not independently generated content chunks or
runtime streaming; the full world/design gate remains open. Godot/art deferred.

The qualified fixture has a positive elevation datum. Its separate retained
exact-zero-datum control rejects a nonzero clamp sliver below the current geometry
resolution. No tolerance or work limit was relaxed to make that request pass.

[Contract](../../../docs/WORLD_METADATA.md#persisted-bounded-terrain-capture),
[S08](../../../docs/world-generation/stages/s08-deterministic-chunks-and-persistence.md),
[commands and limits](../../../docs/evidence/2026-10-31-metadata-terrain-capture-verification.json).
