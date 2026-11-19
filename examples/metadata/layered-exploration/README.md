# Layered exploration metadata

This input uses the shared compiler for nested exploration floors, encounter locations and logical traversal links. Layer kinds are authored data. The engine validates dependencies and exact membership and emits metadata schema 2. No city generator, art service or Godot project is used.

The example has 15 nodes and 12 logical links. Parent-relative path coordinates place six rooms on each of two floors. `regions`, `encounters` and `traversal` are semantic layers; floor elevation remains in coordinates. This fixture does not supply room walls, collision, terrain or playable navigation.

Generated output for this change is in [the review](../../../generated/layered-exploration/review.html). The [shared metadata contract](../../../docs/WORLD_METADATA.md#semantic-layers-and-current-output-schema) describes validation and remaining limits.
