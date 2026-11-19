# Composed spatial world programs

These reusable catalogs and recipes exercise the shared C# world composition core without streets, blocks or parcels:

- `dungeon`: two floors with rooms along a bent route; floor-relative z offsets and an explicit stair connection.
- `platformer`: four levels whose sections follow a changing-height polyline, with directed progression links.
- `space`: two systems with destinations on an x/z ring around each parent.

They are technical examples of hierarchy, topology and spatial metadata, not complete dungeon, platformer or space generators. The engine does not infer terrain, walls, collision, feasible jumps or orbital motion from these kinds. The supplied graph connections are not physical corridor geometry. The built-in schematic remains a hierarchy diagram when `MapGeometry` is absent; inspect JSON/node metadata for calculated coordinates.

Generate through the existing `generate-metadata` boundary. Current review outputs are under `generated/world-programs/`. The catalog part selects `MetadataLayout.StrategyId` and strategy-owned parameters; custom strategies register through `IMetadataLayout` in the API. No genre-specific branch or old line-only JSON reader is present.
