# Partitioned world persistence reference

`recipe.json` and `catalog.json` preserve the captured full 513×513 drainage/lake
request that previously failed monolithic world publication. All 263169 samples,
explicit inflow 16, level 128, required 32768 m² region, two encounter destinations,
32-face limit and original work declarations remain unchanged.

The current `generate-metadata` flow stores the complete 64037963-byte world in
62 SHA256-addressed 1 MiB parts (the last is shorter). `drainage-plan.json` uses the
same codec. The normal typed reader reconstructs and semantically replays both.
Every packet file remains below 16 MiB. The generated schematic loads and verifies
the same manifest, with destination inspection, layers, fit and actual SVG export.
This technical fixture is not a populated game world or artwork.

`domains.json` composes this dense physical domain with a three-state,
geometry-free progression domain. It changes only the explicit travel selection
to a terrain-qualified water network, preserving the declared physical program.
An explicit transition and complete forward/return journey retain source network
segments and logical links. The root collection and child packets use the same
storage owner; local coordinates remain independent.

A separate source-history test retains one full terrain relocation, a recovery
edit and 20 editorial history edits. All 22 accepted source requests and the full
original 263169-value buffer restore; geometry is reused for editorial edits,
while the source-bound storage index refreshes correctly. That test qualifies
large history integrity, not 20 new world designs.

Limits: ordinary authored inputs 16 MiB; each physical file 16 MiB; parts 1 MiB;
reconstructed generated document 256 MiB/256 parts. Source, cache, work and edit
limits remain independently enforced. Missing, corrupted, reordered and changed
parts refuse before typed acceptance. Cryptographic integrity does not replace
source replay, asset approval or game-design acceptance. Godot remains deferred.
