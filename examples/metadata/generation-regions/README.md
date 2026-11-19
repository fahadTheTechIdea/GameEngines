# Independently generated world regions

This framework reference has two irregular ownership regions in one world frame.
Each expands a complete catalog program: a region root, a compound, two physical
modules and a marker. The western12×10m compound crosses the ownership seam while
remaining one source object. Both local journeys and the full-width outward/return
connection use the existing terrain/access/route owners. This is not a game setup.

The shared513×513 terrain source is declared once. Each request stores only its
original-lattice window and halo:1008 samples for west,1080 for east. Generation
domains overlap to contain complete seam-spanning sources; ownership does not
overlap. These polygons are generation contracts, not storage cells or streets.

```csharp
var definition = await WorldMetadataGenerator.ReadAsync<MetadataRegionalWorldDefinition>(
    "examples/metadata/generation-regions/definition.json");
var engine = new MetadataRegionalWorldGenerator();
var west = engine.GenerateRegion(definition, "west");
var east = engine.GenerateRegion(definition, "east");
await engine.PublishRegionAsync(definition, west, "generated/my-west");
await engine.PublishRegionAsync(definition, east, "generated/my-east");
var loadedEast = await engine.ReadRegionAsync("generated/my-east", definition);
var loadedWest = await engine.ReadRegionAsync("generated/my-west", definition);
var world = engine.Assemble(definition, [loadedEast, loadedWest]);
await engine.PublishAsync(world, "generated/my-regional-world");
```

CLI entry points use the same APIs and existing Log/Audit/Notification boundary:

- `generate-region <definition.json> <region-id> <new-output-dir>`
- `generate-regional-world <definition.json> <new-output-dir>`
- `assemble-regional-world <definition.json> <region-directory-list.json> <new-output-dir>`

The directory list is a JSON array of saved region directories. Each packet
replays its exact selected source, build and external dependencies. Unchanged
packets may be reused across another region's local content edit. Assembly checks
the current collection, physical overlaps, complete local paths and cross-region
connections again. A canonical address includes owner region, member kind and
local ID, allowing the same recipe template in multiple regions.

Publication includes complete ordinary metadata/JavaScript inspection per region,
assembled JSON and a combined `schematic.html` linked from `index.html`. The
combined map preserves whole structures across ownership boundaries, repeated
template IDs and exact saved directed paths. Region focus, source selection,
terrain/semantic/layer controls and SVG export inspect the same saved coordinates.
Browser import performs structural checks; native read/replay authenticates source
and generation proof. Automatic cross-region
repair and runtime loading remain open. All original world generation/design
gates remain open; artwork and Godot are deferred.

[Inspection verification](../../../docs/evidence/2026-10-31-regional-inspection-verification.json).

## Transactional editing

`richer-definition.json` requires three modules inside the existing west compound.
It retains the original compound/module dimensions, terrain source, world extent
and budgets. The engine realizes every required member and requalifies its local
and regional paths; unchanged east content remains exact.

```csharp
var core = new WorldMetadataGenerator(); // Supply a registry when source types need it.
var session = new MetadataRegionalGenerationSession(core, definition);
var before = session.Snapshot;
var edit = new MetadataRegionalEditRequest(
    "richer", "Add the complete declared module program", "example:module-program",
    before.World.DefinitionSha256, before.Report.WorldSha256, richerDefinition);
await session.ApplyAndPublishAsync(edit, "generated/my-regional-edit");
var restored = await MetadataRegionalGenerationSession.RestoreAsync(
    core, "generated/my-regional-edit/generation-session.json");
```

CLI adapters expose `region-session start <definition.json> <new-output-dir>` and
`region-session edit <generation-session.json> <edit.json> <new-output-dir>`.
An orchestrator authors the edit using both current snapshot hashes; hashes are
not hardcoded because generated proof is build/dependency pinned. SDK hosts use
the reporting overloads for shared Log/Audit/Notification integration, including
committed-state recovery when a success sink fails. Every output is a new atomic
packet with source-only history and combined/individual inspection.

Saved replay requires exact historical dependencies. SDK hosts can supply the
existing installed-dependency resolver; files never install executable caches or
download implementations. Rollback is an explicit new edit with a prior source
definition and current hashes. These are framework technical programs, not a
specific game or completed world-design acceptance.

[Current regional session verification](../../../docs/evidence/2026-10-31-regional-session-verification.json).

[S08 plan](../../../docs/world-generation/stages/s08-deterministic-chunks-and-persistence.md)
and [metadata contract](../../../docs/WORLD_METADATA.md).
