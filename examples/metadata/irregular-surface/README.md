# Irregular exploration surface

This example uses the shared world metadata engine with a concave domain and a planar hillside terrain field. It has five location/region nodes and three logical travel links. The domain's missing northeast corner stays outside the world; it is not filled by its rectangular bounds.

The engine validates positioned nodes against terrain coverage and the domain and persists the surface definition. It does not generate physical routes or destination footprints yet. The three links express travel intent only. `PartialScope` and the remaining-scope list retain those limits.

Inputs: [recipe](recipe.json), [catalog](catalog.json). Current exported data: [world metadata](../../../generated/irregular-surface/world-metadata.json). See [the surface contract](../../../docs/WORLD_METADATA.md#irregular-surface-domains-and-terrain-constraints) for supported queries, boundaries and limits. The existing hierarchy schematic is not an exact drawing of the physical surface.
