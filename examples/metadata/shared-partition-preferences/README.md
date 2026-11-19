# Shared terrain with distinct region preferences

This framework example describes three nested physical programs in one
terrain-derived partition. Each intent has the same hard suitability rules and
an independently authored preference for a value of `horizontal-position`.
WorldCreator first partitions the suitable component, then selects a bounded
qualified anchor inside each resulting region. The field is a test of the
generic environmental contract, not a requirement for any game genre.

Use the C# `WorldMetadataGenerator` with `recipe.json` and `catalog.json`, or
the CLI `generate-metadata <recipe.json> <catalog.json> <new-output-directory>`.
The tested world contains 11 nodes, nine physical sites and a required local
network. Its declared coverage is `PartialScope`; it is not a complete game.

Preferences do not automatically change partition boundaries and the finite
anchor search does not guarantee a global optimum. Hard suitability, clearance,
positive density and declared region relationships remain required. Different
hard suitability profiles cannot share this partition strategy.

`related-recipe.json` adds a maximum distance between two intents. In its tested
world their first-choice anchors are too far apart; the engine tries a later
qualified anchor and records its zero-based `AnchorPreferenceRank` in the area
stratum. A later fixed anchor can also force an earlier preferred anchor to be
reconsidered; the search remains bounded by declared work and attempt limits.
`pinned-related-recipe.json` demonstrates that case: `gamma` keeps an exact
authored anchor, while the earlier `beta` program can select another suitable
anchor to satisfy the required distance.
`revised-preference-recipe.json` changes beta's preference and is exercised as
a real reported source edit by the public
[SDK workflow](../../worldcreator-workflow/README.md#metadata-recipe-revisions).
