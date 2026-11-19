# Seeded regional world

This framework fixture declares three purposeful regions inside an irregular
terrain boundary, with a protected hole, three nested physical programs and
connections between them. It is game-independent source data, not a complete
playable game or a visual design.

`recipe.json` declares `Regions.IntentPartition.Pattern.PartitionSearch` with
the allowed axis offsets `0`, `90`, and `45` degrees and the seed `world-1`.
The engine uses the seed to order those permitted choices for each partition;
it does not invent angles or promise every seed produces a different result.
Change the seed to explore alternatives, preserving the source catalog and
declared spatial requirements. Generation can explicitly fail when those
requirements cannot be met.

The C# `WorldMetadataGenerator` and `generate-metadata` CLI accept these source
files. The output is engine-independent metadata; downstream game integration,
art and Godot remain separate work. The source declares `PartialScope`.
