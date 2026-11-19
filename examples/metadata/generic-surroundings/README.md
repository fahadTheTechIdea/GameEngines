# Generic catalog surroundings

This framework example uses `new WorldMetadataGenerator()` with a game-authored
catalog. No registered building, Modern catalog, mockup, artwork or Godot is needed.
`PhysicalFootprint.SiteRules` owns the required arrival area, repair yard and side
buffer. The placement binding owns directional depths and the selected entrance.
Both generic and registered sources use the same `BuildingSitePlanner` grammar.

The source is an off-center 8 × 10m rectangle, with an authored asymmetric
reservation. Requested front/rear depths of4m and side depths of2m produce a
12 × 18m reservation without changing the physical source. At30° and center
(50,50), the engine saves rotated source features and translated world geometry.

| Feature | Area |
| --- | ---: |
| Entrance walkway | 6m² |
| Required repair yard | 34.5m² |
| Required arrival area | 40.25m² |
| Required side buffer | 14.25m² |

The arrival forecourt deliberately shares its entrance strip. The optional plant
requires5m and is omitted from its2m band; required rules would reject instead.
Larger existing reservations are preserved. Repeated and nested instances receive
their complete site program. Generic rectangular sources support off-center entrances
on any edge when the full doorway fits; front/rear follow the selected edge and
left/right follow the entrance-aligned source frame. The binding's rotation then
turns the complete site in the world. Nonrectangular source geometry,
non-meter site rules, mismatched entrances and missing required layout reject.

The CLI consumes `recipe.json`, `catalog.json` and a new output folder through
`generate-metadata`. The [contract](../../../docs/WORLD_METADATA.md#catalog-surroundings-and-directional-site-bands)
describes the API. This two-node fixture demonstrates semantic site geometry;
it does not certify a complete world, artwork or runtime navigation of each zone.

`off-center-recipe.json` and `off-center-catalog.json` move the declared entrance
two meters left of the footprint center while retaining the complete site program.
Its walkway follows that entrance through the same 30-degree source rotation.
`side-recipe.json` and `side-catalog.json` instead select an off-center entrance
on the positive-X face; its walkway and surrounding zones follow that face.
