# Portable complete population world

Reusable framework inputs combining shared terrain, derived regional ownership,
complete nested physical programs, surroundings, content domains and connecting
journeys. No game setup, mockup, artwork or Godot project is required.

| Request | Declared result |
| --- | --- |
| `request.json` | Three regions in a 336×224m world; 48 nodes, 45 physical sources and ten directed local/regional paths. |
| `terrain.json` | Places a 560m² protected void inside the baseline beta compound's actual footprint; the engine moves the complete compound without shrinking it or losing members. |
| `source-size.json` | Each compound grows from 58×50m to 64×50m; capacity measures 2900→3200m² and all nested module/content sources remain. |
| `more.json` | Three modules per compound with six buffer members and two equipment members per module; 87 nodes, 84 physical sources and sixteen directed paths. |

Use `generate-derived-regional-world <request.json> <new-output-dir>` with the
current CLI, or `MetadataRegionalOwnershipPlanner.Plan` followed by
`MetadataRegionalWorldGenerator.Generate`. Ordinary `WorldMetadataGenerator`
generation of a bound region template produces the identical complete packet.
Output directories must be new. The published whole-world schematic also links
to each individual region inspector and complete source/definition data.

Each template leaves its surface, origin and regional root anchor unbound. Its
`PopulationSource.Context.PlacementAnchorIds` explicitly requests
`regional-origin`. The existing population owner inherits the actual caller
surface/origin and that named anchor before source generation. Source overrides,
missing context/anchors and recursive sources refuse; no previously generated
world positions are copied into templates.

The source phase generates a complete compound and its source modules. Local
module sequences are deliberate game-authored grammars. Full module dimensions,
directional site features, 1.5m entrances and original local journeys remain. The
consumer adds all required objects through actual per-parent buffer and covered
work-floor domains. Whole source bodies/reservations must fit their domains and
preserve source paths. Regional paths qualify the unchanged 0.4m actor diameter.

Compounds declare a minimum 85% open source-ground fraction. Final physical
accounting enforces it after all nested content. Source-open area is not a runtime
walkability certificate. Ground rules account for the complete owned terrain and
the protected void; material coverage does not imply that every area is filled
with objects. Ownership boundaries are generation ownership, not mandatory roads
or physical barriers.

Public `AnalyzeProgram` retains complete consumer and `SourceProgram` expansion,
source/choice/dependency provenance and unbound coordinates. Capacity measures the
complete root envelope once. Static analysis is not placement or a guarantee of
physical feasibility. Terrain-derived count/composition choices remain separately
scoped; unsupported coupling refuses.

Enable **Population strata** in the inspector to see exact source masks and all
required anchors. Selecting an object shows its semantic owner, nested parent,
domain and source provenance. Shared-coordinate navigation and SVG export retain
whole original geometry. The qualified source-history edit changes actual equipment
intent in one region while preserving the other complete region packets exactly.

This demonstrates complete declared composition through reusable owners. Broad
professional-world design acceptance, density-aware ownership, coupled repair,
selective locks, independent content streaming, artwork and gameplay integration
remain open. Godot stays deferred.

[API](../../../docs/WORLD_METADATA.md#portable-source-and-population-programs)
and [exact verification](../../../docs/evidence/2026-10-31-portable-population-verification.json).
