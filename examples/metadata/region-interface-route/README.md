# Physical travel through a generated region interface

This framework example generates three terrain-qualified regions and complete
nested compound programs. The `region-gate` relationship requires a shared
boundary between alpha and beta; an explicit bidirectional connection binds a
route to that region relationship. The engine qualifies source ports, both
region domains, terrain, each crossing leg and full traveler width.

The geometric relationship alone does not create travel. The route rule uses
`MetadataInterfaceBinding` with `Source: RegionRelation`, the connection ID and
the relationship ID. Each endpoint's placement declares the corresponding
`RegionId`; nested members may inherit it from their placed parent.

Run through the public `WorldMetadataGenerator` or `generate-metadata` CLI with
`recipe.json` and `catalog.json`. This is an engine-independent framework input,
not a complete game or visual approval. [Contract](../../../docs/WORLD_METADATA.md)
and [verification](../../../docs/evidence/2026-11-01-region-interface-route-verification.json).
