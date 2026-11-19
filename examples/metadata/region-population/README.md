# Population inside generated regions

This example preserves two complete activity sites and adds four required
resource stations. Their 2 × 1.6 m source footprints and 2.6 × 2.2 m reservations
are rotated 30 degrees. Two deliberate interior bands sample the generated west
region, whose actual river boundary is diagonal and whose interior contains an
excluded clearing. No region outline or output coordinates are copied into the
population grammar. There are no roads in this program.

`moved-river.json` shifts the river five meters. All four dependent resources move
five meters; the original sites stay fixed. `local-allocation.json` selects a
generated power-diagram allocation within the west region and uses its exact
shape. `outside-reservation.json` requests an inset where the resource footprints
fit but their required clearance crosses the river boundary. That entire proposal
must fail without publication.

```powershell
dotnet run --project src/WorldCreator/WorldCreator.Cli --no-build -- generate-metadata examples/metadata/region-population/recipe.json examples/metadata/region-population/catalog.json generated/region-population-initial
dotnet run --project src/WorldCreator/WorldCreator.Cli --no-build -- generate-metadata examples/metadata/region-population/moved-river.json examples/metadata/region-population/catalog.json generated/region-population-moved
dotnet run --project src/WorldCreator/WorldCreator.Cli --no-build -- generate-metadata examples/metadata/region-population/local-allocation.json examples/metadata/region-population/catalog.json generated/region-population-local
dotnet run --project src/WorldCreator/WorldCreator.Cli --no-build -- generate-metadata examples/metadata/region-population/outside-reservation.json examples/metadata/region-population/catalog.json generated/region-population-refused
```

Existing output directories are not overwritten. Integration tests cover equivalent
terrain triangulations and rotated bands, explicit span selection around holes,
count/capacity failure, complete nested parent frames, source replay/tampering,
incremental source reuse, terrain invalidation, rollback and checkpoint replay.
This is a verification program, not a complete city or approved artwork. Sightlines,
variation, scale and the GameArts/native Godot stages remain open.
