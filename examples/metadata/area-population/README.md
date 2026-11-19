# Density-weighted habitat population

Framework inputs for the C# metadata engine, without artwork, mockups or Godot.
Both variants retain the complete six-compound source program and all 194 required
environmental members: 96 lowland, 96 highland and two woodland members.
Source footprints, reservations and the one-meter separation rule are unchanged.

Purpose-bearing power allocations separate activity areas from habitats. The area
owner integrates the declared density, recursively balances mass along geometric
principal axes and derives one weighted centroid per connected stratum. The
existing prescribed-placement owner qualifies the whole physical assignment.
Failed contents reject; no item is dropped or moved into an arbitrary vacancy.

`baseline` uses `fertility = 3 + 0.1 * height`; `reversed-density` uses
`fertility = 3 - 0.1 * height`. Changing only that field moves every environmental
member while source compounds, terrain/regions, allocation geometry and all
nineteen source connections stay exact. Both worlds retain 223 nodes, 222 physical
members and fourteen nested programs.

Independent habitat areas are 2,019.5 lowland, 445455/272 highland and 68 woodland
square meters. Their baseline masses are 75240547/15000, 108930607659/18496000 and
25806/125. These measure habitat strata, not physical object coverage.

`PlacementPlan.PopulationAreas` contains exact leaf cells/boundaries, area, mass,
world centroid and member provenance. The JavaScript schematic consumes these
witnesses; **Population strata** is an optional layer, off by default. The internal
partition does not create streets or physical lots.

Use WorldCreator.Cli `generate-metadata <recipe.json> <catalog.json> <new-output-dir>`.
Qualification packets are in `generated/area-population-accepted`. The test writer
`--write-area-population-examples` reproduces the inputs into a new directory.

Counts, habitat purpose and density rules remain authored. Automatic world intent,
broader distribution strategies, larger solid backends and world-scale streaming/
throughput remain open. Godot is deferred. See
[verification](../../../docs/evidence/2026-10-29-area-population-verification.json).
