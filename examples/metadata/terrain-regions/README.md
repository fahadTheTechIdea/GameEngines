# Terrain-connected region example

The normal `generate-metadata` CLI and `WorldMetadataGenerator` API accept this recipe. It requests two named regions and two catalog-sized activity sites. C# derives region geometry from a sloping surface, an altitude limit, a diagonal excluded channel and a protected clearing. JavaScript displays the stored geometry without generating or repairing it.

The western region is 3,856 square meters. The eastern region is approximately 1,937.24 square meters. Both retain their minimum areas; the sites keep 8×6-meter physical footprints and 10×8-meter reservations. The remaining land is explicitly outside the region rules. The internal convex decomposition is not drawn as a block grid.

This is a region/placement qualification fixture, not a finished world or city. Local subdivision, interfaces/crossings, routes, complete content programs, GameArts production and native Godot acceptance remain open. A named seed selects one connected suitable component. Two seeds in the same component require an explicit partition strategy; this capability rejects that request instead of inventing a border.
