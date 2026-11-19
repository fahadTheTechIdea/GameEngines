# Terrain-owned counts with capacity

The dry habitat provides room for exactly four required members. Their selected
authored variants provide 18 units against a requirement of 15. The wet recipe
protects a strip of terrain, reducing the eligible count to exactly three; those
members provide 13 units, so the original 15-unit demand is rejected. The engine
does not add a fourth member to override the terrain count.

Use `dry-recipe.json` with `catalog.json` to generate the first world. Use
`wet-recipe.json` with `wet-catalog.json` to generate the feasible three-member
world with its explicitly revised 12-unit demand. The wet recipe with the
original catalog demonstrates the attributed capacity shortfall.

These are engine-independent framework inputs. `generate-metadata` creates a
source package, and `verify-metadata` replays it. The example is not a complete
game, artwork package or Godot project.
