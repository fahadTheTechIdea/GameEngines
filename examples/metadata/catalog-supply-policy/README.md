# Registered catalog supply policy

This fixture uses the Modern catalog's actual `village-house/cottage/Standard` definition. Its 8 × 10 meter footprint and capacity five remain source-owned. Four active demands each consume 1.5 game-service-units: neighborhood access, potable water, power and wastewater. Pedestrian and utility connections use the existing terrain/access planner; the utility link carries three demands.

`MetadataServicePlanner` invokes the registered `modern.siting` policy for every active requirement. Adding `marine` to the potable supply's qualities rejects generation even though the required `potable` and `reliable` tags remain present. The actual CLI rejection is audited and creates no output directory.

The reviewed packet is `generated/catalog-supply-policy-reviewed`. Select the site to see the four policy identities, source bindings, physical distances and explicit unqualified context. These are game-service contracts, not a civil-engineering certification. Required gardens, region compatibility, neighbors, containment, full world generation, artwork and Godot remain separate gates.

For registered service plans, pass the actual registry to `WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(file, catalogs: registry)` and `new WorldMetadataSchematicExporter(registry)`. Missing or mismatched implementation provenance rejects. Rebuilding a registered catalog or its contract can change its digest; regenerate with the selected implementation rather than editing stored identities.

[API](../../../docs/WORLD_METADATA.md#common-catalog-supply-policy) · [Evidence](../../../docs/evidence/2026-10-27-world-supply-policy-verification.json).
