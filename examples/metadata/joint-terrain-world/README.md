# Joint terrain and complete program generation

This reusable framework input declares an80×40m game domain and two regions.
The engine derives each actual suitable habitat and its required discovery count,
selects complete catalog programs, reserves their source envelopes and generates
every nested marker and journey. It uses terrain-only source contexts; no source
building, mockup, artwork or Godot project is required.

The catalog minimum is one discovery per region. Actual baseline habitats require
five each. Each discovery owns a full2.4×2.4m marker. The preferred2×2m parent cannot
contain it; the separately declared3.2×3.2m parent can. Both preserve the same child
and function. The engine retains the exact source failure, selects complete larger
parents and rebuilds the full decision list as terrain changes member counts.
It never shrinks a marker or drops a required discovery. The larger-source control
uses a declared3.6m parent; protected terrain changes the counts to four and five.
These dimensions, densities and roles are authored example content, not defaults.

`Coupling` and regional `ProgramComposition` opt into the shared joint planner.
Terrain quantities bind an exact mix count. Capacity/cost/source constraints,
parent bounds, original work limits and0.4m actor width remain mandatory.
Initialization, branches, failed source proofs, measured habitats, actual choices
and complete accepted geometry are saved in `OwnershipPlan.JointPlan`.

```text
generate-derived-regional-world examples/metadata/joint-terrain-world/request.json <new-output-directory>
```

The SDK uses `MetadataRegionalOwnershipPlanner.Plan` and
`MetadataRegionalWorldGenerator.Generate`, or the existing source-driven
`MetadataRegionalGenerationSession` workflow for attributed edits and rollback.
The output includes native world data, source/trace provenance and exact
JavaScript/SVG inspection. Native reads rederive the source; the viewer performs
structural checks and does not generate or relocate content.

`water-request.json` and `irregular-request.json` use the same required
exploration programs with a forbidden water obstacle and an irregular world
outline respectively. In a fixed six-case terrain study, five worlds generated
complete declared physical programs; protected ground changed the derived
discovery count. An all-water request was refused. Source-footprint areas and
route endpoints were checked independently, including sampled clearance from
the water obstacle. These are framework examples, not a configured game.

`generated-field-request.json` uses a seeded generated heightfield instead of
terrain patches. The same two regions retain all 22 nodes, 20 source-sized
physical sites and their two-way inter-region connection. Changing the generated
field's only kind to water causes the required ground regions to refuse before
program-alternative enumeration. `supplied-field-request.json` supplies the
same complete field directly; a recipe may also be supplied with a matching
field. `supplied-field-lifted-request.json` is a source revision that changes
the field values. `captured-terrain-request.json` instead requests a bounded
capture on the original terrain lattice; saved capture plans are validated on
replay. Complete supplied fields are not selectively streamed.
`captured-fine-request.json` uses 2.5 m terrain spacing and explicitly requests
membership region representation for the habitat, while preserving terrain
samples for area density, site and route calculations.

`captured-design-request.json` is a reproducible seeded design over the same
game-authored regional programs. Change `SeedPlan.Seed` to request another
terrain detail realization; the catalog, required content and routes stay explicit.
From the repository root, generate and verify one design with:

```text
generate-regional-design examples/metadata/joint-terrain-world/captured-design-request.json <new-output-directory>
verify-regional-design <output-directory>
```

The equivalent public SDK owner is `MetadataRegionalDesignGenerator`: read a
`MetadataRegionalDesignRequest`, call `Generate`, `PublishAsync`, and `ReadAsync`
to verify source replay. Seeded generation does not assume city layouts, a
particular game, artwork or Godot. A saved capture plan is tied to its recorded
terrain recipe and engine build; use an authored capture without a saved plan
when intentionally changing source terrain or moving to a different build.

```csharp
var request = await WorldMetadataGenerator.ReadAsync<MetadataRegionalDesignRequest>(inputPath, cancellationToken);
var designer = new MetadataRegionalDesignGenerator();
var generated = designer.Generate(request, cancellationToken);
await designer.PublishAsync(generated, outputDirectory, cancellationToken);
var replayed = await designer.ReadAsync(outputDirectory, cancellationToken);
```

These types are in `WorldCreator.Core.Metadata`; pass a new output directory.
`replayed.World` contains the saved regions and their connections, and
`replayed.Receipt` contains the seed derivations and design decisions.

The larger compound control also retains complete source programs, surrounding
ground, opaque bodies and all declared local/regional journeys. These technical
controls qualify joint mechanics, not broad whole-world design, all genres/scales,
selective authoring locks, streaming or visual/gameplay acceptance. Godot is deferred.

[Contract](../../../docs/WORLD_METADATA.md) ·
[Evidence and limits](../../../docs/evidence/2026-10-31-regional-joint-verification.json)
