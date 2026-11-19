# Regional demand generation

[definition.json](definition.json) is a reusable two-region metadata example:
complete compounds, nested solid modules, irregular ownership, shared terrain,
local journeys and a bidirectional connection between actual compound ports.
The compound crossing an ownership seam remains one complete source. The example
does not impose a city layout, art style, game engine or mandatory world grid.

Use the existing C# regional generator with a private live generation plan:

```csharp
using WorldCreator.Core.Metadata;

var definition = await WorldMetadataGenerator.ReadAsync<MetadataRegionalWorldDefinition>(
    "examples/metadata/regional-demand-generation/definition.json", token);
var engine = new MetadataRegionalWorldGenerator();
var plan = engine.CreateGenerationPlan(definition,
    maximumRetainedPacketBytes: 16 * 1024 * 1024, token: token);

var west = engine.GenerateRegion(plan, "west", token);
var sameWest = engine.GenerateRegion(plan, "west", token); // private packet reused
engine.ReleaseRegion(plan, "west", token);
var east = engine.GenerateRegion(plan, "east", token);
west = engine.GenerateRegion(plan, "west", token);
var world = engine.Assemble(plan, token); // both regions must be resident
await engine.PublishAsync(world, "new-world-output", token);
```

`plan.Definition` and returned packets/worlds are independent mutable snapshots;
changing them does not edit the plan. Create another plan for an edited design.
`plan.Snapshot` reports sorted resident IDs, retained serialized packet bytes,
accepted generations, resident reuses, releases and completed assemblies. Failed
attempts do not increment successful counters or replace accepted packets.

Authored preparation validates the whole definition without expanding local
content. Each new request generates just its region; resident requests reuse it.
Dependency selection is checked across the pinned design before reuse/generation
and after completing the result. A different generator instance cannot use the
handle. Concurrent/reentrant mutations fail explicitly; snapshot reads remain
available. Cancellation and failed requests/assembly leave resident state intact.

The serialized packet cap is explicit (1 byte through 128 MiB, default 128 MiB).
The engine refuses an over-cap packet without evicting or truncating other regions.
Release regions explicitly. This cap does not measure managed heap, caller-held
copies, temporary generation allocations or source-definition memory. It cannot
guarantee that every legal region fits in the selected cap.

Assembly requires all declared packets resident and uses the ordinary seam,
whole-source, global collision and directed journey checks without repeating
local generation. It may still refuse incompatible regions. Keeping only a
subset resident is useful for regional consumers; it does not certify a complete
world's missing connections. Derived ownership preparation can run broader
semantic/physical planning; `DerivedDesignPreparation` reports that distinction.
This API does not make initial derived world design independent or infinite.

The handle is an ephemeral SDK facility, not a save format or CLI session.
`PublishRegionAsync(plan, regionId, newDirectory, token)` uses ordinary authoritative
replay/publication; saved reads and publication intentionally recheck source
generation. For derived definitions that replay may repeat global design work.
Existing public commands remain `generate-region`, `generate-regional-world` and
`assemble-regional-world`; external packets retain full replay checks.

See the [S08 owner](../../../docs/world-generation/stages/s08-deterministic-chunks-and-persistence.md)
and [verification and limits](../../../docs/evidence/2026-10-31-regional-demand-generation-verification.json).
The professional generator is still in progress. Godot and artwork are deferred.
