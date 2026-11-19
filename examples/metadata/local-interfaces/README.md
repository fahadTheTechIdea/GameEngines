# Physical connections between local allocations

This engine fixture extends `local-layouts` with two explicit bidirectional journeys. Seven source-sized sites and fourteen constituent metadata points remain. One journey crosses the western oriented-grid boundary; the other crosses an irregular eastern allocation boundary. These are selected connections, not a complete world transport network.

Each route rule binds an explicit connection ID to a declared local interface and named catalog entrances. C# qualifies the full 0.6 m passage envelope against terrain, solids, allocation boundaries and the region profile. Source and destination legs stay in their allocations. A bounded candidate search selects a perpendicular crossing and retains failed crossing attempts. The route stores all three legs and direction-dependent costs; read/export replays the result.

The 8 × 6 m source footprints and 10 × 8 m reservations are unchanged. Site-level entrance failures are rejected before whole-assignment search and recorded in placement diagnostics. Catalog constituent markers are metadata points, not physically allocated substructures.

Generated review: [schematic](../../../generated/local-interfaces-final/schematic.html). Verification: [interface evidence](../../../docs/evidence/2026-10-27-world-interfaces-verification.json).

Internal execution:

```powershell
dotnet src/WorldCreator/WorldCreator.Cli/bin/Debug/net10.0/WorldCreator.Cli.dll generate-metadata examples/metadata/local-interfaces/recipe.json examples/metadata/local-interfaces/catalog.json <new-output-directory>
```

No adaptive repartition, multilevel passage, vehicle turn model, complete site population, production art or native Godot runtime acceptance is claimed.
