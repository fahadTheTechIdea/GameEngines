# Local terrain edits retain distant complete regions

This reusable framework example contains full nested programs, shared original-
lattice terrain and required outward/return journeys. The world uses authored
region domains; it does not impose a city model or a road grid.

[definition.json](definition.json) starts two complete compounds. The
[west overlay edit](west-overlay-edit.json) puts water across the required journey
after the western entrance. The engine calculates another valid path and rebuilds
the western packet, while retaining the eastern packet exactly. The
[moved overlay](moved-overlay-edit.json) removes that source from the western domain
and introduces it in the east, invalidating both. The
[shared height edit](shared-height-edit.json) changes the shared recipe and retains
global invalidation. All required structures, source dimensions and actor widths
remain unchanged.

Use the ordinary SDK session or `region-session start` / `region-session edit`.
The supplied edit files pin tested snapshots; construct fresh requests using the
current accepted `DefinitionSha256` and `Report.WorldSha256` in another session/build.

```csharp
var before = session.Snapshot;
var edit = new MetadataRegionalEditRequest(
    "terrain-revision", "Revise the intended local terrain", "developer:terrain",
    before.World.DefinitionSha256, before.Report.WorldSha256, revisedDefinition);
var result = await session.ApplyAndPublishAsync(edit, newOutputDirectory, token);
// result.Report.Regions records complete-packet reuse and actual stage execution.
```

Dependency domains use the same validated surface projection as generation.
Intersecting or boundary-touching patches/exclusions keep their entire geometry,
purpose and provenance. Provably disjoint overlays do not invalidate the region.
The projection is conservative: it uses the declared generation domain's closed
bounds, not only an existing path or building. Canonical source ordering keeps
equivalent overlay orderings eligible. Malformed remote sources still refuse.

Shared height recipes, world boundaries, ownership/domain masks and installed
providers remain dependencies. This does not localize height-operation edits or
infer that a region is unaffected because its current paths miss an edited source.
Reused packets still undergo ordinary complete-world collision, access and path
checks. A newly blocked required journey rejects the edit and preserves the prior
accepted world/cache/history. External saved packets retain full source replay.

[scale-16.json](scale-16.json) retains80 nodes/48 physical sources and every declared
round trip. A local overlay generates one packet and retains fifteen. The identical
current test harness with the prior Core rematerializes all sixteen packets;
independent comparison confirms the same full geometry. Measured edit observations
were2.66s/464MB allocated before and1.85s/347MB allocated after. Process peak working
set was167MB before and168MB after. These are single serial measurements, not
portable thresholds or a managed-heap guarantee. Full seam/path assembly still runs.

See the [S04 owner](../../../docs/world-generation/stages/s04-terrain-aware-connections.md)
and [commands, exact binaries, failures and limits](../../../docs/evidence/2026-11-01-regional-overlay-invalidation-verification.json).
This advances regional dependency invalidation. Broader world-design quality,
variety/scale and native streaming remain open; Godot/artwork are deferred. Browser
DOM/render review was not run because local browser use is disabled in Work settings.
