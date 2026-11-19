# Weighted layout adaptation

This game-neutral example requests two area-balanced programs with relative
weights 11:10 and a required physical connection. Its first declared orientation
cannot place the complete required sources. The existing bounded adaptation
policy permits one alternate 90-degree rotation; the engine retains the failed
attempt, selects the alternative and preserves both weights, all seven nodes,
both physical sites and the required route.

The policy is explicit: it searches only the declared two orientations within
the declared limits. It does not guarantee another feasible orientation exists.
Use the recipe and catalog with `WorldMetadataGenerator` or `generate-metadata`.
The output is world metadata, not a complete game or runtime integration.
