# Directed game progression

This game-independent example uses the common topology generator to create
a one-way sequence of two stages. All 24 required destinations are reachable
from the entry, the intermediate gate remains mandatory, and three additional
links are forward shortcuts. The generator preserves the catalog hierarchy.

The physical variant supplies terrain, required compounds and a forward-only
route between them. `Directed` is optional; omitting it retains bidirectional
links. This example produces an acyclic topology and do not promise return
travel. This is a framework fixture, not a complete game or art asset.

See [topology contract](../../../docs/WORLD_METADATA.md#generated-progression-topology).
