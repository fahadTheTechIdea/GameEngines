# Bounded local repartition

Two mandatory source-sized structures have authored positions. The initial local grid boundary at x=20 cannot contain their required footprints/reservations and entrance approaches. `LayoutAdaptation` permits the owning grid width to increase in 2 m steps. The engine rejects the baseline, selects x=22, and recomputes the physical connection. Both 14 × 6 m footprints, 16 × 8 m reservations, authored positions and all four constituent metadata points remain.

The policy authorizes one layout and finite parameter ranges. It cannot resize content, delete programs, move anchors, change interfaces or modify another layout. The weighted-partition variant is exercised in the focused C# tests. Both strategies use the same allocation, placement, physical-access and route owners. One aggregate work budget covers all attempts. Cancellation, malformed declarations and exhausted budgets are terminal.

[Generated schematic](../../../generated/local-repartition/schematic.html) shows selected geometry and saved rejected-layout history. [Verification](../../../docs/evidence/2026-10-27-world-adaptation-verification.json) records local scope and limitations. This is an algorithm fixture, not a complete city/world or artwork.

Internal invocation:

```powershell
dotnet src/WorldCreator/WorldCreator.Cli/bin/Debug/net10.0/WorldCreator.Cli.dll generate-metadata examples/metadata/local-repartition/recipe.json examples/metadata/local-repartition/catalog.json <new-output-directory>
```
