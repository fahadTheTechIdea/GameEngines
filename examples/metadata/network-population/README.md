# Population from a generated destination network

This verification example uses the generic world pipeline. The base recipe keeps
seven physical members, including three journey destinations. The network owner
selects a terrain-qualified backbone and retains a required connection. The final
recipe adds four required markers by sampling an actual generated edge ID. It
contains no copied route coordinates; all eleven physical members are retained.

`recipe.json` and `moved-rock.json` differ in the rock exclusion's position in both
source and complete recipes. Network paths and dependent markers respond; the
other source members retain their identities, positions and dimensions. The final
network retains the base's exact destinations, edges and paths. `PreservationWork`
reports current validation separately from source search/candidate receipts.

```powershell
dotnet run --project src/WorldCreator/WorldCreator.Cli --no-build -- generate-metadata examples/metadata/network-population/recipe.json examples/metadata/network-population/catalog.json generated/network-population-initial
dotnet run --project src/WorldCreator/WorldCreator.Cli --no-build -- generate-metadata examples/metadata/network-population/moved-rock.json examples/metadata/network-population/catalog.json generated/network-population-moved
dotnet run --project src/WorldCreator/WorldCreator.Cli --no-build -- generate-metadata examples/metadata/network-population/blocked-passage.json examples/metadata/network-population/catalog.json generated/network-population-refused
```

The third input puts required reservations into a protected passage. It must fail
without publishing a packet. Existing output directories are never overwritten.
Integration tests also exercise reverse sampling, mixed explicit/network sources,
tampering, changed destination sets, source reuse, failed-edit rollback and actual
source-history checkpoint publication/restoration. This is a bounded verification
example, not a complete city or approved artwork. Region interiors, sightlines,
variation, scale and the GameArts/native Godot stages remain open.
