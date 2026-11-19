# Directional seeded terrain

These framework inputs keep the same two required compounds, three
bidirectional paths and protected terrain corridor. The directional recipe
rotates the seeded sampling axes by 30 degrees and stretches the second axis
by a factor of two. The baseline uses the identity transform. `capture-recipe`
uses a bounded original-lattice terrain window.

The fixture uses a small detail amplitude to exercise integration, not to
demonstrate visual terrain quality. It does not require a game, art or Godot.
See the [terrain contract](../../../docs/WORLD_METADATA.md).
