# Reusable regional program blueprint

This framework example defines one complete regional program and instantiates it
for four named regions. The shared program includes the complete recipe, catalog,
physical sizes, placement and access rules. The blueprint retains the ordinary
world terrain, regions' network endpoints and generation budgets. It is not a
game preset and does not invent content or a layout.

Use `MetadataRegionalOwnershipPlanner.ExpandBlueprint(blueprint)` and pass the
result to `MetadataRegionalOwnershipPlanner.Plan`, or use the CLI:

```text
generate-regional-blueprint examples/metadata/regional-blueprint/blueprint.json <new-output-directory>
```

The generated world uses the normal regional publisher and source replay. Its
saved source is the expanded ownership request; save the original blueprint as
the editable authoring input. Expansion does not retain a separate blueprint
history or automatically propagate later changes to published worlds.

`seeded-blueprint.json` contains one reusable four-region program with declared
terrain-detail and partition alternatives. `seed-a.json` and `seed-b.json` choose
two fixed world seeds. The C# API builds a design request using
`MetadataRegionalDesignGenerator.FromSeededBlueprint`, then generates and
publishes it through the normal design owner. The CLI equivalent is:

```text
generate-regional-seeded-blueprint examples/metadata/regional-blueprint/seeded-blueprint.json examples/metadata/regional-blueprint/seed-a.json <new-output-directory>
```

The two tested seeds retain the same complete required programs and journeys,
but produce different terrain samples and regional boundaries. Seeds are not
guaranteed to give a distinct or feasible result. The saved design receipt
contains the expanded source and derived layer seeds; retain the blueprint and
seed plan as editable inputs. Program selection requires explicit design slots
and is not inferred from a blueprint.

`design-a.json` and `design-b.json` add an explicit slot whose two options refer
to named blueprint programs. Their seeds vary terrain, partition choices and
program selection. The tested seeds select different source-sized compound
programs while preserving every required member and two-way connection. Options
reuse each named program's recipe and catalog; the instance's regional policy
stays unchanged unless an option sets `UseProgramPolicy` to true, in which case
the complete policy comes from the referenced program. Semantic bindings explicitly resolve the changed root's actual
network endpoint. Generate either with:

```text
generate-regional-blueprint-design examples/metadata/regional-blueprint/design-a.json <new-output-directory>
```

`MetadataRegionalDesignGenerator.GenerateBlueprint` generates the same design
for a C# consumer and retains the editable blueprint in the published receipt.
Replay checks its content hash and its expansion against the saved design. The
receipt also contains the expanded design and seed derivations. The snapshot is
structured data, not a byte-for-byte copy of the original file.

To resume authoring from a published blueprint-design packet, read it through
the same generator; this verifies the world and source before returning them:

```csharp
var engine = new MetadataRegionalDesignGenerator();
var published = await engine.ReadAsync(outputDirectory, cancellationToken);
var editableBlueprint = published.Receipt.BlueprintSource
    ?? throw new InvalidOperationException("This packet has no blueprint source.");
// Revise the blueprint, then use GenerateBlueprint and a new publication directory.
```

For an editable session with source history, pass the blueprint design directly
to the existing regional session instead:

```csharp
var session = await MetadataRegionalGenerationSession.StartDesignedAsync(
    new WorldMetadataGenerator(), initialBlueprintDesign, reporting, cancellationToken);
var before = session.Snapshot;
var editable = before.DesignSource!.BlueprintSource!;
var revised = editable with { SeedPlan = editable.SeedPlan! with { Seed = "another-seed" } };
var edit = new MetadataRegionalBlueprintDesignEditRequest("change-seed",
    "Revise the authored world seed", "example:seed", before.World.DefinitionSha256,
    before.Report.WorldSha256, before.DesignSourceSha256, revised);
await session.ApplyDesignAndPublishAsync(edit, newOutputDirectory, cancellationToken);
```

The checkpoint preserves each original named-program blueprint and authenticates
its expansion during restore. A source-only refactor updates the design-source
pin even when its generated geometry is identical. Use that pin from the current
snapshot for every revision. Failed revisions preserve accepted source/world state.
CLI equivalents are `region-session start-blueprint-design <blueprint-design.json>
<new-output-directory>` and `region-session edit-blueprint-design
<generation-session.json> <blueprint-design-edit.json> <new-output-directory>`.
The edit JSON has `Id`, `Purpose`, `SourceRuleId`, `ExpectedDefinitionSha256`,
`ExpectedWorldSha256`, `ExpectedDesignSha256` and `Source` (the revised blueprint
design), with optional `ProtectionChanges`. This route retains authored designs;
plain blueprint expansion remains a stateless API.

`design-policy.json` demonstrates complete policy selection: with its fixed
seed, the selected `wide` program supplies its increased allocation weight along
with its other regional policy. The region instance keeps its identity and
connections. It uses the same CLI command shown above.

`design-shared-options.json` demonstrates one set of program alternatives used
by two different regions. Each region resolves its own semantic endpoint and
selects through the ordinary world-design constraints. Edit the named set once
to change the choices offered to both regions.
`design-shared-options-mixed.json` uses another fixed seed; in the tested world,
the two regions select different programs from that same set.

`larger-modules.json` is an authored revision of the shared catalog program. The
public SDK [workflow](../../worldcreator-workflow/README.md#procedural-regional-source-workflow)
expands both revisions and regenerates all four instances using the normal
regional edit and checkpoint APIs.

Each instance declares an ID, purpose, source rule and program ID. Optional
weight, minimum area and minimum clearance override the referenced program.
Other settings—including any pinned anchor—are inherited exactly; define a
different program when those settings must differ. Endpoint references use the
instance's region ID and the program's existing local node and port IDs.

This input reproduces `../regional-networks/request.json` with 11,863 bytes
instead of 36,128 bytes. The verified world contains four regions, 20 nodes,
12 physical sites and three bidirectional connections. It does not establish
complete game design, general terrain variety, artwork or runtime integration.
