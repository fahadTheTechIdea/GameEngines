# Weighted local programs

This framework fixture divides a suitable region into two purposeful local
programs using `AreaBalanced`. Their `AllocationWeight` values are 11 and 10,
independent of their minimum required areas. Both nested physical structures,
their reservations, access and the required route remain present.

Weights are relative to the other intents within the same local layout. When a
density field is declared they apply to density-weighted mass, not raw area.
The engine preserves each minimum and all access requirements. In this tightly
constrained fixture, the more extreme 2:3 and 3:2 ratios refuse because an
approach would be blocked; the example does not resize or omit content.

Use the recipe and catalog with `WorldMetadataGenerator` or `generate-metadata`.
The fixture is not a complete game, artwork or runtime integration.
